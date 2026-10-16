import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { PlatformService, PlatformTenant } from '../../core/api/platform.service';
import { BadgeComponent } from '../../shared/ui/badge';
import { ButtonComponent } from '../../shared/ui/button';

@Component({
  selector: 'app-platform',
  imports: [BadgeComponent, ButtonComponent],
  templateUrl: './platform.html',
  styleUrl: './platform.scss',
})
export class PlatformComponent {
  private readonly platformApi = inject(PlatformService);

  readonly tenants = signal<PlatformTenant[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);

  readonly editingId = signal<string | null>(null);
  readonly draftName = signal('');
  readonly savingId = signal<string | null>(null);
  readonly togglingId = signal<string | null>(null);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.platformApi.list().subscribe({
      next: (tenants) => {
        this.tenants.set(tenants);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(this.readError(error, 'Chargement impossible.'));
        this.loading.set(false);
      },
    });
  }

  startEdit(tenant: PlatformTenant): void {
    this.message.set(null);
    this.editingId.set(tenant.id);
    this.draftName.set(tenant.name);
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.draftName.set('');
  }

  onDraftInput(event: Event): void {
    this.draftName.set((event.target as HTMLInputElement).value);
  }

  saveName(tenant: PlatformTenant): void {
    const name = this.draftName().trim();
    if (!name) {
      this.error.set('Le nom de la societe est obligatoire.');
      return;
    }

    this.savingId.set(tenant.id);
    this.error.set(null);
    this.platformApi.update(tenant.id, { name }).subscribe({
      next: (updated) => {
        this.applyUpdate(updated);
        this.message.set(`Societe renommee en « ${updated.name} ».`);
        this.savingId.set(null);
        this.cancelEdit();
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(this.readError(error, 'Renommage impossible.'));
        this.savingId.set(null);
      },
    });
  }

  toggleActive(tenant: PlatformTenant): void {
    const nextActive = !tenant.isActive;
    this.togglingId.set(tenant.id);
    this.error.set(null);
    this.message.set(null);

    this.platformApi.update(tenant.id, { isActive: nextActive }).subscribe({
      next: (updated) => {
        this.applyUpdate(updated);
        this.message.set(
          updated.isActive
            ? `Societe « ${updated.name} » activee.`
            : `Societe « ${updated.name} » desactivee.`,
        );
        this.togglingId.set(null);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(this.readError(error, 'Mise a jour impossible.'));
        this.togglingId.set(null);
      },
    });
  }

  private applyUpdate(updated: PlatformTenant): void {
    this.tenants.update((tenants) =>
      tenants.map((tenant) => (tenant.id === updated.id ? updated : tenant)),
    );
  }

  private readError(error: HttpErrorResponse, fallback: string): string {
    if (error.status === 0) {
      return 'Serveur injoignable.';
    }

    const problem = error.error as
      | { detail?: string; title?: string; errors?: Record<string, string[]> }
      | null;

    const first = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined;
    return first ?? problem?.detail ?? problem?.title ?? fallback;
  }

  formatDate(value: string): string {
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleDateString('fr-FR');
  }
}
