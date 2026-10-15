import { Component, ElementRef, HostListener, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ChatService } from '../../core/chat/chat.service';
import { NotificationsService } from '../../core/api/notifications.service';
import { NotificationDto } from '../../core/models/notification.models';
import { listTime } from '../../core/utils/display';
import { IconComponent } from '../../shared/icon/icon';
import { AvatarComponent } from '../../shared/ui/avatar';
import { BadgeComponent } from '../../shared/ui/badge';
import { TooltipDirective } from '../../shared/ui/tooltip';
import { CallOverlayComponent } from './call-overlay';

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    CallOverlayComponent,
    IconComponent,
    AvatarComponent,
    BadgeComponent,
    TooltipDirective,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class ShellComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly chat = inject(ChatService);
  private readonly notificationsApi = inject(NotificationsService);

  readonly user = this.auth.user;
  readonly isAdmin = this.auth.isAdmin;
  readonly searchTerm = signal('');
  readonly shortcutHint = /Mac|iPhone|iPad/i.test(navigator.platform) ? '⌘K' : 'Ctrl K';

  readonly notifications = signal<NotificationDto[]>([]);
  readonly unreadCount = signal(0);
  readonly notificationsOpen = signal(false);
  readonly notificationsLoading = signal(false);

  private static readonly RAIL_KEY = 'tc.sidebar-collapsed';
  readonly collapsed = signal(this.readRail());

  private readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('searchInput');
  private readonly notificationsPanel = viewChild<ElementRef<HTMLElement>>('notificationsPanel');

  constructor() {
    this.chat.start();
    this.chat.forceLogout$.pipe(takeUntilDestroyed()).subscribe(() => this.forceLogout());
    this.chat.notificationsUpdated$.pipe(takeUntilDestroyed()).subscribe(() => this.loadNotifications());
    this.loadNotifications();
  }

  timeLabel(value: string): string {
    return listTime(value);
  }

  notificationIcon(type: number): 'conversations' | 'phone' {
    return type === 1 ? 'phone' : 'conversations';
  }

  loadNotifications(): void {
    this.notificationsLoading.set(true);
    this.notificationsApi.getMine(false, 30).subscribe({
      next: (result) => {
        this.notifications.set(result.items);
        this.unreadCount.set(result.unreadCount);
        this.notificationsLoading.set(false);
      },
      error: () => this.notificationsLoading.set(false),
    });
  }

  toggleNotifications(): void {
    const next = !this.notificationsOpen();
    this.notificationsOpen.set(next);
    if (next) {
      this.loadNotifications();
    }
  }

  openNotification(notification: NotificationDto): void {
    if (!notification.read) {
      this.notificationsApi.markRead(notification.id).subscribe(() => {
        this.notifications.update((items) =>
          items.map((item) => (item.id === notification.id ? { ...item, read: true } : item)),
        );
        this.unreadCount.update((count) => Math.max(0, count - 1));
      });
    }

    this.notificationsOpen.set(false);

    if (notification.conversationId) {
      void this.router.navigate(['/conversations', notification.conversationId]);
    }
  }

  markAllRead(): void {
    if (this.unreadCount() === 0) {
      return;
    }

    this.notificationsApi.markAllRead().subscribe(() => {
      this.notifications.update((items) => items.map((item) => ({ ...item, read: true })));
      this.unreadCount.set(0);
    });
  }

  toggleRail(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    try {
      localStorage.setItem(ShellComponent.RAIL_KEY, next ? '1' : '0');
    } catch {
      /* storage unavailable */
    }
  }

  private readRail(): boolean {
    try {
      return localStorage.getItem(ShellComponent.RAIL_KEY) === '1';
    } catch {
      return false;
    }
  }

  onSearchInput(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  submitSearch(event: Event): void {
    event.preventDefault();
    const term = this.searchTerm().trim();
    this.searchTerm.set('');
    void this.router.navigate(['/directory'], { queryParams: term ? { q: term } : {} });
  }

  @HostListener('window:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      this.searchInput()?.nativeElement.focus();
    }

    if (event.key === 'Escape' && this.notificationsOpen()) {
      this.notificationsOpen.set(false);
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.notificationsOpen()) {
      return;
    }

    const panel = this.notificationsPanel()?.nativeElement;
    if (panel && !panel.contains(event.target as Node)) {
      this.notificationsOpen.set(false);
    }
  }

  logout(): void {
    this.chat.stop();
    this.auth.logout().subscribe(() => void this.router.navigate(['/login']));
  }

  private forceLogout(): void {
    this.chat.stop();
    this.auth.clearSession();
    void this.router.navigate(['/login']);
  }
}
