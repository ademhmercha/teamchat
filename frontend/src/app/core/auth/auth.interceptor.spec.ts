import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { MaintenanceService } from '../maintenance/maintenance.service';
import { ConnectivityService } from '../pwa/connectivity.service';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor : redirection vers la page de veille', () => {
  const api = `${environment.apiBaseUrl}/api/users`;

  let http: HttpClient;
  let backend: HttpTestingController;
  let redirect: ReturnType<typeof vi.fn>;
  let online: ReturnType<typeof signal<boolean>>;
  let router: { url: string; navigateByUrl: ReturnType<typeof vi.fn>; navigate: ReturnType<typeof vi.fn> };
  let auth: { accessToken: ReturnType<typeof vi.fn>; refresh: ReturnType<typeof vi.fn>; clearSession: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    redirect = vi.fn(() => true);
    online = signal(true);
    router = { url: '/app/conversations', navigateByUrl: vi.fn(), navigate: vi.fn() };
    auth = { accessToken: vi.fn(() => 'token'), refresh: vi.fn(() => of(void 0)), clearSession: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
        { provide: ConnectivityService, useValue: { online, markOffline: () => online.set(false) } },
        { provide: MaintenanceService, useValue: { redirect } },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  function fail(url: string, status: number): void {
    http.get(url).subscribe({ error: () => undefined });
    backend.expectOne(url).flush('x', { status, statusText: 'err' });
  }

  it.each([502, 503, 504])('redirige sur un %i venant de /api/*', (status) => {
    fail(api, status);
    expect(redirect).toHaveBeenCalledTimes(1);
  });

  it.each([400, 401, 403, 404, 409, 500])('ne redirige jamais sur un %i', (status) => {
    http.get(api).subscribe({ error: () => undefined });
    backend.expectOne(api).flush('x', { status, statusText: 'err' });
    if (status === 401) {
      backend.match(api).forEach((request) => request.flush('x', { status: 401, statusText: 'err' }));
    }
    expect(redirect).not.toHaveBeenCalled();
  });

  it('redirige sur une erreur reseau (0) quand le navigateur est en ligne', () => {
    http.get(api).subscribe({ error: () => undefined });
    backend.expectOne(api).error(new ProgressEvent('error'));
    expect(redirect).toHaveBeenCalledTimes(1);
  });

  it('ne redirige pas sur une erreur reseau (0) hors ligne : page /offline', () => {
    online.set(false);
    http.get(api).subscribe({ error: () => undefined });
    backend.expectOne(api).error(new ProgressEvent('error'));
    expect(redirect).not.toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/offline');
  });

  it('ne redirige pas pour une URL qui n est pas /api/* ni /hubs/*', () => {
    fail('https://lh3.googleusercontent.com/a/photo', 503);
    fail(`${environment.apiBaseUrl}/assets/x.json`, 503);
    expect(redirect).not.toHaveBeenCalled();
  });

  it('retombe sur le comportement normal quand la garde anti-boucle refuse la redirection', () => {
    redirect.mockReturnValue(false);
    online.set(true);
    http.get(api).subscribe({ error: () => undefined });
    backend.expectOne(api).error(new ProgressEvent('error'));
    expect(redirect).toHaveBeenCalledTimes(1);
    expect(router.navigateByUrl).toHaveBeenCalledWith('/offline');
  });

  it('n envoie jamais le jeton a une URL externe', () => {
    http.get('https://lh3.googleusercontent.com/a/photo').subscribe();
    const request = backend.expectOne('https://lh3.googleusercontent.com/a/photo');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush('');
  });

  it('ajoute le jeton aux appels de l API', () => {
    http.get(api).subscribe();
    const request = backend.expectOne(api);
    expect(request.request.headers.get('Authorization')).toBe('Bearer token');
    request.flush('');
  });
});
