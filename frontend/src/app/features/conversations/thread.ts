import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import { AttachmentsService } from '../../core/api/attachments.service';
import { ConversationsService } from '../../core/api/conversations.service';
import { AuthService } from '../../core/auth/auth.service';
import { CallService } from '../../core/calls/call.service';
import { ChatService } from '../../core/chat/chat.service';
import { CallType } from '../../core/models/call.models';
import { ConversationListItemDto, MessageDto } from '../../core/models/conversation.models';
import { formatDuration, formatFileSize, initialsOf, shortTime } from '../../core/utils/display';
import { IconComponent } from '../../shared/icon/icon';
import { EmptyStateComponent } from '../../shared/ui/empty-state';
import { ConversationsUiService } from './conversations-ui.service';

const TYPING_SENT_INTERVAL_MS = 1500;
const STOP_TYPING_DELAY_MS = 1000;
const MAX_VOICE_SECONDS = 120;
const GROUP_WINDOW_MS = 5 * 60 * 1000;

const EMOJIS = ['😀', '😄', '😉', '👍', '🙏', '🎉', '❤️', '🔥', '😅', '🤔', '👏', '✅'];

interface DisplayRow {
  message: MessageDto;
  isSystem: boolean;
  own: boolean;
  grouped: boolean;
  lastOfGroup: boolean;
  dateLabel: string | null;
}

const DAY_MS = 24 * 60 * 60 * 1000;

function startOfDay(date: Date): number {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
}

function formatDay(iso: string): string {
  const date = new Date(iso);
  const today = new Date();
  const diffDays = Math.round((startOfDay(today) - startOfDay(date)) / DAY_MS);
  if (diffDays === 0) {
    return "Aujourd'hui";
  }
  if (diffDays === 1) {
    return 'Hier';
  }
  const sameYear = date.getFullYear() === today.getFullYear();
  return new Intl.DateTimeFormat('fr-FR', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    ...(sameYear ? {} : { year: 'numeric' as const }),
  }).format(date);
}

interface SharedFile {
  id: string;
  fileName: string;
  sizeBytes: number;
  isImage: boolean;
}

