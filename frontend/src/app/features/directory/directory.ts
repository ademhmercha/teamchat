import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { of, Subject } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, finalize, map, startWith, switchMap } from 'rxjs/operators';
import { ConversationsService } from '../../core/api/conversations.service';
import { UsersService } from '../../core/api/users.service';
import { ChatService } from '../../core/chat/chat.service';
import { PagedResult, UserListItem } from '../../core/models/user.models';
import { AvatarComponent } from '../../shared/ui/avatar';
import { BadgeComponent } from '../../shared/ui/badge';
import { ButtonComponent } from '../../shared/ui/button';
import { EmptyStateComponent } from '../../shared/ui/empty-state';
import { IconComponent } from '../../shared/icon/icon';

@Component({
  selector: 'app-directory',
  imports: [
    ReactiveFormsModule,
    IconComponent,
    AvatarComponent,
    BadgeComponent,
    ButtonComponent,
    EmptyStateComponent,
  ],
  templateUrl: './directory.html',
  styleUrl: './directory.scss',
})
export class DirectoryComponent {
  private readonly users = inject(UsersService);
  private readonly conversations = inject(ConversationsService);
  private readonly chat = inject(ChatService);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly route = inject(ActivatedRoute);

  private readonly reload$ = new Subject<void>();

  readonly pageSize = 20;
  readonly skeletons = Array.from({ length: 6 });
  readonly searchInput = this.formBuilder.control('');
  readonly search = signal('');
  readonly page = signal(1);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<PagedResult<UserListItem> | null>(null);
  readonly startingId = signal<string | null>(null);

  private readonly presence = this.chat.presence;

  readonly totalPages = computed(() => {
    const total = this.result()?.totalCount ?? 0;
    return Math.max(1, Math.ceil(total / this.pageSize));
  });

  readonly hasQuery = computed(() => this.search().length > 0);

  constructor() {
    const initialQuery = this.route.snapshot.queryParamMap.get('q');
    if (initialQuery) {
      this.searchInput.setValue(initialQuery, { emitEvent: false });
      this.search.set(initialQuery.trim());
    }

    this.searchInput.valueChanges
      .pipe(
        debounceTime(300),
        map((value) => value.trim()),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe((term) => {
        this.search.set(term);
        this.page.set(1);
        this.reload$.next();
      });

    this.reload$
      .pipe(
        startWith(void 0),
        switchMap(() => {
          this.loading.set(true);
          this.error.set(null);

          return this.users.search(this.search() || null, this.page(), this.pageSize).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(this.readErrorMessage(error));
              return of(null);
            }),
            finalize(() => this.loading.set(false)),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        if (result) {
          this.result.set(result);
        }
      });
  }

  isOnline(user: UserListItem): boolean {
    return this.presence()[user.id] === true;
  }

  startConversation(user: UserListItem): void {
    if (!user.isActive || this.startingId() !== null) {
      return;
    }

    this.startingId.set(user.id);
    this.conversations
      .createDirect(user.id)
      .pipe(finalize(() => this.startingId.set(null)))
      .subscribe({
        next: (conversation) => void this.router.navigate(['/conversations', conversation.id]),
        error: (error: HttpErrorResponse) => this.error.set(this.readErrorMessage(error)),
      });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.page()) {
      return;
    }

    this.page.set(page);
    this.reload$.next();
  }

  private readErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'Serveur injoignable.';
    }

    const problem = error.error as { detail?: string; title?: string } | null;
    return problem?.detail ?? problem?.title ?? 'Erreur lors du chargement.';
  }
}
