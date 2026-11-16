import { DOCUMENT } from '@angular/common';
import { Injectable, InjectionToken, inject } from '@angular/core';
import {
  REDIRECT_GUARD_KEY,
  buildMaintenanceUrl,
  canRedirect,
  isHubMaintenanceError,
} from './maintenance';

export const MAINTENANCE_NAVIGATE = new InjectionToken<(url: string) => void>('MAINTENANCE_NAVIGATE', {
  providedIn: 'root',
  factory: () => {
    const document = inject(DOCUMENT);
    return (url: string) => document.defaultView?.location.assign(url);
  },
});

@Injectable({ providedIn: 'root' })
export class MaintenanceService {
  private readonly document = inject(DOCUMENT);
  private readonly navigate = inject(MAINTENANCE_NAVIGATE);

  redirect(): boolean {
    const location = this.document.defaultView?.location;
    if (!location) {
      return false;
    }

    const now = Date.now();
    if (!canRedirect(location.pathname, this.lastRedirectAt(), now)) {
      return false;
    }

    this.remember(now);
    this.navigate(buildMaintenanceUrl(`${location.pathname}${location.search}${location.hash}`));
    return true;
  }

  handleHubError(error: unknown): boolean {
    const online = this.document.defaultView?.navigator.onLine ?? true;
    return isHubMaintenanceError(error, online) && this.redirect();
  }

  private lastRedirectAt(): number | null {
    try {
      const value = this.document.defaultView?.sessionStorage.getItem(REDIRECT_GUARD_KEY);
      return value ? Number(value) : null;
    } catch {
      return null;
    }
  }

  private remember(now: number): void {
    try {
      this.document.defaultView?.sessionStorage.setItem(REDIRECT_GUARD_KEY, String(now));
    } catch {
    }
  }
}
