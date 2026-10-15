import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
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
  readonly notice = signal<string | null>(null);
  readonly items = signal<UserProfile[]>([]);
  readonly search = signal('');
  readonly saving = signal(false);
  readonly busyId = signal<string | null>(null);

  readonly roleFilter = signal<'all' | 'User' | 'Admin'>('all');
  readonly statusFilter = signal<'all' | 'active' | 'inactive'>('all');
  readonly sortKey = signal<'name' | 'email' | 'role'>('name');
  readonly sortDir = signal<'asc' | 'desc'>('asc');
  readonly page = signal(1);
  readonly pageSize = 20;

  readonly filtered = computed(() => {
    const role = this.roleFilter();
    const status = this.statusFilter();
    const list = this.items().filter(
      (user) =>
        (role === 'all' || user.role === role) &&
        (status === 'all' || (status === 'active') === user.isActive),
    );

    const key = this.sortKey();
    const direction = this.sortDir() === 'asc' ? 1 : -1;
    return [...list].sort((a, b) => {
      const left = key === 'name' ? a.displayName : key === 'email' ? a.email : a.role;
      const right = key === 'name' ? b.displayName : key === 'email' ? b.email : b.role;
      return left.localeCompare(right, 'fr', { sensitivity: 'base' }) * direction;
    });
  });

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize)));

  readonly paged = computed(() => {
    const start = (this.page() - 1) * this.pageSize;
    return this.filtered().slice(start, start + this.pageSize);
  });

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
    this.page.set(1);
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }
    this.searchTimer = setTimeout(() => this.load(), 300);
  }

  setRoleFilter(value: string): void {
    this.roleFilter.set(value as 'all' | 'User' | 'Admin');
    this.page.set(1);
  }

  setStatusFilter(value: string): void {
    this.statusFilter.set(value as 'all' | 'active' | 'inactive');
    this.page.set(1);
  }

  setSort(value: string): void {
    const [key, dir] = value.split(':');
    this.sortKey.set(key as 'name' | 'email' | 'role');
    this.sortDir.set(dir as 'asc' | 'desc');
    this.page.set(1);
  }

  goToPage(page: number): void {
    this.page.set(Math.min(Math.max(1, page), this.totalPages()));
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

  requestReset(user: UserProfile): void {
    if (!confirm(`Envoyer un nouveau lien de mot de passe à ${user.displayName} ?`)) {
      return;
    }

    this.busyId.set(user.id);
    this.error.set(null);
    this.admin.resetPassword(user.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.flash(`Lien de réinitialisation envoyé à ${user.email}.`);
      },
      error: (error: HttpErrorResponse) => {
        this.busyId.set(null);
        this.error.set(this.readErrorMessage(error));
      },
    });
  }

  deleteUser(user: UserProfile): void {
    if (
      !confirm(
        `Supprimer le compte de ${user.displayName} ?\nLes messages seront conservés (auteur « Utilisateur supprimé »).`,
      )
    ) {
      return;
    }

    this.busyId.set(user.id);
    this.error.set(null);
    this.admin.remove(user.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.items.update((users) => users.filter((item) => item.id !== user.id));
        this.flash(`Compte de ${user.displayName} supprimé.`);
      },
      error: (error: HttpErrorResponse) => {
        this.busyId.set(null);
        this.error.set(this.readErrorMessage(error));
      },
    });
  }

  private flash(message: string): void {
    this.notice.set(message);
    setTimeout(() => this.notice.set(null), 4000);
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
        this.page.set(1);
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
