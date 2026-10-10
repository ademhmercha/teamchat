import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
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

  isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  start(): void {
    if (this.connection) {
      return;
    }

    const hub = new HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl}/hubs/chat`, {
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

  isOnline(userId: string): boolean {
    return this.presenceMap.get(userId) === true;
  }

  typingDisplayName(conversationId: string, userId: string): string | undefined {
    return this.typing()[`${conversationId}:${userId}`];
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