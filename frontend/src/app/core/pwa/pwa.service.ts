import { Injectable, inject, signal } from '@angular/core';
import { SwUpdate } from '@angular/service-worker';

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

/**
 * Fonctionnalites PWA : invite d'installation (beforeinstallprompt) et detection
 * d'une nouvelle version du service worker (mise a jour disponible).
 */
@Injectable({ providedIn: 'root' })
export class PwaService {
  private readonly swUpdate = inject(SwUpdate);

  private deferredPrompt: BeforeInstallPromptEvent | null = null;

  /** Le navigateur propose-t-il d'installer l'application ? */
  readonly canInstall = signal(false);
  /** Une nouvelle version est-elle prete (a activer par l'utilisateur) ? */
  readonly updateAvailable = signal(false);

  constructor() {
    if (typeof window !== 'undefined') {
      window.addEventListener('beforeinstallprompt', (event) => {
        event.preventDefault();
        this.deferredPrompt = event as BeforeInstallPromptEvent;
        this.canInstall.set(true);
      });

      window.addEventListener('appinstalled', () => {
        this.deferredPrompt = null;
        this.canInstall.set(false);
      });
    }

    if (this.swUpdate.isEnabled) {
      this.swUpdate.versionUpdates.subscribe((event) => {
        if (event.type === 'VERSION_READY') {
          this.updateAvailable.set(true);
        }
      });
      this.swUpdate.checkForUpdate().catch(() => undefined);
    }
  }

  /** Affiche l'invite d'installation native (si disponible). */
  async install(): Promise<void> {
    const prompt = this.deferredPrompt;
    if (!prompt) {
      return;
    }

    await prompt.prompt();
    await prompt.userChoice.catch(() => undefined);
    this.deferredPrompt = null;
    this.canInstall.set(false);
  }

  /** Active la nouvelle version et recharge la page. */
  reloadForUpdate(): void {
    this.swUpdate
      .activateUpdate()
      .then(() => document.location.reload())
      .catch(() => document.location.reload());
  }
}
