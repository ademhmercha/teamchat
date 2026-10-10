import { Routes } from '@angular/router';
import { adminGuard, authGuard, guestGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/login/login').then((m) => m.LoginComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell/shell').then((m) => m.ShellComponent),
    children: [
      { path: '', redirectTo: 'directory', pathMatch: 'full' },
      {
        path: 'directory',
        loadComponent: () => import('./features/directory/directory').then((m) => m.DirectoryComponent),
      },
      {
        path: 'admin',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/admin/admin-home').then((m) => m.AdminHomeComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
