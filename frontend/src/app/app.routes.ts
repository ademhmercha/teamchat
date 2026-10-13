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
          import('./features/conversations/conversations-list').then((m) => m.ConversationsListComponent),
      },
      {
        path: 'conversations/:id',
        loadComponent: () =>
          import('./features/conversations/thread').then((m) => m.ThreadComponent),
      },
      {
        path: 'admin',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/admin/admin-home').then((m) => m.AdminHomeComponent),
      },
      {
        path: 'admin/users',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/admin/admin-users').then((m) => m.AdminUsersComponent),
      },
      {
        path: 'admin/groups',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/admin/admin-groups').then((m) => m.AdminGroupsComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
