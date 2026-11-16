import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { vi } from 'vitest';
import { ToastService } from '../../shared/ui/toast';
import { AuthService } from '../auth/auth.service';
import { ChatService } from '../chat/chat.service';
import { CallSessionDto, CallStatus, CallType } from '../models/call.models';
import { CallSounds } from './call-sounds';
import { CallService } from './call.service';

const SESSION: CallSessionDto = {
  callId: 'call-1',
  callerId: 'me',
  callerDisplayName: 'Moi',
  calleeId: 'bob',
  calleeDisplayName: 'Bob',
  callerAvatarUrl: null,
  calleeAvatarUrl: null,
  type: CallType.Audio,
  status: CallStatus.Ringing,
  conversationId: 'conv',
  startedAt: new Date().toISOString(),
};

function makeStream(): MediaStream {
  const track = { kind: 'audio', enabled: true, stop: vi.fn(), addEventListener: vi.fn() } as unknown as MediaStreamTrack;
  return {
    getTracks: () => [track],
    getAudioTracks: () => [track],
    getVideoTracks: () => [],
    addTrack: vi.fn(),
    removeTrack: vi.fn(),
  } as unknown as MediaStream;
}

class FakeMediaStream {
  private tracks: MediaStreamTrack[];

  constructor(tracks: MediaStreamTrack[] = []) {
    this.tracks = [...tracks];
  }

  getTracks(): MediaStreamTrack[] {
    return this.tracks;
  }

  getAudioTracks(): MediaStreamTrack[] {
    return this.tracks.filter((track) => track.kind === 'audio');
  }

  getVideoTracks(): MediaStreamTrack[] {
    return this.tracks.filter((track) => track.kind === 'video');
  }

  addTrack(track: MediaStreamTrack): void {
    this.tracks.push(track);
  }

  removeTrack(track: MediaStreamTrack): void {
    this.tracks = this.tracks.filter((item) => item !== track);
  }
}

class FakePeer {
  signalingState = 'stable';
  connectionState = 'new';
  remoteDescription = null;
  readonly senders: { track: MediaStreamTrack | null; replaceTrack: ReturnType<typeof vi.fn> }[] = [];
  ontrack: unknown;
  onicecandidate: unknown;
  onnegotiationneeded: unknown;
  onconnectionstatechange: unknown;
  addTrack = vi.fn((track: MediaStreamTrack) => {
    const sender = { track, replaceTrack: vi.fn().mockResolvedValue(undefined) };
    this.senders.push(sender);
    return sender;
  });
  removeTrack = vi.fn();
  createDataChannel = vi.fn(() => ({ readyState: 'closed', send: vi.fn(), close: vi.fn() }));
  close = vi.fn();
}

function track(kind: string): MediaStreamTrack {
  return { kind, enabled: true, readyState: 'live', stop: vi.fn(), addEventListener: vi.fn() } as unknown as MediaStreamTrack;
}

