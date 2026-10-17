import { Routes } from '@angular/router';
import { adminGuard, authGuard, guestGuard, superAdminGuard } from './core/auth/auth.guard';
import { offlineGuard } from './core/pwa/offline.guard';

export const routes: Routes = [
  {
    path: 'offline',
    loadComponent: () => import('./features/offline/offline').then((m) => m.OfflineComponent),
  },
  {
    path: '',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/landing/landing').then((m) => m.LandingComponent),
  },
  {
    path: 'register',
    canActivate: [offlineGuard, guestGuard],
    loadComponent: () => import('./features/register/register').then((m) => m.RegisterComponent),
  },
  {
    path: 'login',
    canActivate: [offlineGuard, guestGuard],
    loadComponent: () => import('./features/login/login').then((m) => m.LoginComponent),
  },
  {
    path: 'forgot-password',
    canActivate: [offlineGuard, guestGuard],
    loadComponent: () =>
      import('./features/auth/forgot-password').then((m) => m.ForgotPasswordComponent),
  },
  {
    path: 'reset-password',
    canActivate: [offlineGuard, guestGuard],
    loadComponent: () => import('./features/auth/set-password').then((m) => m.SetPasswordComponent),
  },
  {
    path: 'accept-invite',
    canActivate: [offlineGuard, guestGuard],
    loadComponent: () => import('./features/auth/set-password').then((m) => m.SetPasswordComponent),
  },
  {
    path: 'app',
    canActivate: [offlineGuard, authGuard],
    loadComponent: () => import('./layout/shell/shell').then((m) => m.ShellComponent),
    children: [
      { path: '', redirectTo: 'directory', pathMatch: 'full' },
      {
        path: 'directory',
        loadComponent: () => import('./features/directory/directory').then((m) => m.DirectoryComponent),
      },
      {
        path: 'account',
        loadComponent: () => import('./features/account/account').then((m) => m.AccountComponent),
      },
      {
        path: 'conversations',
        loadComponent: () =>
          import('./features/conversations/conversations-layout').then(
            (m) => m.ConversationsLayoutComponent,
          ),
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./features/conversations/conversations-empty').then(
                (m) => m.ConversationsEmptyComponent,
              ),
          },
          {
            path: ':id',
            loadComponent: () =>
              import('./features/conversations/thread').then((m) => m.ThreadComponent),
          },
        ],
      },
      {
        path: 'admin',
        canActivate: [adminGuard],
        loadComponent: () =>
          import('./features/admin/admin-layout').then((m) => m.AdminLayoutComponent),
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./features/admin/admin-home').then((m) => m.AdminHomeComponent),
          },
          {
            path: 'users',
            loadComponent: () =>
              import('./features/admin/admin-users').then((m) => m.AdminUsersComponent),
          },
          {
            path: 'groups',
            loadComponent: () =>
              import('./features/admin/admin-groups').then((m) => m.AdminGroupsComponent),
          },
        ],
      },
      {
        path: 'platform',
        canActivate: [superAdminGuard],
        loadComponent: () =>
          import('./features/platform/platform').then((m) => m.PlatformComponent),
      },
    ],
  },
  // Redirections des anciennes URL (avant le prefixe /app) conservees en signets.
  { path: 'directory', redirectTo: '/app/directory' },
  { path: 'account', redirectTo: '/app/account' },
  { path: 'admin', redirectTo: '/app/admin' },
  { path: 'conversations', redirectTo: '/app/conversations' },
  {
    path: 'conversations/:id',
    redirectTo: ({ params }) => `/app/conversations/${params['id']}`,
  },
  { path: '**', redirectTo: '' },
];
