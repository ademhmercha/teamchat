import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import { ChatService } from '../chat/chat.service';
import {
  CallSessionDto,
  CallStatus,
  CallType,
  IceCandidatePayload,
  IceServerDto,
  SdpPayload,
} from '../models/call.models';

export type CallPhase = 'idle' | 'outgoing' | 'incoming' | 'active' | 'ended';

const ENDED_MESSAGE_MS = 2500;

/**
 * Orchestre les appels 1-a-1 : capture locale, WebRTC (RTCPeerConnection) et
 * signalisation via le hub (ChatService). L'etat est expose sous forme de
 * signals consommes par l'overlay d'appel.
 */
@Injectable({ providedIn: 'root' })
export class CallService {
  private readonly http = inject(HttpClient);
  private readonly chat = inject(ChatService);
  private readonly auth = inject(AuthService);

  private peer: RTCPeerConnection | null = null;
  private localStream: MediaStream | null = null;
  private screenStream: MediaStream | null = null;
  private cameraTrack: MediaStreamTrack | null = null;
  private screenTrack: MediaStreamTrack | null = null;
  private videoSender: RTCRtpSender | null = null;
  private iceServers: RTCIceServer[] | null = null;
  private pendingRemoteCandidates: RTCIceCandidateInit[] = [];
  private tracksAdded = false;
  private endedTimer?: number;

  readonly phase = signal<CallPhase>('idle');
  readonly session = signal<CallSessionDto | null>(null);
  readonly incoming = signal<CallSessionDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly statusText = signal<string | null>(null);
  readonly muted = signal(false);
  readonly cameraOn = signal(false);
  readonly screenSharing = signal(false);
  readonly localStreamSignal = signal<MediaStream | null>(null);
  readonly remoteStream = signal<MediaStream | null>(null);
  readonly remoteHasVideo = signal(false);

  /** L'appel est reduit en petit widget flottant pour laisser l'app utilisable. */
  readonly minimized = signal(false);
  /** La video distante (partage d'ecran ou camera) est agrandie dans la fenetre. */
  readonly enlarged = signal(false);

  /** Le navigateur supporte-t-il le partage d'ecran ? (faux sur mobile). */
  readonly canScreenShare = typeof navigator !== 'undefined' && !!navigator.mediaDevices?.getDisplayMedia;

  /** Une piste video locale est-elle active (camera ou partage d'ecran) ? */
  readonly localHasVideo = computed(() => this.cameraOn() || this.screenSharing());
  /** Faut-il afficher une zone video (locale ou distante) ? */
  readonly showVideo = computed(() => this.remoteHasVideo() || this.localHasVideo());

  private readonly remoteMedia = new MediaStream();

  readonly visible = computed(() => this.phase() !== 'idle');
  readonly currentUserId = this.auth.user()?.id ?? null;

  constructor() {
    this.chat.incomingCall$.subscribe((call) => this.onIncomingCall(call));
    this.chat.callAccepted$.subscribe((call) => void this.onCallAccepted(call));
    this.chat.callEnded$.subscribe((payload) => this.onCallEnded(payload.callId, payload.status));
    this.chat.receiveOffer$.subscribe((payload) => void this.onOffer(payload));
    this.chat.receiveAnswer$.subscribe((payload) => void this.onAnswer(payload));
    this.chat.receiveIceCandidate$.subscribe((payload) => void this.onRemoteCandidate(payload));
  }

  peerName(): string {
    const session = this.session();
    if (session) {
      return session.callerId === this.currentUserId
        ? session.calleeDisplayName
        : session.callerDisplayName;
    }

    return this.incoming()?.callerDisplayName ?? 'Appel';
  }

  peerAvatarUrl(): string | null {
    const session = this.session();
    if (session) {
      return session.callerId === this.currentUserId
        ? session.calleeAvatarUrl ?? null
        : session.callerAvatarUrl ?? null;
    }
    const incoming = this.incoming();
    return incoming?.callerAvatarUrl ?? null;
  }

  callType(): number {
    return this.session()?.type ?? this.incoming()?.type ?? CallType.Audio;
  }

  isVideo(): boolean {
    return this.callType() === CallType.Video;
  }