@Component({
  selector: 'app-thread',
  imports: [FormsModule, IconComponent, EmptyStateComponent],
  templateUrl: './thread.html',
  styleUrl: './thread.scss',
})
export class ThreadComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly conversations = inject(ConversationsService);
  private readonly attachments = inject(AttachmentsService);
  private readonly chat = inject(ChatService);
  private readonly calls = inject(CallService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  readonly ui = inject(ConversationsUiService);

  private readonly scrollArea = viewChild<ElementRef<HTMLDivElement>>('scrollArea');
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');
  private readonly stopTyping$ = new Subject<void>();
  private lastTypingSent = 0;

  private mediaRecorder: MediaRecorder | null = null;
  private recordedChunks: Blob[] = [];
  private recordStart = 0;
  private recordTimer?: number;

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

  readonly uploading = signal(false);
  readonly uploadProgress = signal(0);
  readonly recording = signal(false);
  readonly recordSeconds = signal(0);
  readonly mediaUrls = signal<Record<string, string>>({});

  readonly editingId = signal<string | null>(null);
  readonly editValue = signal('');
  readonly savingEdit = signal(false);

  readonly newCount = signal(0);
  readonly copiedId = signal<string | null>(null);

  readonly detailsOpen = signal(true);
  readonly emojiOpen = signal(false);
  readonly emojis = EMOJIS;

  readonly currentUserId = this.auth.user()?.id;

  readonly displayMessages = computed<DisplayRow[]>(() => {
    const list = this.messages();
    return list.map((message, index) => {
      const prev = list[index - 1];
      const next = list[index + 1];
      const isSystem = message.type === 4;
      const grouped =
        !isSystem &&
        !!prev &&
        prev.type !== 4 &&
        prev.senderId === message.senderId &&
        !this.isDeleted(message) &&
        this.withinGroup(prev, message);
      const nextSame =
        !isSystem &&
        !!next &&
        next.type !== 4 &&
        next.senderId === message.senderId &&
        !this.isDeleted(next) &&
        this.withinGroup(message, next);

      const newDay = !prev || startOfDay(new Date(prev.createdAt)) !== startOfDay(new Date(message.createdAt));

      return {
        message,
        isSystem,
        own: message.senderId === this.currentUserId,
        grouped,
        lastOfGroup: !nextSame,
        dateLabel: newDay ? formatDay(message.createdAt) : null,
      };
    });
  });

  readonly sharedFiles = computed<SharedFile[]>(() =>
    this.messages()
      .filter((message) => !!message.attachment && (message.type === 2 || message.type === 3))
      .map((message) => ({
        id: message.attachment!.id,
        fileName: message.attachment!.fileName,
        sizeBytes: message.attachment!.sizeBytes,
        isImage: message.type === 3,
      })),
  );

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

    this.destroyRef.onDestroy(() => {
      this.finishRecording(false);
      this.attachments.releaseAll();
    });
  }

  pickFile(): void {
    this.fileInput()?.nativeElement.click();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }

    this.uploadFile(file, file.name);
  }

  downloadAttachment(attachmentId: string, fileName: string): void {
    this.attachments.download(attachmentId, fileName);
  }

  async startRecording(): Promise<void> {
    if (this.recording() || !navigator.mediaDevices?.getUserMedia) {
      this.error.set('L\'enregistrement audio n\'est pas disponible sur ce navigateur.');
      return;
    }

    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const mimeType = this.pickAudioMimeType();
      this.mediaRecorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);
      this.recordedChunks = [];
      this.mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          this.recordedChunks.push(event.data);
        }
      };
      this.mediaRecorder.start();

      this.recordStart = Date.now();
      this.recordSeconds.set(0);
      this.recording.set(true);
      this.recordTimer = window.setInterval(() => {
        this.recordSeconds.update((value) => value + 1);
        if (this.recordSeconds() >= MAX_VOICE_SECONDS) {
          this.stopRecordingAndSend();
        }
      }, 1000);
    } catch {
      this.error.set('Micro indisponible ou accès refusé.');
    }
  }

  stopRecordingAndSend(): void {
    this.finishRecording(true);
  }

  cancelRecording(): void {
    this.finishRecording(false);
  }

  size(bytes: number): string {
    return formatFileSize(bytes);
  }

  duration(seconds: number | null | undefined): string {
    return formatDuration(seconds);
  }

  mediaUrl(message: MessageDto): string {
    const attachment = message.attachment;
    return attachment ? (this.mediaUrls()[attachment.id] ?? '') : '';
  }

  onScroll(): void {
    const el = this.scrollArea()?.nativeElement;
    if (!el) {
      return;
    }

    const fromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    const atBottom = fromBottom < 120;
    this.atBottom.set(atBottom);
    if (atBottom) {
      this.newCount.set(0);
    }

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

  canCall(): boolean {
    return !this.isGroup() && !!this.conversation()?.otherUserId;
  }

  startAudioCall(): void {
    this.startCall(CallType.Audio);
  }

  startVideoCall(): void {
    this.startCall(CallType.Video);
  }

  private startCall(type: number): void {
    const conversation = this.conversation();
    const otherUserId = conversation?.otherUserId;
    if (!otherUserId) {
      return;
    }

    void this.calls.startCall(otherUserId, conversation?.otherDisplayName ?? 'Contact', type);
  }

  time(message: MessageDto): string {
    return shortTime(message.createdAt);
  }

  initialsOfName(name: string | null | undefined): string {
    return initialsOf(name ?? '?');
  }

  toggleDetails(): void {
    this.detailsOpen.update((open) => !open);
  }

  closeDetails(): void {
    this.detailsOpen.set(false);
  }

  toggleEmoji(): void {
    this.emojiOpen.update((open) => !open);
  }

  insertEmoji(emoji: string): void {
    this.input.update((value) => value + emoji);
    this.emojiOpen.set(false);
  }

  private isDeleted(message: MessageDto): boolean {
    return message.deleted;
  }

  private withinGroup(previous: MessageDto, current: MessageDto): boolean {
    const prevTime = new Date(previous.createdAt).getTime();
    const currTime = new Date(current.createdAt).getTime();
    return Math.abs(currTime - prevTime) <= GROUP_WINDOW_MS;
  }

  private uploadFile(file: File, fileName: string): void {
    this.uploadBlob(file, fileName, undefined, false);
  }

  private uploadBlob(blob: Blob, fileName: string, durationSec: number | undefined, isVoice: boolean): void {
    const conversationId = this.conversationId();
    if (!conversationId) {
      return;
    }

    this.error.set(null);
    this.uploading.set(true);
    this.uploadProgress.set(0);

    this.attachments
      .upload(conversationId, blob, { fileName, isVoice, durationSec })
      .subscribe({
        next: (event) => this.uploadProgress.set(event.progress),
        error: (error: HttpErrorResponse) => {
          this.uploading.set(false);
          this.uploadProgress.set(0);
          this.error.set(this.readErrorMessage(error));
        },
        complete: () => {
          this.uploading.set(false);
          this.uploadProgress.set(0);
          this.scrollToBottom();
        },
      });
  }

  private finishRecording(send: boolean): void {
    const recorder = this.mediaRecorder;
    if (this.recordTimer !== undefined) {
      clearInterval(this.recordTimer);
      this.recordTimer = undefined;
    }

    if (!recorder) {
      this.recording.set(false);
      return;
    }

    const duration = Math.max(1, Math.round((Date.now() - this.recordStart) / 1000));
    const mimeType = recorder.mimeType || 'audio/webm';
    const stream = recorder.stream;

    recorder.onstop = () => {
      stream.getTracks().forEach((track) => track.stop());
      if (send && this.recordedChunks.length > 0) {
        const type = mimeType.split(';')[0];
        const extension = type.includes('mp4') ? 'm4a' : type.includes('ogg') ? 'ogg' : 'webm';
        const blob = new Blob(this.recordedChunks, { type });
        this.uploadBlob(blob, `vocal.${extension}`, duration, true);
      }

      this.recordedChunks = [];
    };

    this.mediaRecorder = null;
    this.recording.set(false);
    this.recordSeconds.set(0);
    recorder.stop();
  }

  private pickAudioMimeType(): string | undefined {
    if (typeof MediaRecorder === 'undefined') {
      return undefined;
    }

    const candidates = ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus', 'audio/mp4'];
    return candidates.find((candidate) => MediaRecorder.isTypeSupported(candidate));
  }

  private ensureMedia(message: MessageDto): void {
    const attachment = message.attachment;
    if (!attachment || (message.type !== 1 && message.type !== 3)) {
      return;
    }

    if (this.mediaUrls()[attachment.id]) {
      return;
    }

    this.attachments.getBlobUrl(attachment.id).subscribe({
      next: (url) => this.mediaUrls.update((current) => ({ ...current, [attachment.id]: url })),
      error: () => undefined,
    });
  }

  private reload(): void {
    const conversationId = this.conversationId();
    if (!conversationId) {
      return;
    }

    this.error.set(null);
    this.messages.set([]);
    this.hasMore.set(false);
    this.newCount.set(0);

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
        result.items.forEach((message) => this.ensureMedia(message));
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
        older.forEach((message) => this.ensureMedia(message));

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
    this.ensureMedia(message);
    if (this.atBottom()) {
      this.scrollToBottom();
    } else if (message.senderId !== this.currentUserId) {
      this.newCount.update((value) => value + 1);
    }
  }

  jumpToLatest(): void {
    this.newCount.set(0);
    this.scrollToBottom();
  }

  copyMessage(message: MessageDto): void {
    const text = message.content ?? '';
    if (!text || !navigator.clipboard) {
      return;
    }

    navigator.clipboard
      .writeText(text)
      .then(() => {
        this.copiedId.set(message.id);
        window.setTimeout(() => {
          if (this.copiedId() === message.id) {
            this.copiedId.set(null);
          }
        }, 1400);
      })
      .catch(() => undefined);
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