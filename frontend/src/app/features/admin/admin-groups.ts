import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminGroupsService } from '../../core/api/admin-groups.service';
import { UsersService } from '../../core/api/users.service';
import { GroupDto } from '../../core/models/conversation.models';
import { UserListItem } from '../../core/models/user.models';
import { initialsOf } from '../../core/utils/display';

@Component({
  selector: 'app-admin-groups',
  imports: [FormsModule],
  templateUrl: './admin-groups.html',
  styleUrl: './admin-groups.scss',
})
export class AdminGroupsComponent {
  private readonly groups = inject(AdminGroupsService);
  private readonly users = inject(UsersService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<GroupDto[]>([]);
  readonly candidates = signal<UserListItem[]>([]);

  readonly editorOpen = signal(false);
  readonly editingGroupId = signal<string | null>(null);
  readonly groupName = signal('');
  readonly selectedIds = signal<string[]>([]);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);

  readonly editorTitle = computed(() =>
    this.editingGroupId() ? 'Modifier les membres' : 'Nouveau groupe',
  );

  constructor() {
    this.load();
    this.loadCandidates();
  }

  openCreate(): void {
    this.editingGroupId.set(null);
    this.groupName.set('');
    this.selectedIds.set([]);
    this.formError.set(null);
    this.editorOpen.set(true);
  }

  openEdit(group: GroupDto): void {
    this.editingGroupId.set(group.id);
    this.groupName.set(group.name);
    this.selectedIds.set(group.members.map((member) => member.userId));
    this.formError.set(null);
    this.editorOpen.set(true);
  }

  closeEditor(): void {
    this.editorOpen.set(false);
  }

  onNameInputEvent(event: Event): void {
    this.groupName.set((event.target as HTMLInputElement).value);
  }

  isSelected(userId: string): boolean {
    return this.selectedIds().includes(userId);
  }

  toggleMember(userId: string): void {
    this.selectedIds.update((ids) =>
      ids.includes(userId) ? ids.filter((id) => id !== userId) : [...ids, userId],
    );
  }

  save(): void {
    const name = this.groupName().trim();
    const editingGroupId = this.editingGroupId();

    if (!editingGroupId && !name) {
      this.formError.set('Le nom du groupe est obligatoire.');
      return;
    }

    if (this.selectedIds().length < 1) {
      this.formError.set('Sélectionnez au moins un membre.');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    if (editingGroupId) {
      this.groups.updateMembers(editingGroupId, this.selectedIds()).subscribe({
        next: () => this.onSaved(),
        error: (error: HttpErrorResponse) => this.onSaveError(error),
      });
    } else {
      this.groups.createGroup(name, this.selectedIds()).subscribe({
        next: () => this.onSaved(),
        error: (error: HttpErrorResponse) => this.onSaveError(error),
      });
    }
  }

  private onSaved(): void {
    this.saving.set(false);
    this.closeEditor();
    this.load();
  }

  private onSaveError(error: HttpErrorResponse): void {
    this.saving.set(false);
    this.formError.set(this.readErrorMessage(error));
  }

  archive(group: GroupDto): void {
    if (!confirm(`Archiver le groupe « ${group.name} » ?`)) {
      return;
    }

    this.groups.archiveGroup(group.id).subscribe({
      next: () => this.load(),
      error: (error: HttpErrorResponse) => this.error.set(this.readErrorMessage(error)),
    });
  }

  initials(group: GroupDto): string {
    return initialsOf(group.name);
  }

  memberNames(group: GroupDto): string {
    return group.members.map((member) => member.displayName).join(', ');
  }

  initialsOfUser(user: UserListItem): string {
    return initialsOf(user.displayName);
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.groups.getGroups().subscribe({
      next: (groups) => {
        this.items.set(groups);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(this.readErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  private loadCandidates(): void {
    this.users.search(null, 1, 100).subscribe({
      next: (result) => this.candidates.set(result.items.filter((user) => user.isActive)),
      error: () => this.candidates.set([]),
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