describe('CallService', () => {
  let chat: {
    incomingCall$: Subject<CallSessionDto>;
    callAccepted$: Subject<CallSessionDto>;
    callHandled$: Subject<string>;
    callEnded$: Subject<{ callId: string; status: number }>;
    receiveOffer$: Subject<unknown>;
    receiveAnswer$: Subject<unknown>;
    receiveIceCandidate$: Subject<unknown>;
    callUser: ReturnType<typeof vi.fn>;
    acceptCall: ReturnType<typeof vi.fn>;
    rejectCall: ReturnType<typeof vi.fn>;
    endCall: ReturnType<typeof vi.fn>;
  };
  let toast: ToastService;
  let service: CallService;
  let getUserMedia: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.stubGlobal('MediaStream', FakeMediaStream);
    chat = {
      incomingCall$: new Subject(),
      callAccepted$: new Subject(),
      callHandled$: new Subject(),
      callEnded$: new Subject(),
      receiveOffer$: new Subject(),
      receiveAnswer$: new Subject(),
      receiveIceCandidate$: new Subject(),
      callUser: vi.fn().mockResolvedValue(SESSION),
      acceptCall: vi.fn().mockResolvedValue(undefined),
      rejectCall: vi.fn().mockResolvedValue(undefined),
      endCall: vi.fn().mockResolvedValue(undefined),
    };

    getUserMedia = vi.fn().mockResolvedValue(makeStream());
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia, enumerateDevices: vi.fn().mockResolvedValue([]), addEventListener: vi.fn() },
    });

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ChatService, useValue: chat },
        { provide: AuthService, useValue: { user: () => ({ id: 'me', displayName: 'Moi', avatarUrl: null }) } },
        {
          provide: CallSounds,
          useValue: {
            ringtone: vi.fn(),
            ringback: vi.fn(),
            stop: vi.fn(),
            blinkTitle: vi.fn(),
            stopBlinkTitle: vi.fn(),
          },
        },
      ],
    });
    toast = TestBed.inject(ToastService);
    service = TestBed.inject(CallService);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    TestBed.inject(HttpTestingController).verify();
  });

  it('affiche une erreur visible quand le micro est refusé et revient au repos', async () => {
    getUserMedia.mockRejectedValue(new DOMException('denied', 'NotAllowedError'));

    await service.startCall('bob', 'Bob', CallType.Audio);

    expect(service.phase()).toBe('idle');
    expect(toast.items().some((item) => item.variant === 'error' && item.message.includes('micro'))).toBe(true);
    expect(chat.callUser).not.toHaveBeenCalled();
  });

  it('termine côté serveur un appel créé alors que l\'utilisateur a déjà raccroché', async () => {
    let resolveCall!: (session: CallSessionDto) => void;
    chat.callUser.mockReturnValue(new Promise<CallSessionDto>((resolve) => (resolveCall = resolve)));

    const starting = service.startCall('bob', 'Bob', CallType.Audio);
    await vi.waitFor(() => expect(chat.callUser).toHaveBeenCalled());

    await service.hangup();
    resolveCall(SESSION);
    await starting;

    expect(chat.endCall).toHaveBeenCalledWith('call-1');
    expect(service.session()).toBeNull();
  });

  it('libère le micro obtenu après un raccroché pendant la demande d\'autorisation', async () => {
    const stream = makeStream();
    let resolveMedia!: (value: MediaStream) => void;
    getUserMedia.mockReturnValue(new Promise<MediaStream>((resolve) => (resolveMedia = resolve)));

    const starting = service.startCall('bob', 'Bob', CallType.Audio);
    await service.hangup();
    resolveMedia(stream);
    await starting;

    expect(stream.getTracks()[0].stop).toHaveBeenCalled();
    expect(chat.callUser).not.toHaveBeenCalled();
  });

  it('refuse côté serveur quand l\'acceptation échoue (micro indisponible)', async () => {
    chat.incomingCall$.next({ ...SESSION, callerId: 'bob', calleeId: 'me' });
    expect(service.phase()).toBe('incoming');

    getUserMedia.mockRejectedValue(new DOMException('nope', 'NotFoundError'));
    await service.acceptIncoming();

    expect(service.phase()).toBe('idle');
    expect(chat.rejectCall).toHaveBeenCalledWith('call-1');
    expect(toast.items().some((item) => item.message.includes('microphone'))).toBe(true);
  });

  it('ignore un appel déjà pris sur un autre appareil', () => {
    chat.incomingCall$.next({ ...SESSION, callerId: 'bob', calleeId: 'me' });
    chat.callHandled$.next('call-1');

    expect(service.phase()).toBe('idle');
    expect(service.incoming()).toBeNull();
  });

  it('rejette un appel entrant quand on est déjà en communication', async () => {
    await service.startCall('bob', 'Bob', CallType.Audio);
    chat.incomingCall$.next({ ...SESSION, callId: 'call-2', callerId: 'carol', calleeId: 'me' });

    expect(chat.rejectCall).toHaveBeenCalledWith('call-2');
  });

  it('affiche « Pas de réponse » côté appelant quand l\'appel est manqué', async () => {
    await service.startCall('bob', 'Bob', CallType.Audio);
    chat.callEnded$.next({ callId: 'call-1', status: CallStatus.Missed });

    expect(service.phase()).toBe('ended');
    expect(service.statusText()).toBe('Pas de réponse');
  });

  it('formate le chronomètre', () => {
    service.elapsedSeconds.set(75);
    expect(service.elapsedText()).toBe('01:15');
    service.elapsedSeconds.set(3725);
    expect(service.elapsedText()).toBe('1:02:05');
  });

  it("permet de partager l'écran sans couper la caméra, avec deux envois vidéo distincts", async () => {
    const peer = new FakePeer();
    vi.stubGlobal('RTCPeerConnection', function () {
      return peer;
    });
    const cameraTrack = track('video');
    const screenTrack = track('video');
    const display = { getTracks: () => [screenTrack], getVideoTracks: () => [screenTrack] };
    Object.defineProperty(navigator.mediaDevices, 'getDisplayMedia', {
      configurable: true,
      value: vi.fn().mockResolvedValue(display),
    });

    await service.startCall('bob', 'Bob', CallType.Audio);
    chat.callAccepted$.next({ ...SESSION, status: CallStatus.Accepted });
    await vi.waitFor(() => TestBed.inject(HttpTestingController).expectOne((r) => r.url.endsWith('/api/calls/ice-servers')).flush([]));
    await vi.waitFor(() => expect(peer.addTrack).toHaveBeenCalled());

    getUserMedia.mockResolvedValueOnce(new FakeMediaStream([cameraTrack]));
    await service.toggleCamera();
    await service.toggleScreenShare();

    expect(service.cameraOn()).toBe(true);
    expect(service.screenSharing()).toBe(true);
    expect(peer.senders.map((sender) => sender.track)).toEqual(expect.arrayContaining([cameraTrack, screenTrack]));

    await service.toggleCamera();
    expect(service.cameraOn()).toBe(false);
    expect(service.screenSharing()).toBe(true);

    await service.toggleScreenShare();
    expect(service.screenSharing()).toBe(false);
    expect(peer.removeTrack).toHaveBeenCalledTimes(1);
    expect(screenTrack.stop).toHaveBeenCalled();
  });
});
