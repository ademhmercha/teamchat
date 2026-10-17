import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ConnectivityService } from '../pwa/connectivity.service';
import { AuthService } from './auth.service';

const AUTH_ENDPOINTS = ['/auth/login', '/auth/google', '/auth/refresh', '/auth/logout'];

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const connectivity = inject(ConnectivityService);

  const isApi = request.url.startsWith(environment.apiBaseUrl);
  const isAuthEndpoint = AUTH_ENDPOINTS.some((path) => request.url.includes(path));

  let outgoing = request;
  if (isApi) {
    outgoing = request.clone({ withCredentials: true });
    const token = auth.accessToken();
    if (token && !isAuthEndpoint) {
      outgoing = outgoing.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
    }
  }

  return next(outgoing).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 0) {
        connectivity.markOffline();
        if (router.url.startsWith('/app')) {
          void router.navigateByUrl('/offline');
        }
        return throwError(() => error);
      }

      if (error.status !== 401 || !isApi || isAuthEndpoint) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        switchMap(() =>
          next(outgoing.clone({ setHeaders: { Authorization: `Bearer ${auth.accessToken()}` } })),
        ),
        catchError(() => {
          auth.clearSession();
          void router.navigate(['/login']);
          return throwError(() => error);
        }),
      );
    }),
  );
};
