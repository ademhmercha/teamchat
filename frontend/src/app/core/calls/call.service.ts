import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ToastService } from '../../shared/ui/toast';
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
import { CallSounds } from './call-sounds';

export type CallPhase = 'idle' | 'outgoing' | 'incoming' | 'active' | 'ended';
export type CallQuality = 'good' | 'fair' | 'poor';

export interface CallDevices {
  readonly mics: MediaDeviceInfo[];
  readonly cameras: MediaDeviceInfo[];
  readonly speakers: MediaDeviceInfo[];
}

interface StatsEntry {
  type: string;
  kind?: string;
  state?: string;
  nominated?: boolean;
  currentRoundTripTime?: number;
  packetsLost?: number;
  packetsReceived?: number;
}

interface PeerState {
  mic: boolean;
  cam: boolean;
  screen: boolean;
}

class CallCancelled extends Error {}

const ENDED_MESSAGE_MS = 2500;
const DISCONNECT_GRACE_MS = 5000;
const RECOVERY_TIMEOUT_MS = 15000;
const MAX_ICE_RESTARTS = 2;
const STATS_INTERVAL_MS = 4000;

const AUDIO_CONSTRAINTS: MediaTrackConstraints = {
  echoCancellation: true,
  noiseSuppression: true,
  autoGainControl: true,
};

