import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import { ConversationsService } from '../../core/api/conversations.service';
import { AuthService } from '../../core/auth/auth.service';
import { ChatService } from '../../core/chat/chat.service';
import { ConversationListItemDto, MessageDto } from '../../core/models/conversation.models';
import { initialsOf, shortTime } from '../../core/utils/display';

const TYPING_SENT_INTERVAL_MS = 1500;
const STOP_TYPING_DELAY_MS = 1000;

@Component({
  selector: 'app-thread',
  imports: [FormsModule, RouterLink],
  templateUrl: './thread.html',
  styleUrl: './thread.scss',
})
export class ThreadComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly conversations = inject(ConversationsService);
  private readonly chat = inject(ChatService);
  private readonly auth = inject(AuthService);

  private readonly scrollArea = viewChild<ElementRef<HTMLDivElement>>('scrollArea');
  private readonly stopTyping$ = new Subject<void>();
  private lastTypingSent = 0;

  readonly conversationId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly messages = signal<MessageDto[]>([]);
  readonly hasMore = signal(false);
  readonly loadingOlder = signal(false);
  readonly sending = signal(false);
  readonly input = signal('');
  readonly atBottom = signal(true);
  readonly conversation = signal<ConversationListItemDto | null>(null);
  readonly typing = this.chat.typing;

  readonly editingId = signal<string | null>(null);
  readonly editValue = signal('');
  readonly savingEdit = signal(false);

  readonly currentUserId = this.auth.user()?.id;

  constructor() {
    this.stopTyping$
      .pipe(debounceTime(STOP_TYPING_DELAY_MS), takeUntilDestroyed())
      .subscribe(() => this.chat.stopTyping(this.conversationId() ?? ''));

    this.chat.receiveMessage$.pipe(takeUntilDestroyed()).subscribe((message) => {
      if (message.conversationId === this.conversationId()) {
        this.appendLiveMessage(message);
        this.markRead();
      }
    });

    this.chat.messageEdited$.pipe(takeUntilDestroyed()).subscribe((message) => {
      if (message.conversationId === this.conversationId()) {
        this.messages.update((current) => current.map((m) => (m.id === message.id ? message : m)));
      }
    });

    this.chat.messageDeleted$.pipe(takeUntilDestroyed()).subscribe(({ conversationId, messageId }) => {
      if (conversationId === this.conversationId()) {
        this.messages.update((current) =>
          current.map((m) =>
            m.id === messageId
              ? { ...m, content: null, edited: false, deleted: true }
              : m,
          ),
        );
      }
    });

    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('id');
      if (id && id !== this.conversationId()) {
        this.conversationId.set(id);
        this.cancelEdit();
        this.reload();
      }
    });
  }

  onScroll(): void {
    const el = this.scrollArea()?.nativeElement;
    if (!el) {
      return;
    }

    const fromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    this.atBottom.set(fromBottom < 120);

    if (this.hasMore() && !this.loadingOlder() && el.scrollTop < 60) {
      this.loadOlder();
    }
  }

  send(): void {
    const content = this.input().trim();
    const conversationId = this.conversationId();
    if (!content || !conversationId) {
      return;
    }

    this.sending.set(true);
    this.chat
      .sendMessage(conversationId, content)
      .then(() => {
        this.stopTypingNow();
        this.input.set('');
        this.sending.set(false);
        this.scrollToBottom();
      })
      .catch(() => {
        this.sending.set(false);
      });
  }

  onInput(): void {
    const conversationId = this.conversationId();
    if (!conversationId) {
      return;
    }

    const now = Date.now();
    if (now - this.lastTypingSent >= TYPING_SENT_INTERVAL_MS) {
      this.lastTypingSent = now;
      this.chat.startTyping(conversationId);
    }
    this.stopTyping$.next();
  }

  onInputEvent(event: Event): void {
    this.input.set((event.target as HTMLInputElement).value);
    this.onInput();
  }

  otherTypingName(): string | undefined {
    const conversationId = this.conversationId();
    if (!conversationId) {
      return undefined;
    }

    const typing = this.typing();
    const key = Object.keys(typing).find((k) => k.startsWith(`${conversationId}:`));
    return key ? typing[key] : undefined;
  }

  isGroup(): boolean {
    return this.conversation()?.type === 1;
  }

  conversationTitle(): string {
    const conversation = this.conversation();
    if (!conversation) {
      return 'Conversation';
    }

    return conversation.type === 1
      ? (conversation.name ?? 'Groupe')
      : (conversation.otherDisplayName ?? 'Conversation');
  }

  headerInitials(): string {
    const conversation = this.conversation();
    if (!conversation) {
      return '?';
    }

    return initialsOf(
      conversation.type === 1 ? (conversation.name ?? 'Groupe') : (conversation.otherDisplayName ?? '?'),
    );
  }

  participantCount(): number {
    return this.conversation()?.participantCount ?? 0;
  }

  canEdit(message: MessageDto): boolean {
    return message.senderId === this.currentUserId && !message.deleted;
  }

  startEdit(message: MessageDto): void {
    this.editingId.set(message.id);
    this.editValue.set(message.content ?? '');
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editValue.set('');
    this.savingEdit.set(false);
  }

  onEditInputEvent(event: Event): void {
    this.editValue.set((event.target as HTMLInputElement).value);
  }

  saveEdit(): void {
    const messageId = this.editingId();
    const content = this.editValue().trim();
    if (!messageId || !content) {
      return;
    }

    this.savingEdit.set(true);
    this.chat
      .editMessage(messageId, content)
      .then(() => {
        this.savingEdit.set(false);
        this.cancelEdit();
      })
      .catch(() => this.savingEdit.set(false));
  }

  deleteMessage(message: MessageDto): void {
    if (!confirm('Supprimer ce message ?')) {
      return;
    }

    void this.chat.deleteMessage(message.id).catch(() => undefined);
  }

  peerInitials(): string {
    return initialsOf(this.conversation()?.otherDisplayName ?? '?');
  }

  peerOnline(): boolean {
    const otherUserId = this.conversation()?.otherUserId;
    return otherUserId !== null && otherUserId !== undefined && this.chat.isOnline(otherUserId);
  }

  time(message: MessageDto): string {
    return shortTime(message.createdAt);
  }

  private reload(): void {
    const conversationId = this.conversationId();
    if (!conversationId) {
      return;
    }

    this.error.set(null);
    this.messages.set([]);
    this.hasMore.set(false);

    this.conversations.getMyConversations().subscribe({
      next: (list) => {
        this.conversation.set(list.find((c) => c.id === conversationId) ?? null);
      },
      error: () => {
        this.conversation.set(null);
      },
    });

    this.conversations.getMessages(conversationId).subscribe({
      next: (result) => {
        this.messages.set([...result.items].reverse());
        this.hasMore.set(result.hasMore);
        this.scrollToBottom();
        this.markRead();
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(this.readErrorMessage(error));
      },
    });
  }

  private loadOlder(): void {
    const conversationId = this.conversationId();
    const current = this.messages();
    if (!conversationId || current.length === 0) {
      return;
    }

    this.loadingOlder.set(true);
    const oldest = current[0];
    this.conversations.getMessages(conversationId, oldest.sequence).subscribe({
      next: (result) => {
        const el = this.scrollArea()?.nativeElement;
        const previousHeight = el?.scrollHeight ?? 0;

        const older = [...result.items].reverse();
        const currentIds = new Set(current.map((m) => m.id));
        const merged = [...older.filter((m) => !currentIds.has(m.id)), ...current];
        this.messages.set(merged);
        this.hasMore.set(result.hasMore);

        if (el) {
          el.scrollTop = el.scrollHeight - previousHeight;
        }
        this.loadingOlder.set(false);
      },
      error: () => {
        this.loadingOlder.set(false);
      },
    });
  }

  private appendLiveMessage(message: MessageDto): void {
    const current = this.messages();
    if (current.some((m) => m.id === message.id)) {
      return;
    }

    this.messages.set([...current, message]);
    if (this.atBottom()) {
      this.scrollToBottom();
    }
  }

  private markRead(): void {
    const conversationId = this.conversationId();
    const current = this.messages();
    if (!conversationId || current.length === 0) {
      return;
    }

    const last = current[current.length - 1];
    this.conversations.markAsRead(conversationId, last.id).subscribe({ error: () => undefined });
  }

  private scrollToBottom(): void {
    const el = this.scrollArea()?.nativeElement;
    if (!el) {
      return;
    }
    requestAnimationFrame(() => {
      el.scrollTo({ top: el.scrollHeight });
      this.atBottom.set(true);
    });
  }

  private stopTypingNow(): void {
    this.chat.stopTyping(this.conversationId() ?? '');
    this.lastTypingSent = 0;
  }

  private readErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'Serveur injoignable.';
    }

    const problem = error.error as { detail?: string; title?: string } | null;
    return problem?.detail ?? problem?.title ?? 'Erreur lors du chargement.';
  }
}