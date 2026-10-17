import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ConnectivityService } from './connectivity.service';

/**
 * Redirige vers la page hors-ligne quand l'application est ouverte sans reseau.
 * Pose uniquement sur les routes racine : une fois la coquille chargee, la
 * navigation interne n'est pas interrompue (le bandeau hors-ligne prend le relais).
 */
export const offlineGuard: CanActivateFn = () => {
  const connectivity = inject(ConnectivityService);
  const router = inject(Router);

  return connectivity.online() ? true : router.parseUrl('/offline');
};
