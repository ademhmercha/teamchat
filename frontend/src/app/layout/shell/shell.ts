import { Component, computed, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ChatService } from '../../core/chat/chat.service';
import { CallOverlayComponent } from './call-overlay';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CallOverlayComponent],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class ShellComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly chat = inject(ChatService);

  readonly user = this.auth.user;
  readonly isAdmin = this.auth.isAdmin;
  readonly initials = computed(() => {
    const name = this.auth.user()?.displayName ?? '';
    return name
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('');
  });

  constructor() {
    this.chat.start();
    this.chat.forceLogout$.pipe(takeUntilDestroyed()).subscribe(() => this.forceLogout());
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
