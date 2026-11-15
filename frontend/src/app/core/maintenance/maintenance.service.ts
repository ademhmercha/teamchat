import { DOCUMENT } from '@angular/common';
import { Injectable, InjectionToken, inject } from '@angular/core';
import {
  REDIRECT_GUARD_KEY,
  buildMaintenanceUrl,
  canRedirect,
  isHubMaintenanceError,
} from './maintenance';

/** Navigation du navigateur, remplacable dans les tests (jsdom ne sait pas naviguer). */
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

  /**
   * Redirige vers /maintenance.html?from=<URL courante>. Renvoie false (sans rien faire) si on est deja sur
   * la page de veille ou si une redirection a eu lieu il y a moins de 30 s (anti-boucle).
   */
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

  /** A appeler quand le client SignalR n'arrive pas a se connecter. */
  handleHubError(error: unknown): boolean {
    const online = this.document.defaultView?.navigator.onLine ?? true;
    return isHubMaintenanceError(error, online) && this.redirect();
  }

  private lastRedirectAt(): number | null {
    try {
      const value = this.document.defaultView?.sessionStorage.getItem(REDIRECT_GUARD_KEY);
      return value ? Number(value) : null;
    } catch {
      return null; // stockage indisponible (navigation privee) : la garde sur le chemin suffit
    }
  }

  private remember(now: number): void {
    try {
      this.document.defaultView?.sessionStorage.setItem(REDIRECT_GUARD_KEY, String(now));
    } catch {
      // ignore
    }
  }
}
