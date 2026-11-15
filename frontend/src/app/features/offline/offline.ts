import { Component, effect, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ConnectivityService } from '../../core/pwa/connectivity.service';
import { IconComponent } from '../../shared/icon/icon';
import { ButtonComponent } from '../../shared/ui/button';

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
