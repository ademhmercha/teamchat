import { Injectable, signal } from '@angular/core';

/**
 * Etat de connectivite reseau (navigator.onLine + evenements online/offline).
 * Expose un signal consommable par la coquille et les gardes de routage.
 */
@Injectable({ providedIn: 'root' })
export class ConnectivityService {
  readonly online = signal(typeof navigator === 'undefined' ? true : navigator.onLine);

  constructor() {
    if (typeof window === 'undefined') {
      return;
    }

    window.addEventListener('online', () => this.online.set(true));
    window.addEventListener('offline', () => this.online.set(false));
  }

  /** Marque l'etat hors ligne (ex. sur une erreur reseau sans evenement `offline`). */
  markOffline(): void {
    this.online.set(false);
  }
}
