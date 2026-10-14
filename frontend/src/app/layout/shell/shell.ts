import { Component, ElementRef, HostListener, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ChatService } from '../../core/chat/chat.service';
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

  readonly user = this.auth.user;
  readonly isAdmin = this.auth.isAdmin;
  readonly searchTerm = signal('');

  private static readonly RAIL_KEY = 'tc.sidebar-collapsed';
  readonly collapsed = signal(this.readRail());

  private readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('searchInput');

  constructor() {
    this.chat.start();
    this.chat.forceLogout$.pipe(takeUntilDestroyed()).subscribe(() => this.forceLogout());
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
