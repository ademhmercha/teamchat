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
    ],
  },
  { path: '**', redirectTo: '' },
];
