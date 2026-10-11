import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import { ConversationsService } from '../../core/api/conversations.service';
import { UsersService } from '../../core/api/users.service';
import { AuthService } from '../../core/auth/auth.service';
import { ChatService } from '../../core/chat/chat.service';
import { ConversationListItemDto } from '../../core/models/conversation.models';
import { UserListItem } from '../../core/models/user.models';
import { initialsOf, shortTime } from '../../core/utils/display';

@Component({
  selector: 'app-conversations-list',
  imports: [RouterLink, FormsModule],
  templateUrl: './conversations-list.html',
  styleUrl: './conversations-list.scss',
})
export class ConversationsListComponent {
  private readonly conversations = inject(ConversationsService);
  private readonly users = inject(UsersService);
  private readonly chat = inject(ChatService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  private readonly refresh$ = new Subject<void>();

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<ConversationListItemDto[]>([]);
  readonly presence = this.chat.presence;

  readonly pickerOpen = signal(false);
  readonly pickerSearch = signal('');
  readonly pickerLoading = signal(false);
  readonly pickerUsers = signal<UserListItem[]>([]);

  readonly currentUserId = this.auth.user()?.id;

  readonly isEmpty = computed(() => this.items().length === 0 && !this.loading());

  constructor() {
    this.refresh$
      .pipe(
        debounceTime(300),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.load());

    this.chat.receiveMessage$?.pipe(takeUntilDestroyed()).subscribe(() => this.refresh$.next());
    this.chat.messageEdited$?.pipe(takeUntilDestroyed()).subscribe(() => this.refresh$.next());
    this.chat.messageDeleted$?.pipe(takeUntilDestroyed()).subscribe(() => this.refresh$.next());
    this.chat.conversationUpdated$?.pipe(takeUntilDestroyed()).subscribe(() => this.refresh$.next());
    this.chat.addedToGroup$?.pipe(takeUntilDestroyed()).subscribe(() => this.refresh$.next());

    this.load();
  }

  isGroup(item: ConversationListItemDto): boolean {
    return item.type === 1;
  }

  title(item: ConversationListItemDto): string {
    return item.type === 1 ? (item.name ?? 'Groupe') : (item.otherDisplayName ?? 'Chat');
  }

  isOnline(item: ConversationListItemDto): boolean {
    return !this.isGroup(item) && item.otherUserId !== null && this.chat.isOnline(item.otherUserId);
  }

  initials(item: ConversationListItemDto): string {
    return initialsOf(item.type === 1 ? (item.name ?? 'Groupe') : (item.otherDisplayName ?? '?'));
  }

  openPicker(): void {
    this.pickerOpen.set(true);
    this.pickerSearch.set('');
    this.searchUsers('');
  }

  closePicker(): void {
    this.pickerOpen.set(false);
  }

  onPickerSearchEvent(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.pickerSearch.set(value);
    this.searchUsers(value);
  }

  startConversation(user: UserListItem): void {
    this.closePicker();
    this.conversations.createDirect(user.id).subscribe((conversation) => {
      void this.router.navigate(['/conversations', conversation.id]);
    });
  }

  initialsOfUser(user: UserListItem): string {
    return initialsOf(user.displayName);
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.conversations.getMyConversations().subscribe({
      next: (result) => {
        this.items.set(result);
        this.loading.set(false);
      },
      error: (error) => {
        this.error.set(this.readErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  private searchUsers(search: string): void {
    this.pickerLoading.set(true);
    this.users.search(search || null, 1, 30).subscribe({
      next: (result) => {
        this.pickerUsers.set(
          result.items.filter((u) => u.id !== this.currentUserId && u.isActive),
        );
        this.pickerLoading.set(false);
      },
      error: () => {
        this.pickerUsers.set([]);
        this.pickerLoading.set(false);
      },
    });
  }

  private readErrorMessage(error: unknown): string {
    const status = (error as { status?: number })?.status;
    if (status === 0) {
      return 'Serveur injoignable.';
    }

    const problem = (error as { error?: { detail?: string; title?: string } })?.error;
    return problem?.detail ?? problem?.title ?? 'Erreur lors du chargement.';
  }

  protected readonly shortTime = shortTime;
}