@Injectable({ providedIn: 'root' })
export class CallService {
  private readonly http = inject(HttpClient);
  private readonly chat = inject(ChatService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly sounds = inject(CallSounds);

  private peer: RTCPeerConnection | null = null;
  private peerPromise: Promise<RTCPeerConnection> | null = null;
  private stateChannel: RTCDataChannel | null = null;
  private localStream: MediaStream | null = null;
  private screenStream: MediaStream | null = null;
  private screenSender: RTCRtpSender | null = null;
  private cameraTrack: MediaStreamTrack | null = null;
  private screenTrack: MediaStreamTrack | null = null;
  private audioSender: RTCRtpSender | null = null;
  private videoSender: RTCRtpSender | null = null;
  private iceServers: RTCIceServer[] | null = null;
  private pendingRemoteCandidates: RTCIceCandidateInit[] = [];
  private tracksAdded = false;

  private attempt = 0;
  private accepted = false;
  private canNegotiate = false;
  private polite = false;
  private makingOffer = false;
  private ignoreOffer = false;
  private facing: 'user' | 'environment' = 'user';
  private iceRestarts = 0;
  private connectedAt: number | null = null;
  private previousLost = 0;
  private previousReceived = 0;
  private wakeLock: WakeLockSentinel | null = null;

  private endedTimer?: number;
  private graceTimer?: number;
  private recoveryTimer?: number;
  private tickTimer?: number;
  private statsTimer?: number;

  readonly phase = signal<CallPhase>('idle');
  readonly session = signal<CallSessionDto | null>(null);
  readonly incoming = signal<CallSessionDto | null>(null);
  readonly statusText = signal<string | null>(null);
  readonly muted = signal(false);
  readonly remoteMuted = signal(false);
  readonly cameraOn = signal(false);
  readonly screenSharing = signal(false);
  readonly localStreamSignal = signal<MediaStream | null>(null);
  readonly remoteStream = signal<MediaStream | null>(null);
  readonly remoteHasVideo = signal(false);
  readonly remoteScreenStream = signal<MediaStream | null>(null);
  readonly remoteHasScreen = signal(false);
  readonly quality = signal<CallQuality | null>(null);
  readonly elapsedSeconds = signal(0);
  readonly devices = signal<CallDevices>({ mics: [], cameras: [], speakers: [] });
  readonly selectedMicId = signal<string | null>(null);
  readonly selectedCameraId = signal<string | null>(null);
  readonly selectedSpeakerId = signal<string | null>(null);

  readonly minimized = signal(false);
  readonly enlarged = signal(false);

  readonly canScreenShare = typeof navigator !== 'undefined' && !!navigator.mediaDevices?.getDisplayMedia;
  readonly isTouch = typeof window !== 'undefined' && window.matchMedia?.('(pointer: coarse)').matches === true;

  readonly localHasVideo = computed(() => this.cameraOn());
  readonly showVideo = computed(
    () => this.remoteHasVideo() || this.remoteHasScreen() || this.cameraOn() || this.screenSharing(),
  );
  readonly canFlipCamera = computed(() => this.isTouch && this.devices().cameras.length > 1);
  readonly elapsedText = computed(() => {
    const total = this.elapsedSeconds();
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const seconds = total % 60;
    const mm = String(minutes).padStart(2, '0');
    const ss = String(seconds).padStart(2, '0');
    return hours > 0 ? `${hours}:${mm}:${ss}` : `${mm}:${ss}`;
  });

  private readonly remoteMedia = new MediaStream();
  private readonly remoteScreenMedia = new MediaStream();
  private readonly remoteVideoFlag = signal(false);
  private readonly remoteScreenFlag = signal(false);

  readonly visible = computed(() => this.phase() !== 'idle');

  get currentUserId(): string | null {
    return this.auth.user()?.id ?? null;
  }

  constructor() {
    this.chat.incomingCall$.subscribe((call) => this.onIncomingCall(call));
    this.chat.callAccepted$.subscribe((call) => void this.onCallAccepted(call));
    this.chat.callHandled$.subscribe((callId) => this.onCallHandled(callId));
    this.chat.callEnded$.subscribe((payload) => this.onCallEnded(payload.callId, payload.status));
    this.chat.receiveOffer$.subscribe((payload) => void this.onOffer(payload));
    this.chat.receiveAnswer$.subscribe((payload) => void this.onAnswer(payload));
    this.chat.receiveIceCandidate$.subscribe((payload) => void this.onRemoteCandidate(payload));

    if (typeof document !== 'undefined') {
      document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
          this.sounds.stopBlinkTitle();
          if (this.phase() === 'active') {
            void this.acquireWakeLock();
          }
        }
      });
    }

    if (typeof navigator !== 'undefined' && navigator.mediaDevices?.addEventListener) {
      navigator.mediaDevices.addEventListener('devicechange', () => {
        if (this.phase() === 'active') {
          void this.refreshDevices();
        }
      });
    }
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
    const attempt = ++this.attempt;
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
    this.polite = false;
    this.requestNotificationPermission();
    this.sounds.ringback();

    try {
      await this.prepareLocalMedia(type, attempt);
      const session = await this.chat.callUser(calleeId, type);
      if (attempt !== this.attempt) {
        void this.chat.endCall(session.callId).catch(() => undefined);
        return;
      }

      if (this.session()?.callId === '') {
        this.session.set(session);
      }
    } catch (error) {
      if (error instanceof CallCancelled || attempt !== this.attempt) {
        return;
      }
      this.fail(this.mediaErrorMessage(error, type === CallType.Video ? 'camera' : 'microphone'));
    }
  }

  async acceptIncoming(): Promise<void> {
    const call = this.incoming();
    if (!call || this.phase() !== 'incoming') {
      return;
    }

    const attempt = ++this.attempt;
    this.sounds.stop();
    this.sounds.stopBlinkTitle();
    this.requestNotificationPermission();
    this.session.set(call);
    this.polite = true;
    this.accepted = false;
    this.phase.set('active');
    this.statusText.set('Connexion…');

    try {
      await this.prepareLocalMedia(call.type, attempt);
      await this.ensurePeer();
      this.addLocalTracks();
      await this.chat.acceptCall(call.callId);
      if (attempt !== this.attempt) {
        return;
      }

      this.accepted = true;
      this.canNegotiate = true;
      this.incoming.set(null);
      void this.acquireWakeLock();
      void this.refreshDevices();
    } catch (error) {
      if (error instanceof CallCancelled || attempt !== this.attempt) {
        return;
      }
      this.fail(this.mediaErrorMessage(error, call.type === CallType.Video ? 'camera' : 'microphone'));
    }
  }

  async rejectIncoming(): Promise<void> {
    const call = this.incoming();
    this.attempt++;
    this.incoming.set(null);
    if (call) {
      await this.chat.rejectCall(call.callId).catch(() => undefined);
    }
    this.finish('Appel refusé');
  }

  async hangup(): Promise<void> {
    const session = this.session();
    this.attempt++;
    if (session?.callId) {
      await this.chat.endCall(session.callId).catch(() => undefined);
    }
    this.finish('Appel terminé');
  }

  toggleMute(): void {
    const stream = this.localStream;
    if (!stream) {
      return;
    }

    this.muted.update((value) => !value);
    const enabled = !this.muted();
    stream.getAudioTracks().forEach((track) => (track.enabled = enabled));
    this.sendState();
  }

  async toggleCamera(): Promise<void> {
    if (this.cameraOn()) {
      await this.stopCamera();
      return;
    }

    await this.startCamera();
  }

  async flipCamera(): Promise<void> {
    this.facing = this.facing === 'user' ? 'environment' : 'user';
    this.selectedCameraId.set(null);
    if (this.cameraOn()) {
      await this.startCamera();
    }
  }

  async selectCamera(deviceId: string): Promise<void> {
    this.selectedCameraId.set(deviceId);
    if (this.cameraOn()) {
      await this.startCamera();
    }
  }

  async selectMicrophone(deviceId: string): Promise<void> {
    this.selectedMicId.set(deviceId);
    const stream = this.localStream;
    if (!stream) {
      return;
    }

    try {
      const media = await navigator.mediaDevices.getUserMedia({
        audio: { ...AUDIO_CONSTRAINTS, deviceId: { exact: deviceId } },
      });
      const track = media.getAudioTracks()[0];
      if (!track) {
        return;
      }

      track.enabled = !this.muted();
      stream.getAudioTracks().forEach((existing) => {
        existing.stop();
        stream.removeTrack(existing);
      });
      stream.addTrack(track);
      this.watchLocalAudio(track);
      await this.audioSender?.replaceTrack(track);
      this.localStreamSignal.set(new MediaStream(stream.getTracks()));
    } catch (error) {
      this.toast.error(this.mediaErrorMessage(error, 'microphone'));
    }
  }

  selectSpeaker(deviceId: string): void {
    this.selectedSpeakerId.set(deviceId);
  }

  async toggleScreenShare(): Promise<void> {
    if (this.screenSharing()) {
      await this.stopScreenShare();
      return;
    }

    if (!navigator.mediaDevices?.getDisplayMedia) {
      this.toast.error('Le partage d\'écran n\'est pas disponible sur ce navigateur.');
      return;
    }

    try {
      const display = await navigator.mediaDevices.getDisplayMedia({ video: true });
      const track = display.getVideoTracks()[0] ?? null;
      if (!track) {
        return;
      }

      const peer = this.peer;
      if (!peer) {
        display.getTracks().forEach((item) => item.stop());
        this.toast.error("Le partage d'écran n'est possible qu'une fois la communication établie.");
        return;
      }

      this.screenStream = display;
      this.screenTrack = track;
      track.onended = () => void this.stopScreenShare();
      this.screenSender = peer.addTrack(track, display);
      this.screenSharing.set(true);
      this.sendState();
    } catch (error) {
      if (error instanceof DOMException && (error.name === 'NotAllowedError' || error.name === 'AbortError')) {
        return;
      }
      this.toast.error('Impossible de partager l\'écran.');
    }
  }

  toggleMinimize(): void {
    if (this.phase() !== 'active') {
      return;
    }

    this.minimized.update((value) => !value);
    if (this.minimized()) {
      this.enlarged.set(false);
    }
  }

  toggleEnlarge(): void {
    if (!this.remoteHasVideo() && !this.remoteHasScreen()) {
      return;
    }

    this.enlarged.update((value) => !value);
    if (this.enlarged()) {
      this.minimized.set(false);
    }
  }

  private async startCamera(): Promise<void> {
    try {
      const media = await navigator.mediaDevices.getUserMedia({ video: this.videoConstraints() });
      const track = media.getVideoTracks()[0] ?? null;
      if (!track) {
        return;
      }

      this.cameraTrack?.stop();
      this.cameraTrack = track;
      this.watchCamera(track);
      await this.applyVideoTrack(track);
      this.cameraOn.set(true);
      this.sendState();
      void this.refreshDevices();
    } catch (error) {
      this.toast.error(this.mediaErrorMessage(error, 'camera'));
    }
  }

  private async stopCamera(): Promise<void> {
    this.cameraTrack?.stop();
    this.cameraTrack = null;
    this.cameraOn.set(false);
    await this.replaceLocalVideoTrack(null);
    this.sendState();
  }

  private async stopScreenShare(): Promise<void> {
    if (!this.screenSharing()) {
      return;
    }

    this.screenTrack?.stop();
    const sender = this.screenSender;
    if (sender && this.peer && this.peer.signalingState !== 'closed') {
      try {
        this.peer.removeTrack(sender);
      } catch {
        void sender.replaceTrack(null).catch(() => undefined);
      }
    }
    this.screenSender = null;
    this.screenTrack = null;
    this.screenStream = null;
    this.screenSharing.set(false);
    this.sendState();
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
      await this.videoSender.replaceTrack(track).catch(() => undefined);
    }
  }

  private onIncomingCall(call: CallSessionDto): void {
    if (this.phase() !== 'idle') {
      void this.chat.rejectCall(call.callId).catch(() => undefined);
      return;
    }

    this.clearEndedTimer();
    this.statusText.set(null);
    this.incoming.set(call);
    this.phase.set('incoming');
    this.sounds.ringtone();

    if (document.hidden) {
      this.sounds.blinkTitle(`📞 ${call.callerDisplayName}`);
      this.notifyIncoming(call);
    }
  }

  private onCallHandled(callId: string): void {
    if (this.phase() !== 'incoming' || this.incoming()?.callId !== callId) {
      return;
    }

    this.sounds.stop();
    this.sounds.stopBlinkTitle();
    this.incoming.set(null);
    this.phase.set('idle');
    this.statusText.set(null);
    this.toast.info('Appel pris sur un autre appareil.');
  }

  private async onCallAccepted(call: CallSessionDto): Promise<void> {
    const session = this.session();
    if (!session || this.phase() !== 'outgoing' || (session.callId !== '' && session.callId !== call.callId)) {
      return;
    }

    this.sounds.stop();
    this.session.set(call);
    this.phase.set('active');
    this.statusText.set('Connexion…');
    this.accepted = true;
    this.canNegotiate = true;

    try {
      await this.ensurePeer();
      this.addLocalTracks();
      void this.acquireWakeLock();
      void this.refreshDevices();
    } catch (error) {
      if (!(error instanceof CallCancelled)) {
        this.fail(this.messageFrom(error));
      }
    }
  }

  private async negotiate(peer: RTCPeerConnection): Promise<void> {
    const session = this.session();
    if (!this.canNegotiate || !session?.callId || peer !== this.peer) {
      return;
    }

    try {
      this.makingOffer = true;
      await peer.setLocalDescription();
      const sdp = peer.localDescription?.sdp;
      if (sdp) {
        await this.chat.sendOffer(session.callId, sdp);
      }
    } catch {
      await peer.setLocalDescription({ type: 'rollback' }).catch(() => undefined);
    } finally {
      this.makingOffer = false;
    }
  }

  private async onOffer(payload: SdpPayload): Promise<void> {
    const session = this.session();
    if (!session || payload.callId !== session.callId) {
      return;
    }

    try {
      const peer = await this.ensurePeer();
      const collision = this.makingOffer || peer.signalingState !== 'stable';
      this.ignoreOffer = !this.polite && collision;
      if (this.ignoreOffer) {
        return;
      }

      this.addLocalTracks();
      await peer.setRemoteDescription({ type: 'offer', sdp: payload.sdp });
      await this.flushRemoteCandidates();
      await peer.setLocalDescription();
      const sdp = peer.localDescription?.sdp;
      if (sdp) {
        await this.chat.sendAnswer(payload.callId, sdp);
      }
    } catch (error) {
      if (!(error instanceof CallCancelled)) {
        this.fail(this.messageFrom(error));
      }
    }
  }

  private async onAnswer(payload: SdpPayload): Promise<void> {
    const session = this.session();
    const peer = this.peer;
    if (!session || !peer || payload.callId !== session.callId || peer.signalingState !== 'have-local-offer') {
      return;
    }

    try {
      await peer.setRemoteDescription({ type: 'answer', sdp: payload.sdp });
      await this.flushRemoteCandidates();
    } catch (error) {
      this.fail(this.messageFrom(error));
    }
  }

  private async onRemoteCandidate(payload: IceCandidatePayload): Promise<void> {
    const session = this.session();
    if (!session || (session.callId !== '' && payload.callId !== session.callId)) {
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

    await this.peer.addIceCandidate(candidate).catch(() => {
      if (!this.ignoreOffer) {
        this.pendingRemoteCandidates = [];
      }
    });
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

  private refreshRemoteVideo(): void {
    const hasLiveVideo = this.remoteMedia.getVideoTracks().some((track) => track.readyState !== 'ended');
    const visible = hasLiveVideo && this.remoteVideoFlag();
    this.remoteHasVideo.set(visible);

    const hasLiveScreen = this.remoteScreenMedia.getVideoTracks().some((track) => track.readyState !== 'ended');
    const screenVisible = hasLiveScreen && this.remoteScreenFlag();
    this.remoteHasScreen.set(screenVisible);
    this.remoteScreenStream.set(hasLiveScreen ? this.remoteScreenMedia : null);

    if (!visible && !screenVisible) {
      this.enlarged.set(false);
    }
  }

  private onCallEnded(callId: string, status: number): void {
    const session = this.session();
    const incoming = this.incoming();
    if (session?.callId !== callId && incoming?.callId !== callId) {
      return;
    }

    this.attempt++;
    this.finish(this.endMessage(status, session?.callerId === this.currentUserId));
  }

  private async prepareLocalMedia(type: number, attempt: number): Promise<void> {
    if (this.localStream) {
      return;
    }

    if (!navigator.mediaDevices?.getUserMedia) {
      throw new Error('Ce navigateur ne prend pas en charge les appels.');
    }

    const wantsVideo = type === CallType.Video;
    const audio = this.audioConstraints();
    let stream: MediaStream;

    try {
      stream = await navigator.mediaDevices.getUserMedia({
        audio,
        video: wantsVideo ? this.videoConstraints() : false,
      });
    } catch (error) {
      if (!wantsVideo) {
        throw error;
      }

      stream = await navigator.mediaDevices.getUserMedia({ audio });
      this.toast.info('Caméra indisponible : l\'appel démarre en audio seul.');
    }

    if (attempt !== this.attempt) {
      stream.getTracks().forEach((track) => track.stop());
      throw new CallCancelled();
    }

    this.localStream = stream;
    this.cameraTrack = stream.getVideoTracks()[0] ?? null;
    this.cameraOn.set(wantsVideo && !!this.cameraTrack);
    if (this.cameraTrack) {
      this.watchCamera(this.cameraTrack);
    }
    const audioTrack = stream.getAudioTracks()[0];
    if (audioTrack) {
      this.watchLocalAudio(audioTrack);
    }
    this.localStreamSignal.set(new MediaStream(stream.getTracks()));
  }

  private watchCamera(track: MediaStreamTrack): void {
    track.addEventListener('ended', () => {
      if (this.cameraTrack === track) {
        this.toast.error('La caméra a été déconnectée.');
        void this.stopCamera();
      }
    });
  }

  private watchLocalAudio(track: MediaStreamTrack): void {
    track.addEventListener('ended', () => {
      if (this.localStream?.getAudioTracks().includes(track)) {
        this.toast.error('Le microphone a été déconnecté.');
        this.muted.set(true);
        this.sendState();
      }
    });
  }

  private audioConstraints(): MediaTrackConstraints {
    const id = this.selectedMicId();
    return id ? { ...AUDIO_CONSTRAINTS, deviceId: { exact: id } } : AUDIO_CONSTRAINTS;
  }

  private videoConstraints(): MediaTrackConstraints {
    const base: MediaTrackConstraints = { width: { ideal: 1280 }, height: { ideal: 720 }, frameRate: { ideal: 30 } };
    const id = this.selectedCameraId();
    return id ? { ...base, deviceId: { exact: id } } : { ...base, facingMode: this.facing };
  }

  private async refreshDevices(): Promise<void> {
    if (!navigator.mediaDevices?.enumerateDevices) {
      return;
    }

    try {
      const all = await navigator.mediaDevices.enumerateDevices();
      this.devices.set({
        mics: all.filter((device) => device.kind === 'audioinput'),
        cameras: all.filter((device) => device.kind === 'videoinput'),
        speakers: all.filter((device) => device.kind === 'audiooutput'),
      });
    } catch {
      this.devices.set({ mics: [], cameras: [], speakers: [] });
    }
  }

  private mediaErrorMessage(error: unknown, kind: 'microphone' | 'camera'): string {
    if (error instanceof DOMException) {
      switch (error.name) {
        case 'NotAllowedError':
        case 'SecurityError':
          return `Accès ${kind === 'microphone' ? 'au micro' : 'à la caméra'} refusé. Autorisez-le dans les réglages du navigateur.`;
        case 'NotFoundError':
        case 'DevicesNotFoundError':
          return kind === 'microphone' ? 'Aucun microphone détecté.' : 'Aucune caméra détectée.';
        case 'NotReadableError':
        case 'TrackStartError':
          return 'Le périphérique est déjà utilisé par une autre application.';
        case 'OverconstrainedError':
          return 'Le périphérique ne correspond pas aux paramètres demandés.';
      }
    }

    return this.messageFrom(error);
  }

  private ensurePeer(): Promise<RTCPeerConnection> {
    if (!this.peerPromise) {
      const created = this.createPeer();
      this.peerPromise = created;
      created.catch(() => {
        if (this.peerPromise === created) {
          this.peerPromise = null;
        }
      });
    }

    return this.peerPromise;
  }

  private async createPeer(): Promise<RTCPeerConnection> {
    const attempt = this.attempt;
    const servers = await this.getIceServers();
    if (attempt !== this.attempt) {
      throw new CallCancelled();
    }

    const peer = new RTCPeerConnection({ iceServers: servers });
    this.peer = peer;
    this.iceRestarts = 0;

    const channel = peer.createDataChannel('state', { negotiated: true, id: 0 });
    channel.onopen = () => this.sendState();
    channel.onmessage = (event) => this.onPeerState(event.data);
    this.stateChannel = channel;

    peer.ontrack = (event) => {
      const track = event.track;
      const isScreen = track.kind === 'video' && (event.streams[0]?.getAudioTracks().length ?? 0) === 0;
      const target = isScreen ? this.remoteScreenMedia : this.remoteMedia;
      if (!target.getTracks().includes(track)) {
        target.addTrack(track);
      }

      if (track.kind === 'video') {
        track.onunmute = () => {
          if (!isScreen) {
            this.remoteVideoFlag.set(true);
          }
          this.refreshRemoteVideo();
        };
        track.onended = () => {
          target.removeTrack(track);
          this.refreshRemoteVideo();
        };
        track.onmute = () => {
          if (isScreen) {
            target.removeTrack(track);
          }
          this.refreshRemoteVideo();
        };
      }

      this.remoteStream.set(this.remoteMedia);
      this.refreshRemoteVideo();
    };

    peer.onicecandidate = (event) => {
      const callId = this.session()?.callId;
      if (event.candidate && callId) {
        void this.chat
          .sendIceCandidate(callId, event.candidate.candidate, event.candidate.sdpMid, event.candidate.sdpMLineIndex)
          .catch(() => undefined);
      }
    };

    peer.onnegotiationneeded = () => void this.negotiate(peer);
    peer.onconnectionstatechange = () => this.handleConnectionState(peer);

    return peer;
  }

  private sendState(): void {
    const channel = this.stateChannel;
    if (!channel || channel.readyState !== 'open') {
      return;
    }

    const state: PeerState = { mic: !this.muted(), cam: this.cameraOn(), screen: this.screenSharing() };
    channel.send(JSON.stringify(state));
  }

  private onPeerState(data: unknown): void {
    try {
      const state = JSON.parse(String(data)) as Partial<PeerState>;
      this.remoteMuted.set(state.mic === false);
      this.remoteVideoFlag.set(state.cam === true);
      this.remoteScreenFlag.set(state.screen === true);
      this.refreshRemoteVideo();
    } catch {
      return;
    }
  }

  private handleConnectionState(peer: RTCPeerConnection): void {
    if (peer !== this.peer || (this.phase() !== 'active' && this.phase() !== 'outgoing')) {
      return;
    }

    switch (peer.connectionState) {
      case 'connecting':
        if (this.connectedAt === null) {
          this.statusText.set('Connexion…');
        }
        break;
      case 'connected':
        this.clearRecoveryTimers();
        this.iceRestarts = 0;
        this.statusText.set('En communication');
        this.markConnected(peer);
        break;
      case 'disconnected':
        this.statusText.set('Reconnexion…');
        window.clearTimeout(this.graceTimer);
        this.graceTimer = window.setTimeout(() => this.tryRecover(peer), DISCONNECT_GRACE_MS);
        break;
      case 'failed':
        this.tryRecover(peer);
        break;
    }
  }

  private clearRecoveryTimers(): void {
    window.clearTimeout(this.graceTimer);
    window.clearTimeout(this.recoveryTimer);
    this.graceTimer = undefined;
    this.recoveryTimer = undefined;
  }

  private tryRecover(peer: RTCPeerConnection): void {
    if (peer !== this.peer || peer.connectionState === 'connected') {
      return;
    }

    if (this.iceRestarts >= MAX_ICE_RESTARTS) {
      this.onTransportFailure();
      return;
    }

    this.iceRestarts++;
    this.statusText.set('Reconnexion…');
    peer.restartIce();
    window.clearTimeout(this.recoveryTimer);
    this.recoveryTimer = window.setTimeout(() => this.tryRecover(peer), RECOVERY_TIMEOUT_MS / MAX_ICE_RESTARTS);
  }

  private markConnected(peer: RTCPeerConnection): void {
    if (this.connectedAt === null) {
      this.connectedAt = Date.now();
      this.tickTimer = window.setInterval(() => {
        if (this.connectedAt !== null) {
          this.elapsedSeconds.set(Math.floor((Date.now() - this.connectedAt) / 1000));
        }
      }, 1000);
      this.statsTimer = window.setInterval(() => void this.sampleQuality(peer), STATS_INTERVAL_MS);
    }
    this.sendState();
  }

  private async sampleQuality(peer: RTCPeerConnection): Promise<void> {
    if (peer !== this.peer) {
      return;
    }

    try {
      const report = await peer.getStats();
      let rtt: number | null = null;
      let lost = 0;
      let received = 0;
      report.forEach((entry: StatsEntry) => {
        if (entry.type === 'candidate-pair' && entry.state === 'succeeded' && entry.nominated) {
          rtt = entry.currentRoundTripTime ?? rtt;
        }
        if (entry.type === 'inbound-rtp' && entry.kind === 'audio') {
          lost += entry.packetsLost ?? 0;
          received += entry.packetsReceived ?? 0;
        }
      });

      const deltaLost = Math.max(0, lost - this.previousLost);
      const deltaReceived = Math.max(0, received - this.previousReceived);
      this.previousLost = lost;
      this.previousReceived = received;
      const total = deltaLost + deltaReceived;
      const loss = total > 0 ? deltaLost / total : 0;
      const delay = rtt ?? 0;

      if (delay > 0.4 || loss > 0.1) {
        this.quality.set('poor');
      } else if (delay > 0.2 || loss > 0.03) {
        this.quality.set('fair');
      } else {
        this.quality.set('good');
      }
    } catch {
      this.quality.set(null);
    }
  }

  private onTransportFailure(): void {
    if (this.phase() === 'idle' || this.phase() === 'ended') {
      return;
    }

    this.toast.error(
      'La connexion a échoué. Vérifiez votre réseau ; un serveur TURN est parfois nécessaire derrière un NAT strict.',
    );

    const session = this.session();
    this.attempt++;
    if (session?.callId) {
      void this.chat.endCall(session.callId).catch(() => undefined);
    }
    this.finish('Appel terminé');
  }

  private addLocalTracks(): void {
    const peer = this.peer;
    const stream = this.localStream;
    if (!peer || !stream || this.tracksAdded) {
      return;
    }

    this.tracksAdded = true;
    stream.getTracks().forEach((track) => {
      const sender = peer.addTrack(track, stream);
      if (track.kind === 'video') {
        this.videoSender = sender;
      } else {
        this.audioSender = sender;
      }
    });
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
      return [{ urls: 'stun:stun.l.google.com:19302' }];
    }

    return this.iceServers;
  }

  private async acquireWakeLock(): Promise<void> {
    if (this.wakeLock && !this.wakeLock.released) {
      return;
    }

    try {
      this.wakeLock = (await navigator.wakeLock?.request('screen')) ?? null;
    } catch {
      this.wakeLock = null;
    }
  }

  private requestNotificationPermission(): void {
    if (typeof Notification !== 'undefined' && Notification.permission === 'default') {
      void Notification.requestPermission().catch(() => undefined);
    }
  }

  private notifyIncoming(call: CallSessionDto): void {
    if (typeof Notification === 'undefined' || Notification.permission !== 'granted') {
      return;
    }

    try {
      const notification = new Notification('Appel entrant', {
        body: `${call.callerDisplayName} · appel ${call.type === CallType.Video ? 'vidéo' : 'audio'}`,
        tag: 'teamchat-call',
        requireInteraction: true,
      });
      notification.onclick = () => {
        window.focus();
        notification.close();
      };
    } catch {
      return;
    }
  }

  private finish(message: string): void {
    this.sounds.stop();
    this.sounds.stopBlinkTitle();
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
    this.toast.error(message);

    const session = this.session();
    const incoming = this.incoming();
    const isCaller = session?.callerId === this.currentUserId;
    this.attempt++;
    if (incoming && this.phase() === 'incoming') {
      void this.chat.rejectCall(incoming.callId).catch(() => undefined);
    } else if (session?.callId) {
      const request = !isCaller && !this.accepted ? this.chat.rejectCall(session.callId) : this.chat.endCall(session.callId);
      void request.catch(() => undefined);
    }

    this.sounds.stop();
    this.sounds.stopBlinkTitle();
    this.releaseMedia();
    this.phase.set('idle');
    this.statusText.set(null);
  }

  private releaseMedia(): void {
    this.clearEndedTimer();
    this.clearRecoveryTimers();
    window.clearInterval(this.tickTimer);
    window.clearInterval(this.statsTimer);
    this.tickTimer = undefined;
    this.statsTimer = undefined;
    void this.wakeLock?.release().catch(() => undefined);
    this.wakeLock = null;

    this.stateChannel?.close();
    this.stateChannel = null;
    this.peer?.close();
    this.peer = null;
    this.peerPromise = null;
    this.localStream?.getTracks().forEach((track) => track.stop());
    this.screenStream?.getTracks().forEach((track) => track.stop());
    this.localStream = null;
    this.screenStream = null;
    this.cameraTrack = null;
    this.screenTrack = null;
    this.screenSender = null;
    this.audioSender = null;
    this.videoSender = null;
    this.tracksAdded = false;
    this.accepted = false;
    this.canNegotiate = false;
    this.makingOffer = false;
    this.ignoreOffer = false;
    this.facing = 'user';
    this.iceRestarts = 0;
    this.connectedAt = null;
    this.previousLost = 0;
    this.previousReceived = 0;
    this.pendingRemoteCandidates = [];
    this.remoteMedia.getTracks().forEach((track) => this.remoteMedia.removeTrack(track));
    this.session.set(null);
    this.incoming.set(null);
    this.muted.set(false);
    this.remoteMuted.set(false);
    this.cameraOn.set(false);
    this.screenSharing.set(false);
    this.remoteScreenMedia.getTracks().forEach((track) => this.remoteScreenMedia.removeTrack(track));
    this.remoteVideoFlag.set(false);
    this.remoteScreenFlag.set(false);
    this.remoteHasVideo.set(false);
    this.remoteHasScreen.set(false);
    this.remoteScreenStream.set(null);
    this.quality.set(null);
    this.elapsedSeconds.set(0);
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

  private endMessage(status: number, isCaller: boolean): string {
    switch (status) {
      case CallStatus.Rejected:
        return 'Appel refusé';
      case CallStatus.Missed:
        return isCaller ? 'Pas de réponse' : 'Appel manqué';
      case CallStatus.Failed:
        return 'Appel échoué';
      default:
        return 'Appel terminé';
    }
  }

  private messageFrom(error: unknown): string {
    if (error instanceof Error && error.message) {
      return error.message.replace(/^An unexpected error occurred[^:]*:\s*/, '');
    }
    return 'Une erreur est survenue pendant l\'appel.';
  }
}