  async startCall(calleeId: string, calleeName: string, type: number, calleeAvatarUrl: string | null = null): Promise<void> {
    if (this.phase() !== 'idle') {
      return;
    }

    this.clearEndedTimer();
    this.error.set(null);
    this.statusText.set('Appel en cours…');
    this.phase.set('outgoing');
    this.session.set({
      callId: '',
      callerId: this.currentUserId ?? '',
      callerDisplayName: this.auth.user()?.displayName ?? '',
      calleeId,
      calleeDisplayName: calleeName,
      callerAvatarUrl: this.auth.user()?.avatarUrl ?? null,
      calleeAvatarUrl,
      type,
      status: CallStatus.Ringing,
      conversationId: '',
      startedAt: new Date().toISOString(),
    });

    try {
      await this.prepareLocalMedia(type);
      const session = await this.chat.callUser(calleeId, type);
      this.session.set(session);
    } catch (error) {
      this.fail(this.mediaErrorMessage(error, type === CallType.Video ? 'camera' : 'microphone'));
    }
  }

  async acceptIncoming(): Promise<void> {
    const call = this.incoming();
    if (!call || this.phase() !== 'incoming') {
      return;
    }

    this.error.set(null);
    this.session.set(call);
    this.phase.set('active');
    this.statusText.set('Connexion…');

    try {
      await this.prepareLocalMedia(call.type);
      await this.chat.acceptCall(call.callId);
      await this.ensurePeer();
      this.addLocalTracks();
      this.incoming.set(null);
    } catch (error) {
      this.fail(this.mediaErrorMessage(error, call.type === CallType.Video ? 'camera' : 'microphone'));
    }
  }

  async rejectIncoming(): Promise<void> {
    const call = this.incoming();
    this.incoming.set(null);
    if (call) {
      await this.chat.rejectCall(call.callId).catch(() => undefined);
    }
    this.finish('Appel refuse');
  }

  async hangup(): Promise<void> {
    const session = this.session();
    if (session?.callId) {
      await this.chat.endCall(session.callId).catch(() => undefined);
    }
    this.finish('Appel termine');
  }

  toggleMute(): void {
    const stream = this.localStream;
    if (!stream) {
      return;
    }

    this.muted.update((value) => !value);
    const enabled = !this.muted();
    stream.getAudioTracks().forEach((track) => (track.enabled = enabled));
  }

  async toggleCamera(): Promise<void> {
    if (this.screenSharing()) {
      return;
    }

    if (this.cameraOn()) {
      this.cameraTrack?.stop();
      this.cameraTrack = null;
      this.replaceLocalVideoTrack(null);
      this.cameraOn.set(false);
      return;
    }

    try {
      const media = await navigator.mediaDevices.getUserMedia({ video: this.videoConstraints() });
      const track = media.getVideoTracks()[0] ?? null;
      if (!track) {
        return;
      }

      this.cameraTrack = track;
      await this.applyVideoTrack(track);
      this.cameraOn.set(true);
    } catch (error) {
      this.error.set(this.mediaErrorMessage(error, 'camera'));
    }
  }

  async toggleScreenShare(): Promise<void> {
    if (this.screenSharing()) {
      this.stopScreenShare();
      return;
    }

    if (!navigator.mediaDevices?.getDisplayMedia) {
      this.error.set('Le partage d\'ecran n\'est pas disponible sur ce navigateur.');
      return;
    }

    try {
      const display = await navigator.mediaDevices.getDisplayMedia({ video: true });
      const track = display.getVideoTracks()[0] ?? null;
      if (!track) {
        return;
      }

      this.screenStream = display;
      this.screenTrack = track;
      track.onended = () => this.stopScreenShare();
      await this.applyVideoTrack(track);
      this.screenSharing.set(true);
    } catch {
      // annule par l'utilisateur
    }
  }

  dismissError(): void {
    this.error.set(null);
  }

  /** Reduit l'appel en widget flottant (ou le retablit) pour continuer a utiliser l'app. */
  toggleMinimize(): void {
    if (this.phase() !== 'active') {
      return;
    }

    this.minimized.update((value) => !value);
    if (this.minimized()) {
      this.enlarged.set(false);
    }
  }

  /** Agrandit la video distante (partage d'ecran) dans la fenetre. */
  toggleEnlarge(): void {
    if (!this.remoteHasVideo()) {
      return;
    }

    this.enlarged.update((value) => !value);
    if (this.enlarged()) {
      this.minimized.set(false);
    }
  }

  private stopScreenShare(): void {
    this.screenTrack?.stop();
    this.screenTrack = null;
    this.screenStream = null;
    this.screenSharing.set(false);

    const fallback = this.cameraOn() ? this.cameraTrack : null;
    void this.replaceLocalVideoTrack(fallback);
  }

