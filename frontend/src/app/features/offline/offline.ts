import { Component, effect, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ConnectivityService } from '../../core/pwa/connectivity.service';
import { IconComponent } from '../../shared/icon/icon';
import { ButtonComponent } from '../../shared/ui/button';

/**
 * Page de repli affichee lorsque l'application est ouverte sans reseau.
 * Retourne automatiquement a l'application des que la connexion est retablie.
 */
@Component({
  selector: 'app-offline',
  imports: [IconComponent, ButtonComponent],
  templateUrl: './offline.html',
  styleUrl: './offline.scss',
})
export class OfflineComponent {
  private readonly router = inject(Router);
  private readonly connectivity = inject(ConnectivityService);

  readonly online = this.connectivity.online;

  constructor() {
    effect(() => {
      if (this.online()) {
        void this.router.navigateByUrl('/app');
      }
    });
  }

  retry(): void {
    if (this.connectivity.online()) {
      void this.router.navigateByUrl('/app');
    } else {
      document.location.reload();
    }
  }
}
