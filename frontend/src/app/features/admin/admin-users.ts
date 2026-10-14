import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminUsersService } from '../../core/api/admin-users.service';
import { UserProfile } from '../../core/models/user.models';
import { IconComponent } from '../../shared/icon/icon';
import { AvatarComponent } from '../../shared/ui/avatar';
import { BadgeComponent } from '../../shared/ui/badge';
import { EmptyStateComponent } from '../../shared/ui/empty-state';
import { ModalComponent } from '../../shared/ui/modal';

@Component({
  selector: 'app-admin-users',
  imports: [
    FormsModule,
    IconComponent,
    AvatarComponent,
    BadgeComponent,
    EmptyStateComponent,
    ModalComponent,
  ],
  templateUrl: './admin-users.html',
  styleUrl: './admin-users.scss',
})
export class AdminUsersComponent {
  private readonly admin = inject(AdminUsersService);

  readonly roles = ['User', 'Admin'];
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<UserProfile[]>([]);
  readonly search = signal('');
  readonly saving = signal(false);
  readonly busyId = signal<string | null>(null);

  readonly editorOpen = signal(false);
  readonly formError = signal<string | null>(null);
  readonly newEmail = signal('');
  readonly newDisplayName = signal('');
  readonly newRole = signal('User');

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.load();
  }

  onSearchInput(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }
    this.searchTimer = setTimeout(() => this.load(), 300);
  }

  openCreate(): void {
    this.newEmail.set('');
    this.newDisplayName.set('');
    this.newRole.set('User');
    this.formError.set(null);
    this.editorOpen.set(true);
  }

  closeEditor(): void {
    this.editorOpen.set(false);
  }

  save(): void {
    const email = this.newEmail().trim();
    const displayName = this.newDisplayName().trim();

    if (!email || !displayName) {
      this.formError.set("L'email et le nom sont obligatoires.");
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    this.admin.create({ email, displayName, role: this.newRole() }).subscribe({
      next: () => {
        this.saving.set(false);
        this.closeEditor();
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.formError.set(this.readErrorMessage(error));
      },
    });
  }

  toggleActive(user: UserProfile): void {
    if (user.isActive && !confirm(`Désactiver le compte de ${user.displayName} ?\nCelui-ci sera déconnecté.`)) {
      return;
    }

    this.busyId.set(user.id);
    this.admin.update(user.id, { isActive: !user.isActive }).subscribe({
      next: (updated) => {
        this.busyId.set(null);
        this.replace(updated);
      },
      error: (error: HttpErrorResponse) => {
        this.busyId.set(null);
        this.error.set(this.readErrorMessage(error));
      },
    });
  }

  changeRole(user: UserProfile, role: string): void {
    if (role === user.role) {
      return;
    }

    this.busyId.set(user.id);
    this.admin.update(user.id, { role }).subscribe({
      next: (updated) => {
        this.busyId.set(null);
        this.replace(updated);
      },
      error: (error: HttpErrorResponse) => {
        this.busyId.set(null);
        this.error.set(this.readErrorMessage(error));
      },
    });
  }

  private replace(updated: UserProfile): void {
    this.items.update((users) => users.map((user) => (user.id === updated.id ? updated : user)));
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.admin.list(this.search().trim() || null, 1, 100).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(this.readErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  private readErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'Serveur injoignable.';
    }

    const problem = error.error as { detail?: string; title?: string } | null;
    return problem?.detail ?? problem?.title ?? 'Erreur.';
  }
}