  private async applyVideoTrack(track: MediaStreamTrack): Promise<void> {
    const stream = this.localStream;
    if (!stream) {
      return;
    }

    stream.getVideoTracks().forEach((existing) => {
      if (existing !== track) {
        stream.removeTrack(existing);
      }
    });
    if (!stream.getVideoTracks().includes(track)) {
      stream.addTrack(track);
    }
    this.localStreamSignal.set(new MediaStream(stream.getTracks()));

    const peer = this.peer;
    if (!peer) {
      return;
    }

    if (this.videoSender) {
      await this.videoSender.replaceTrack(track);
    } else {
      this.videoSender = peer.addTrack(track, stream);
      await this.renegotiate();
    }
  }

  private async replaceLocalVideoTrack(track: MediaStreamTrack | null): Promise<void> {
    const stream = this.localStream;
    if (stream) {
      stream.getVideoTracks().forEach((existing) => stream.removeTrack(existing));
      if (track) {
        stream.addTrack(track);
      }
      this.localStreamSignal.set(new MediaStream(stream.getTracks()));
    }

    if (this.videoSender) {
      await this.videoSender.replaceTrack(track);
    }
  }

  private async renegotiate(): Promise<void> {
    const peer = this.peer;
    const session = this.session();
    if (!peer || !session?.callId || peer.signalingState !== 'stable') {
      return;
    }

    const offer = await peer.createOffer();
    await peer.setLocalDescription(offer);
    await this.chat.sendOffer(session.callId, offer.sdp ?? '');
  }

  private onIncomingCall(call: CallSessionDto): void {
    if (this.phase() !== 'idle') {
      void this.chat.rejectCall(call.callId).catch(() => undefined);
      return;
    }

    this.clearEndedTimer();
    this.error.set(null);
    this.statusText.set(null);
    this.incoming.set(call);
    this.phase.set('incoming');
  }

  private async onCallAccepted(call: CallSessionDto): Promise<void> {
    const session = this.session();
    if (!session || (session.callId !== '' && session.callId !== call.callId)) {
      return;
    }

    this.session.set(call);
    this.phase.set('active');
    this.statusText.set('En communication');

    try {
      await this.ensurePeer();
      this.addLocalTracks();
      await this.renegotiate();
    } catch (error) {
      this.fail(this.messageFrom(error));
    }
  }

  private async onOffer(payload: SdpPayload): Promise<void> {
    const session = this.session();
    if (!session || payload.callId !== session.callId) {
      return;
    }

    try {
      const peer = await this.ensurePeer();
      this.addLocalTracks();
      await peer.setRemoteDescription({ type: 'offer', sdp: payload.sdp });
      await this.flushRemoteCandidates();
      const answer = await peer.createAnswer();
      await peer.setLocalDescription(answer);
      await this.chat.sendAnswer(payload.callId, answer.sdp ?? '');
    } catch (error) {
      this.fail(this.messageFrom(error));
    }
  }

  private async onAnswer(payload: SdpPayload): Promise<void> {
    const session = this.session();
    if (!session || !this.peer || payload.callId !== session.callId) {
      return;
    }

    if (this.peer.signalingState === 'stable') {
      return;
    }

    try {
      await this.peer.setRemoteDescription({ type: 'answer', sdp: payload.sdp });
      await this.flushRemoteCandidates();
    } catch (error) {
      this.fail(this.messageFrom(error));
    }
  }

  private async onRemoteCandidate(payload: IceCandidatePayload): Promise<void> {
    const session = this.session();
    if (!session || payload.callId !== session.callId) {
      return;
    }

    const candidate: RTCIceCandidateInit = {
      candidate: payload.candidate,
      sdpMid: payload.sdpMid,
      sdpMLineIndex: payload.sdpMLineIndex,
    };

    if (!this.peer || !this.peer.remoteDescription) {
      this.pendingRemoteCandidates.push(candidate);
      return;
    }

    await this.peer.addIceCandidate(candidate).catch(() => undefined);
  }

  private async flushRemoteCandidates(): Promise<void> {
    const peer = this.peer;
    if (!peer) {
      return;
    }

    const pending = this.pendingRemoteCandidates;
    this.pendingRemoteCandidates = [];
    for (const candidate of pending) {
      await peer.addIceCandidate(candidate).catch(() => undefined);
    }
  }

