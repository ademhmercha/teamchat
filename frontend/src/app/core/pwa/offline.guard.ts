import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ConnectivityService } from './connectivity.service';

export const offlineGuard: CanActivateFn = () => {
  const connectivity = inject(ConnectivityService);
  const router = inject(Router);

  return connectivity.online() ? true : router.parseUrl('/offline');
};
