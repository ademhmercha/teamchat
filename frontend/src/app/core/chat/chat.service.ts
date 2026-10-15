import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import {
  CallEndedPayload,
  CallSessionDto,
  IceCandidatePayload,
  SdpPayload,
} from '../models/call.models';
import { MessageDto } from '../models/conversation.models';

type TypingKey = string;

/** Durée pendant laquelle un indicateur « écrit… » reste affiche sans renouvellement. */
const TYPING_TTL_MS = 2500;
/** Intervalle minimal entre deux StartTyping vers le serveur. */
const TYPING_THROTTLE_MS = 1500;

/**
 * Service central temps reel (SignalR, /hubs/chat).
 * Le serveur rejoins les groupes a chaque (re)connexion : le resubscribe est
 * donc automatique apres reconnexion. La presence et le typing sont exposes
 * sous forme de signals consommables par la liste et le fil.
 */
@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly auth = inject(AuthService);

  private connection: HubConnection | null = null;
  private readonly presenceMap = new Map<string, boolean>();
  private readonly typingTimers = new Map<TypingKey, number>();

  readonly state = signal<HubConnectionState | 'Reconnecting'>(HubConnectionState.Disconnected);
  readonly presence = signal<Readonly<Record<string, boolean>>>({});
  readonly typing = signal<Readonly<Record<string, string>>>({});

  private readonly _receiveMessage = new Subject<MessageDto>();
  readonly receiveMessage$ = this._receiveMessage.asObservable();

  private readonly _messageEdited = new Subject<MessageDto>();
  readonly messageEdited$ = this._messageEdited.asObservable();

  private readonly _messageDeleted = new Subject<{ conversationId: string; messageId: string }>();
  readonly messageDeleted$ = this._messageDeleted.asObservable();

  private readonly _addedToGroup = new Subject<string>();
  readonly addedToGroup$ = this._addedToGroup.asObservable();

  private readonly _conversationUpdated = new Subject<string>();
  readonly conversationUpdated$ = this._conversationUpdated.asObservable();

  private readonly _incomingCall = new Subject<CallSessionDto>();
  readonly incomingCall$ = this._incomingCall.asObservable();

  private readonly _callAccepted = new Subject<CallSessionDto>();
  readonly callAccepted$ = this._callAccepted.asObservable();

  private readonly _callEnded = new Subject<CallEndedPayload>();
  readonly callEnded$ = this._callEnded.asObservable();

  private readonly _receiveOffer = new Subject<SdpPayload>();
  readonly receiveOffer$ = this._receiveOffer.asObservable();

  private readonly _receiveAnswer = new Subject<SdpPayload>();
  readonly receiveAnswer$ = this._receiveAnswer.asObservable();

  private readonly _receiveIceCandidate = new Subject<IceCandidatePayload>();
  readonly receiveIceCandidate$ = this._receiveIceCandidate.asObservable();

  private readonly _notificationsUpdated = new Subject<void>();
  readonly notificationsUpdated$ = this._notificationsUpdated.asObservable();

  private readonly _forceLogout = new Subject<void>();
  readonly forceLogout$ = this._forceLogout.asObservable();

  isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  start(): void {
    if (this.connection) {
      return;
    }

    const hub = new HubConnectionBuilder()
      .withUrl(`${environment.hubBaseUrl}/hubs/chat`, {
        accessTokenFactory: () => this.auth.accessToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection = hub;

    hub.onreconnecting(() => this.state.set('Reconnecting'));
    hub.onreconnected(() => this.state.set(HubConnectionState.Connected));
    hub.onclose(() => this.state.set(HubConnectionState.Disconnected));

    hub.on('ReceiveMessage', (message: MessageDto) => this._receiveMessage.next(message));
    hub.on('MessageEdited', (message: MessageDto) => this._messageEdited.next(message));
    hub.on('MessageDeleted', (conversationId: string, messageId: string) =>
      this._messageDeleted.next({ conversationId, messageId }),
    );
    hub.on('AddedToGroup', (conversationId: string) => this._addedToGroup.next(conversationId));
    hub.on('ConversationUpdated', (conversationId: string) => this._conversationUpdated.next(conversationId));
    hub.on('PresenceSnapshot', (onlineIds: string[]) => this.applySnapshot(onlineIds));
    hub.on('PresenceChanged', (userId: string, isOnline: boolean) => {
      if (isOnline) {
        this.presenceMap.set(userId, true);
      } else {
        this.presenceMap.delete(userId);
      }
      this.publishPresence();
    });
    hub.on('UserTyping', (conversationId: string, userId: string, displayName: string) =>
      this.signalTyping(conversationId, userId, displayName),
    );
    hub.on('UserStoppedTyping', (conversationId: string, userId: string) =>
      this.clearTyping(conversationId, userId),
    );
    hub.on('IncomingCall', (call: CallSessionDto) => this._incomingCall.next(call));
    hub.on('CallAccepted', (call: CallSessionDto) => this._callAccepted.next(call));
    hub.on('CallEnded', (callId: string, status: number) => this._callEnded.next({ callId, status }));
    hub.on('ReceiveOffer', (callId: string, sdp: string) => this._receiveOffer.next({ callId, sdp }));
    hub.on('ReceiveAnswer', (callId: string, sdp: string) => this._receiveAnswer.next({ callId, sdp }));
    hub.on(
      'ReceiveIceCandidate',
      (callId: string, candidate: string, sdpMid: string | null, sdpMLineIndex: number | null) =>
        this._receiveIceCandidate.next({ callId, candidate, sdpMid, sdpMLineIndex }),
    );
    hub.on('NotificationsUpdated', () => this._notificationsUpdated.next());
    hub.on('ForceLogout', () => this._forceLogout.next());

    this.state.set(HubConnectionState.Connecting);
    void hub.start().then(
      () => this.state.set(HubConnectionState.Connected),
      () => {
        if (hub.state === HubConnectionState.Disconnected) {
          this.state.set(HubConnectionState.Disconnected);
        }
      },
    );
  }

  stop(): void {
    this.typingTimers.forEach((timer) => clearTimeout(timer));
    this.typingTimers.clear();
    this.presenceMap.clear();
    this.presence.set({});
    this.typing.set({});

    const hub = this.connection;
    this.connection = null;
    this.state.set(HubConnectionState.Disconnected);
    if (hub) {
      void hub.stop();
    }
  }

  sendMessage(conversationId: string, content: string): Promise<void> {
    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      return Promise.reject(new Error('Connexion temps reel indisponible.'));
    }

    return this.connection.invoke('SendMessage', conversationId, content);
  }

  editMessage(messageId: string, newContent: string): Promise<void> {
    if (!this.isConnected()) {
      return Promise.reject(new Error('Connexion temps reel indisponible.'));
    }

    return this.connection!.invoke('EditMessage', messageId, newContent);
  }

  deleteMessage(messageId: string): Promise<void> {
    if (!this.isConnected()) {
      return Promise.reject(new Error('Connexion temps reel indisponible.'));
    }

    return this.connection!.invoke('DeleteMessage', messageId);
  }

  startTyping(conversationId: string): void {
    if (!this.isConnected()) {
      return;
    }
    void this.connection?.invoke('StartTyping', conversationId);
  }

  stopTyping(conversationId: string): void {
    if (!this.isConnected()) {
      return;
    }
    void this.connection?.invoke('StopTyping', conversationId);
  }

  callUser(calleeId: string, type: number): Promise<CallSessionDto> {
    return this.invoke<CallSessionDto>('CallUser', calleeId, type);
  }

  acceptCall(callId: string): Promise<void> {
    return this.invoke<void>('AcceptCall', callId);
  }

  rejectCall(callId: string): Promise<void> {
    return this.invoke<void>('RejectCall', callId);
  }

  endCall(callId: string): Promise<void> {
    return this.invoke<void>('EndCall', callId);
  }

  sendOffer(callId: string, sdp: string): Promise<void> {
    return this.invoke<void>('SendOffer', callId, sdp);
  }

  sendAnswer(callId: string, sdp: string): Promise<void> {
    return this.invoke<void>('SendAnswer', callId, sdp);
  }

  sendIceCandidate(
    callId: string,
    candidate: string,
    sdpMid: string | null,
    sdpMLineIndex: number | null,
  ): Promise<void> {
    return this.invoke<void>('SendIceCandidate', callId, candidate, sdpMid, sdpMLineIndex);
  }

  isOnline(userId: string): boolean {
    return this.presenceMap.get(userId) === true;
  }

  typingDisplayName(conversationId: string, userId: string): string | undefined {
    return this.typing()[`${conversationId}:${userId}`];
  }

  private invoke<T>(method: string, ...args: unknown[]): Promise<T> {
    if (!this.isConnected()) {
      return Promise.reject(new Error('Connexion temps reel indisponible.'));
    }

    return this.connection!.invoke<T>(method, ...args);
  }

  private applySnapshot(onlineIds: readonly string[]): void {
    this.presenceMap.clear();
    for (const id of onlineIds) {
      this.presenceMap.set(id, true);
    }
    this.publishPresence();
  }

  private publishPresence(): void {
    this.presence.set(Object.fromEntries(this.presenceMap));
  }

  private signalTyping(conversationId: string, userId: string, displayName: string): void {
    const key: TypingKey = `${conversationId}:${userId}`;
    this.typing.update((current) => ({ ...current, [key]: displayName }));

    const previous = this.typingTimers.get(key);
    if (previous) {
      clearTimeout(previous);
    }
    this.typingTimers.set(
      key,
      setTimeout(() => this.clearTyping(conversationId, userId), TYPING_TTL_MS),
    );
  }

  private clearTyping(conversationId: string, userId: string): void {
    const key: TypingKey = `${conversationId}:${userId}`;
    const timer = this.typingTimers.get(key);
    if (timer) {
      clearTimeout(timer);
      this.typingTimers.delete(key);
    }
    this.typing.update((current) => {
      if (!(key in current)) {
        return current;
      }
      const next = { ...current };
      delete next[key];
      return next;
    });
  }
}