  /** Recalcule la presence de video distante (une piste coupee/remplacee passe en "muted"). */
  private refreshRemoteVideo(): void {
    const hasLiveVideo = this.remoteMedia
      .getVideoTracks()
      .some((track) => track.readyState !== 'ended' && !track.muted);
    this.remoteHasVideo.set(hasLiveVideo);
    if (!hasLiveVideo) {
      this.enlarged.set(false);
    }
  }

  private onCallEnded(callId: string, status: number): void {
    const session = this.session();
    const incoming = this.incoming();
    if (session?.callId !== callId && incoming?.callId !== callId) {
      return;
    }

    this.finish(this.endMessage(status));
  }

  private async prepareLocalMedia(type: number): Promise<void> {
    if (this.localStream) {
      return;
    }

    if (!navigator.mediaDevices?.getUserMedia) {
      throw new Error('Ce navigateur ne prend pas en charge les appels.');
    }

    const wantsVideo = type === CallType.Video;

    try {
      this.localStream = await navigator.mediaDevices.getUserMedia(this.mediaConstraints(wantsVideo));
    } catch (error) {
      if (!wantsVideo) {
        throw error;
      }

      // La camera est indisponible/refusee : on poursuit en audio seul plutot que d'echouer.
      this.localStream = await navigator.mediaDevices.getUserMedia({ audio: true });
      this.error.set('Camera indisponible : appel demarre en audio seul.');
    }

    this.cameraTrack = this.localStream.getVideoTracks()[0] ?? null;
    this.cameraOn.set(wantsVideo && !!this.cameraTrack);
    this.localStreamSignal.set(new MediaStream(this.localStream.getTracks()));
  }

  /** Contraintes de capture : camera avant explicite (facingMode "user"). */
  private mediaConstraints(video: boolean): MediaStreamConstraints {
    return {
      audio: true,
      video: video ? this.videoConstraints() : false,
    };
  }

  private videoConstraints(): MediaTrackConstraints {
    return { facingMode: 'user', width: { ideal: 1280 }, height: { ideal: 720 } };
  }

  /** Traduit les erreurs getUserMedia (DOMException) en messages clairs pour l'utilisateur. */
  private mediaErrorMessage(error: unknown, kind: 'microphone' | 'camera'): string {
    if (error instanceof DOMException) {
      switch (error.name) {
        case 'NotAllowedError':
        case 'SecurityError':
          return `Acces ${kind === 'microphone' ? 'au micro' : 'a la camera'} refuse. Autorisez-le dans les reglages du navigateur.`;
        case 'NotFoundError':
        case 'DevicesNotFoundError':
          return kind === 'microphone' ? 'Aucun microphone detecte.' : 'Aucune camera detectee.';
        case 'NotReadableError':
        case 'TrackStartError':
          return 'Le peripherique est deja utilise par une autre application.';
        case 'OverconstrainedError':
          return 'Le peripherique ne correspond pas aux parametres demandes.';
      }
    }

    return this.messageFrom(error);
  }

  private async ensurePeer(): Promise<RTCPeerConnection> {
    if (this.peer) {
      return this.peer;
    }

    const session = this.session();
    const callId = session?.callId ?? '';
    const peer = new RTCPeerConnection({ iceServers: await this.getIceServers() });

    peer.ontrack = (event) => {
      event.streams[0]?.getTracks().forEach((track) => {
        if (!this.remoteMedia.getTracks().includes(track)) {
          this.remoteMedia.addTrack(track);
          if (track.kind === 'video') {
            track.onmute = () => this.refreshRemoteVideo();
            track.onunmute = () => this.refreshRemoteVideo();
            track.onended = () => this.refreshRemoteVideo();
          }
        }
      });
      this.remoteStream.set(this.remoteMedia);
      this.refreshRemoteVideo();
    };

    peer.onicecandidate = (event) => {
      if (event.candidate && callId) {
        void this.chat
          .sendIceCandidate(callId, event.candidate.candidate, event.candidate.sdpMid, event.candidate.sdpMLineIndex)
          .catch(() => undefined);
      }
    };

    // Sans serveur ICE joignable l'appel ne s'etablit pas : on remonte l'etat a l'utilisateur.
    peer.oniceconnectionstatechange = () => this.handleIceConnectionState(peer.iceConnectionState);
    peer.onconnectionstatechange = () => this.handleConnectionState(peer.connectionState);

    this.peer = peer;
    return peer;
  }

