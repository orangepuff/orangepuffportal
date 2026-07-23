import { Route } from '@angular/router';
import { authGuard } from '../lib/auth/auth.guard';
import { adminGuard } from '../lib/auth/admin.guard';

export const PORTAL_SHELL_ROUTES: Route[] = [
  {
    path: '',
    loadComponent: () => import('../lib/landing/landing').then((m) => m.Landing)
  },
  {
    path: 'auth-error',
    loadComponent: () => import('../lib/auth-error/auth-error-page').then((m) => m.AuthErrorPage)
  },
  {
    path: 'unauthorized',
    loadComponent: () => import('../lib/unauthorized/unauthorized-page').then((m) => m.UnauthorizedPage)
  },
  {
    path: 'home',
    canActivate: [authGuard],
    loadComponent: () => import('../lib/home/home').then((m) => m.Home)
  },
  {
    path: 'admin/users',
    canActivate: [adminGuard],
    loadComponent: () => import('../lib/admin/users/user-list/user-list').then((m) => m.UserList)
  },
  {
    path: 'admin/security-rule-categories',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('../lib/admin/security-rule-categories/security-rule-category-list/security-rule-category-list').then(
        (m) => m.SecurityRuleCategoryList
      )
  },
  {
    path: 'admin/security-rule-items',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('../lib/admin/security-rule-items/security-rule-item-list/security-rule-item-list').then(
        (m) => m.SecurityRuleItemList
      )
  },
  {
    path: 'admin/themes',
    canActivate: [adminGuard],
    loadComponent: () => import('../lib/admin/themes/theme-page').then((m) => m.ThemePage)
  },
  {
    path: 'settings',
    canActivate: [authGuard],
    loadComponent: () => import('../lib/settings/settings-page').then((m) => m.SettingsPage)
  }
];
