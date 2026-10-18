import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, computed, effect, inject, signal, viewChild, HostListener } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, catchError, map, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { AttachmentsService } from '../../core/api/attachments.service';
import { ConversationsService } from '../../core/api/conversations.service';
import { AuthService } from '../../core/auth/auth.service';
import { CallService } from '../../core/calls/call.service';
import { ChatService } from '../../core/chat/chat.service';
import { CallType } from '../../core/models/call.models';
import {
  ConversationDetailDto,
  ConversationListItemDto,
  MessageDto,
  SharedAttachmentDto,
} from '../../core/models/conversation.models';
import { formatDuration, formatFileSize, shortTime } from '../../core/utils/display';
import { IconComponent } from '../../shared/icon/icon';
import { AvatarComponent } from '../../shared/ui/avatar';
import { ConfirmComponent } from '../../shared/ui/confirm';
import { EmptyStateComponent } from '../../shared/ui/empty-state';
import { LightboxComponent } from '../../shared/ui/lightbox';
import { ToastService } from '../../shared/ui/toast';
import { ConversationsUiService } from './conversations-ui.service';
import { VoicePlayerComponent } from './voice-player';

const TYPING_SENT_INTERVAL_MS = 1500;
const STOP_TYPING_DELAY_MS = 1000;
const SEARCH_DEBOUNCE_MS = 300;
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
  imports: [
    FormsModule,
    IconComponent,
    AvatarComponent,
    ConfirmComponent,
    EmptyStateComponent,
    VoicePlayerComponent,
    LightboxComponent,
  ],
  templateUrl: './thread.html',
  styleUrl: './thread.scss',
})
export class ThreadComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly conversations = inject(ConversationsService);
  private readonly attachments = inject(AttachmentsService);
  private readonly chat = inject(ChatService);
  private readonly calls = inject(CallService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  readonly ui = inject(ConversationsUiService);

  private readonly scrollArea = viewChild<ElementRef<HTMLDivElement>>('scrollArea');
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');
  private readonly composerInput = viewChild<ElementRef<HTMLTextAreaElement>>('composerInput');
  private readonly detailsButton = viewChild<ElementRef<HTMLButtonElement>>('detailsButton');
  private readonly drafts = new Map<string, string>();
  private dragDepth = 0;
  private readonly stopTyping$ = new Subject<void>();
  private readonly search$ = new Subject<string>();
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
  readonly detail = signal<ConversationDetailDto | null>(null);
  readonly sharedAttachments = signal<SharedAttachmentDto[]>([]);
  readonly typing = this.chat.typing;

  readonly searchOpen = signal(false);
  readonly searchQuery = signal('');
  readonly searchResults = signal<MessageDto[]>([]);
  readonly searching = signal(false);
  readonly highlightId = signal<string | null>(null);
  readonly archiveBusy = signal(false);
  readonly leaveBusy = signal(false);

  readonly pendingDelete = signal<MessageDto | null>(null);
  readonly confirmLeaveOpen = signal(false);

  readonly uploading = signal(false);
  readonly dragActive = signal(false);
  readonly uploadProgress = signal(0);
  readonly recording = signal(false);
  readonly recordSeconds = signal(0);
  readonly mediaUrls = signal<Record<string, string>>({});

  readonly editingId = signal<string | null>(null);
  readonly editValue = signal('');
  readonly savingEdit = signal(false);

  readonly newCount = signal(0);
  readonly copiedId = signal<string | null>(null);

  readonly detailsOpen = signal(false);
  readonly emojiOpen = signal(false);
  readonly emojis = EMOJIS;

  readonly lightboxOpen = signal(false);
  readonly lightboxSrc = signal('');
  readonly lightboxAlt = signal('');

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
    this.sharedAttachments().map((attachment) => ({
      id: attachment.attachmentId,
      fileName: attachment.fileName,
      sizeBytes: attachment.sizeBytes,
      isImage: attachment.type === 3,
    })),
  );

  readonly members = computed(() => this.detail()?.participants ?? []);

  readonly archived = computed(() => this.detail()?.archived ?? this.conversation()?.archived ?? false);

  constructor() {
    this.stopTyping$
      .pipe(debounceTime(STOP_TYPING_DELAY_MS), takeUntilDestroyed())
      .subscribe(() => this.chat.stopTyping(this.conversationId() ?? ''));

    this.search$
      .pipe(
        debounceTime(SEARCH_DEBOUNCE_MS),
        distinctUntilChanged(),
        switchMap((query) => {
          const conversationId = this.conversationId();
          if (!conversationId || query.length < 2) {
            return of({ items: [] as MessageDto[] });
          }

          this.searching.set(true);
          return this.conversations.searchMessages(conversationId, query).pipe(
            map((result) => ({ items: result.items })),
            catchError(() => of({ items: [] as MessageDto[] })),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((outcome) => {
        this.searchResults.set(outcome.items);
        this.searching.set(false);
      });

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

    effect(() => {
      const id = this.conversationId();
      if (id) {
        queueMicrotask(() => this.composerInput()?.nativeElement?.focus());
      }
    });

    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('id');
      if (id && id !== this.conversationId()) {
        const previous = this.conversationId();
        if (previous) {
          this.drafts.set(previous, this.input());
        }
        this.conversationId.set(id);
        this.input.set(this.drafts.get(id) ?? '');
        this.cancelEdit();
        this.closeSearch();
        this.reload();
        setTimeout(() => this.autoGrow());
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

  onDragEnter(event: DragEvent): void {
    if (!this.dragHasFiles(event)) {
      return;
    }

    event.preventDefault();
    this.dragDepth += 1;
    this.dragActive.set(true);
  }

  onDragOver(event: DragEvent): void {
    if (!this.dragHasFiles(event)) {
      return;
    }

    event.preventDefault();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'copy';
    }
  }

  onDragLeave(event: DragEvent): void {
    if (!this.dragHasFiles(event)) {
      return;
    }

    event.preventDefault();
    this.dragDepth = Math.max(0, this.dragDepth - 1);
    if (this.dragDepth === 0) {
      this.dragActive.set(false);
    }
  }

  onDropFiles(event: DragEvent): void {
    const files = Array.from(event.dataTransfer?.files ?? []);
    if (files.length === 0) {
      return;
    }

    event.preventDefault();
    this.resetDragState();
    this.sendPastedOrDroppedFile(files);
  }

  onComposerPaste(event: ClipboardEvent): void {
    const files: File[] = [];
    for (const item of Array.from(event.clipboardData?.items ?? [])) {
      if (item.kind !== 'file') {
        continue;
      }

      const file = item.getAsFile();
      if (file) {
        files.push(file);
      }
    }

    if (files.length === 0) {
      return;
    }

    event.preventDefault();
    this.sendPastedOrDroppedFile(files);
  }

  @HostListener('document:dragleave', ['$event'])
  onDocumentDragLeave(event: DragEvent): void {
    if (!event.relatedTarget) {
      this.resetDragState();
    }
  }

  @HostListener('document:drop')
  onDocumentDrop(): void {
    this.resetDragState();
  }

  private resetDragState(): void {
    this.dragDepth = 0;
    this.dragActive.set(false);
  }

  private dragHasFiles(event: DragEvent): boolean {
    return Array.from(event.dataTransfer?.types ?? []).includes('Files');
  }

  private sendPastedOrDroppedFile(files: File[]): void {
    if (!this.conversationId()) {
      return;
    }

    if (this.uploading()) {
      this.toast.error('Un envoi est déjà en cours.');
      return;
    }

    if (files.length > 1) {
      this.toast.info('Un seul fichier à la fois.');
    }

    const file = files[0];
    this.uploadFile(file, this.uploadNameFor(file));
  }

  private uploadNameFor(file: File): string {
    if (file.name && file.name !== 'blob') {
      return file.name;
    }

    const subtype = file.type.split('/')[1]?.split(';')[0];
    if (!subtype) {
      return 'fichier';
    }

    return file.type.startsWith('image/') ? `image.${subtype}` : `fichier.${subtype}`;
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

  fileExt(fileName: string): string {
    const dot = fileName.lastIndexOf('.');
    return dot > 0 ? fileName.slice(dot + 1).toUpperCase().slice(0, 5) : '';
  }

  /** Tronque un nom de fichier au milieu en conservant l'extension (rapport_ann…pdf). */
  fileLabel(fileName: string, max = 32): string {
    if (fileName.length <= max) {
      return fileName;
    }

    const dot = fileName.lastIndexOf('.');
    const hasExt = dot > 0;
    const ext = hasExt ? fileName.slice(dot) : '';
    const base = hasExt ? fileName.slice(0, dot) : fileName;
    const keep = Math.max(6, max - ext.length - 1);
    const head = Math.ceil(keep * 0.6);
    const tail = Math.floor(keep * 0.4);

    return `${base.slice(0, head)}…${base.slice(base.length - tail)}${ext}`;
  }

  duration(seconds: number | null | undefined): string {
    return formatDuration(seconds);
  }

  mediaUrl(message: MessageDto): string {
    const attachment = message.attachment;
    return attachment ? (this.mediaUrls()[attachment.id] ?? '') : '';
  }

  openImage(message: MessageDto): void {
    const url = this.mediaUrl(message);
    if (!url) {
      return;
    }

    this.lightboxSrc.set(url);
    this.lightboxAlt.set(message.attachment?.fileName ?? '');
    this.lightboxOpen.set(true);
  }

  closeImage(): void {
    this.lightboxOpen.set(false);
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
    this.emojiOpen.set(false);
    this.chat
      .sendMessage(conversationId, content)
      .then(() => {
        this.stopTypingNow();
        this.drafts.delete(conversationId);
        this.input.set('');
        this.autoGrow();
        this.sending.set(false);
        this.scrollToBottom();
      })
      .catch(() => {
        this.sending.set(false);
        this.error.set("Le message n'a pas pu être envoyé. Réessayez.");
      });
  }

  onComposerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      this.send();
    }
  }

  private autoGrow(): void {
    const el = this.composerInput()?.nativeElement;
    if (!el) {
      return;
    }

    el.style.height = 'auto';
    el.style.height = `${Math.min(el.scrollHeight, 160)}px`;
    el.style.overflowY = el.scrollHeight > 160 ? 'auto' : 'hidden';
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
    this.input.set((event.target as HTMLTextAreaElement).value);
    this.autoGrow();
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

  /** Avatar de l'en-tête : un groupe n'a pas d'image, seules les conversations directes en ont une. */
  getHeaderAvatarUrl(): string | null {
    if (this.isGroup()) {
      return null;
    }

    return this.conversation()?.otherAvatarUrl ?? null;
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
      .catch(() => {
        this.savingEdit.set(false);
        this.toast.error("La modification n'a pas pu être enregistrée.");
      });
  }

  deleteMessage(message: MessageDto): void {
    this.pendingDelete.set(message);
  }

  confirmDeleteMessage(): void {
    const message = this.pendingDelete();
    this.pendingDelete.set(null);
    if (!message) {
      return;
    }

    void this.chat
      .deleteMessage(message.id)
      .catch(() => this.toast.error('La suppression du message a échoué.'));
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

    void this.calls.startCall(
      otherUserId,
      conversation?.otherDisplayName ?? 'Contact',
      type,
      conversation?.otherAvatarUrl ?? null,
    );
  }

  time(message: MessageDto): string {
    return shortTime(message.createdAt);
  }

  toggleDetails(): void {
    const next = !this.detailsOpen();
    this.detailsOpen.set(next);
    if (!next) {
      this.focusDetailsButton();
    }
  }

  closeDetails(): void {
    this.detailsOpen.set(false);
    this.focusDetailsButton();
  }

  private focusDetailsButton(): void {
    setTimeout(() => this.detailsButton()?.nativeElement.focus());
  }

  toggleArchive(): void {
    const conversationId = this.conversationId();
    if (!conversationId || this.archiveBusy()) {
      return;
    }

    const next = !this.archived();
    this.archiveBusy.set(true);
    this.conversations.setArchived(conversationId, next).subscribe({
      next: () => {
        this.archiveBusy.set(false);
        this.detail.update((detail) => (detail ? { ...detail, archived: next } : detail));
        this.conversation.update((conversation) => (conversation ? { ...conversation, archived: next } : conversation));
      },
      error: () => {
        this.archiveBusy.set(false);
        this.toast.error("L'opération sur la conversation a échoué.");
      },
    });
  }

  leaveConversation(): void {
    const conversationId = this.conversationId();
    if (!conversationId || this.leaveBusy()) {
      return;
    }

    this.confirmLeaveOpen.set(true);
  }

  confirmLeaveConversation(): void {
    this.confirmLeaveOpen.set(false);
    const conversationId = this.conversationId();
    if (!conversationId || this.leaveBusy()) {
      return;
    }

    this.leaveBusy.set(true);
    this.conversations.leave(conversationId).subscribe({
      next: () => {
        this.leaveBusy.set(false);
        void this.router.navigate(['/app/conversations']);
      },
      error: () => {
        this.leaveBusy.set(false);
        this.toast.error('Impossible de quitter le groupe.');
      },
    });
  }

  openSearch(): void {
    this.searchOpen.set(true);
  }

  closeSearch(): void {
    this.searchOpen.set(false);
    this.searchQuery.set('');
    this.search$.next('');
    this.searchResults.set([]);
    this.searching.set(false);
  }

  onSearchInput(event: Event): void {
    const query = (event.target as HTMLInputElement).value;
    this.searchQuery.set(query);
    this.search$.next(query.trim());
  }

  goToResult(message: MessageDto): void {
    this.closeSearch();
    const exists = this.messages().some((m) => m.id === message.id);
    if (!exists) {
      this.messages.update((current) => [...current, message].sort((a, b) => a.sequence - b.sequence));
      this.ensureMedia(message);
    }

    this.highlightId.set(message.id);
    requestAnimationFrame(() => {
      document.getElementById(`msg-${message.id}`)?.scrollIntoView({ block: 'center' });
    });
    window.setTimeout(() => {
      if (this.highlightId() === message.id) {
        this.highlightId.set(null);
      }
    }, 1800);
  }

  toggleEmoji(): void {
    this.emojiOpen.update((open) => !open);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const wrap = document.querySelector('.emoji-wrap');
    if (wrap && !wrap.contains(event.target as Node)) {
      this.emojiOpen.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.emojiOpen()) {
      this.emojiOpen.set(false);
    }
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

    this.conversations.getDetail(conversationId).subscribe({
      next: (detail) => this.detail.set(detail),
      error: () => this.detail.set(null),
    });

    this.conversations.getSharedAttachments(conversationId).subscribe({
      next: (files) => this.sharedAttachments.set(files),
      error: () => this.sharedAttachments.set([]),
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