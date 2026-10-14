import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { of, Subject } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, finalize, map, startWith, switchMap } from 'rxjs/operators';
import { UsersService } from '../../core/api/users.service';
import { PagedResult, UserListItem } from '../../core/models/user.models';
import { IconComponent } from '../../shared/icon/icon';

@Component({
  selector: 'app-directory',
  imports: [ReactiveFormsModule, IconComponent],
  templateUrl: './directory.html',
  styleUrl: './directory.scss',
})
export class DirectoryComponent {
  private readonly users = inject(UsersService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly route = inject(ActivatedRoute);

  private readonly reload$ = new Subject<void>();

  readonly pageSize = 20;
  readonly searchInput = this.formBuilder.control('');
  readonly search = signal('');
  readonly page = signal(1);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<PagedResult<UserListItem> | null>(null);

  readonly totalPages = computed(() => {
    const total = this.result()?.totalCount ?? 0;
    return Math.max(1, Math.ceil(total / this.pageSize));
  });

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

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.page()) {
      return;
    }

    this.page.set(page);
    this.reload$.next();
  }

  initials(user: UserListItem): string {
    return user.displayName
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('');
  }

  private readErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'Serveur injoignable.';
    }

    const problem = error.error as { detail?: string; title?: string } | null;
    return problem?.detail ?? problem?.title ?? 'Erreur lors du chargement.';
  }
}
