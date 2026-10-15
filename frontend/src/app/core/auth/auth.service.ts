import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, finalize, map, shareReplay, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AuthResponse, UserProfile } from '../models/user.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/auth`;

  private readonly _accessToken = signal<string | null>(null);
  private readonly _user = signal<UserProfile | null>(null);
  private refreshInFlight$: Observable<AuthResponse> | null = null;

  readonly accessToken = this._accessToken.asReadonly();
  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => this._accessToken() !== null);
  readonly isAdmin = computed(() => this._user()?.role === 'Admin');

  login(email: string, password: string): Observable<UserProfile | null> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/login`, { email, password }, { withCredentials: true })
      .pipe(
        tap((response) => this.setSession(response)),
        map((response) => response.user),
      );
  }

  loginWithGoogle(idToken: string): Observable<UserProfile | null> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/google`, { idToken }, { withCredentials: true })
      .pipe(
        tap((response) => this.setSession(response)),
        map((response) => response.user),
      );
  }

  refresh(): Observable<AuthResponse> {
    if (!this.refreshInFlight$) {
      this.refreshInFlight$ = this.http
        .post<AuthResponse>(`${this.baseUrl}/refresh`, {}, { withCredentials: true })
        .pipe(
          tap((response) => this.setSession(response)),
          finalize(() => {
            this.refreshInFlight$ = null;
          }),
          shareReplay({ bufferSize: 1, refCount: false }),
        );
    }

    return this.refreshInFlight$;
  }

  ensureSession(): Observable<boolean> {
    if (this._accessToken()) {
      return of(true);
    }

    return this.refresh().pipe(
      map(() => true),
      catchError(() => {
        this.clearSession();
        return of(false);
      }),
    );
  }

  forgotPassword(email: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/forgot-password`, { email });
  }

  resetPassword(userId: string, token: string, password: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/reset-password`, { userId, token, password });
  }

  acceptInvite(userId: string, token: string, password: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/accept-invite`, { userId, token, password });
  }

  logout(): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/logout`, {}, { withCredentials: true })
      .pipe(
        catchError(() => of(void 0)),
        tap(() => this.clearSession()),
      );
  }

  logoutAll(): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/logout-all`, {}, { withCredentials: true })
      .pipe(
        catchError(() => of(void 0)),
        tap(() => this.clearSession()),
      );
  }

  updateProfile(displayName: string): Observable<UserProfile> {
    return this.http
      .patch<UserProfile>(`${environment.apiBaseUrl}/api/users/me`, { displayName })
      .pipe(tap((user) => this._user.set(user)));
  }

  uploadAvatar(file: File): Observable<UserProfile> {
    const form = new FormData();
    form.append('file', file);
    return this.http
      .post<UserProfile>(`${environment.apiBaseUrl}/api/users/me/avatar`, form)
      .pipe(tap((user) => this._user.set(user)));
  }

  removeAvatar(): Observable<void> {
    return this.http
      .delete<void>(`${environment.apiBaseUrl}/api/users/me/avatar`)
      .pipe(tap(() => this._user.update((user) => (user ? { ...user, avatarUrl: null } : user))));
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/change-password`, { currentPassword, newPassword });
  }

  clearSession(): void {
    this._accessToken.set(null);
    this._user.set(null);
  }

  private setSession(response: AuthResponse): void {
    this._accessToken.set(response.accessToken);
    this._user.set(response.user);
  }
}