  private handleIceConnectionState(state: RTCIceConnectionState): void {
    if (state === 'failed') {
      this.onTransportFailure();
    }
  }

  private handleConnectionState(state: RTCPeerConnectionState): void {
    switch (state) {
      case 'connecting':
        if (this.phase() === 'active') {
          this.statusText.set('Connexion…');
        }
        break;
      case 'connected':
        if (this.phase() === 'active') {
          this.statusText.set('En communication');
        }
        break;
      case 'disconnected':
        if (this.phase() === 'active') {
          this.statusText.set('Connexion instable…');
        }
        break;
      case 'failed':
        this.onTransportFailure();
        break;
    }
  }

  /** La connexion media n'a pas pu s'etablir (ou est tombee) : on termine proprement l'appel. */
  private onTransportFailure(): void {
    if (this.phase() === 'idle' || this.phase() === 'ended') {
      return;
    }

    this.error.set(
      'La connexion a echoue. Verifiez votre reseau ; un serveur TURN est parfois necessaire derriere un NAT strict.',
    );

    const session = this.session();
    if (session?.callId) {
      void this.chat.endCall(session.callId).catch(() => undefined);
    }
    this.finish('Appel termine');
  }

  private addLocalTracks(): void {
    const peer = this.peer;
    const stream = this.localStream;
    if (!peer || !stream || this.tracksAdded) {
      return;
    }

    stream.getTracks().forEach((track) => {
      const sender = peer.addTrack(track, stream);
      if (track.kind === 'video') {
        this.videoSender = sender;
      }
    });
    this.tracksAdded = true;
  }

  private async getIceServers(): Promise<RTCIceServer[]> {
    if (this.iceServers) {
      return this.iceServers;
    }

    try {
      const servers = await firstValueFrom(
        this.http.get<IceServerDto[]>(`${environment.apiBaseUrl}/api/calls/ice-servers`),
      );
      this.iceServers = servers.map((server) => ({
        urls: server.urls,
        username: server.username ?? undefined,
        credential: server.credential ?? undefined,
      }));
    } catch {
      this.iceServers = [{ urls: 'stun:stun.l.google.com:19302' }];
    }

    return this.iceServers;
  }

  private finish(message: string): void {
    this.statusText.set(message);
    this.phase.set('ended');
    this.releaseMedia();

    this.clearEndedTimer();
    this.endedTimer = window.setTimeout(() => {
      if (this.phase() === 'ended') {
        this.phase.set('idle');
        this.statusText.set(null);
      }
    }, ENDED_MESSAGE_MS);
  }

  private fail(message: string): void {
    this.error.set(message);
    this.releaseMedia();

    if (this.phase() === 'incoming' || this.phase() === 'outgoing') {
      this.session.set(null);
      this.incoming.set(null);
      this.phase.set('idle');
      this.statusText.set(null);
    }
  }

  private releaseMedia(): void {
    this.clearEndedTimer();
    this.peer?.close();
    this.peer = null;
    this.localStream?.getTracks().forEach((track) => track.stop());
    this.screenStream?.getTracks().forEach((track) => track.stop());
    this.localStream = null;
    this.screenStream = null;
    this.cameraTrack = null;
    this.screenTrack = null;
    this.videoSender = null;
    this.tracksAdded = false;
    this.pendingRemoteCandidates = [];
    this.remoteMedia.getTracks().forEach((track) => this.remoteMedia.removeTrack(track));
    this.session.set(null);
    this.incoming.set(null);
    this.muted.set(false);
    this.cameraOn.set(false);
    this.screenSharing.set(false);
    this.remoteHasVideo.set(false);
    this.minimized.set(false);
    this.enlarged.set(false);
    this.localStreamSignal.set(null);
    this.remoteStream.set(null);
  }

  private clearEndedTimer(): void {
    if (this.endedTimer !== undefined) {
      clearTimeout(this.endedTimer);
      this.endedTimer = undefined;
    }
  }

  private endMessage(status: number): string {
    switch (status) {
      case CallStatus.Rejected:
        return 'Appel refuse';
      case CallStatus.Missed:
        return 'Appel manque';
      case CallStatus.Failed:
        return 'Appel echoue';
      default:
        return 'Appel termine';
    }
  }

  private messageFrom(error: unknown): string {
    if (error instanceof Error && error.message) {
      return error.message.replace(/^An unexpected error occurred[^:]*:\s*/, '');
    }
    return 'Une erreur est survenue pendant l\'appel.';
  }
}
