import { Injectable, inject } from '@angular/core';
import { IdentityService } from '@orangepuff/portal-frontend-shared';
import { PORTAL_SHELL_CONFIG } from '../config/portal-shell-config';

/**
 * Shell-specific wrapper around the shared IdentityService — adds login() (the OAuth
 * redirect, only the shell ever needs to trigger this) and re-exposes the shared
 * currentUser/isAuthenticated/checked/checkSession/logout so existing call sites don't
 * need to change.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly identityService = inject(IdentityService);
  private readonly config = inject(PORTAL_SHELL_CONFIG);

  readonly currentUser = this.identityService.currentUser;
  readonly isAuthenticated = this.identityService.isAuthenticated;
  readonly checked = this.identityService.checked;

  checkSession() {
    return this.identityService.checkSession();
  }

  login(returnUrl: string): void {
    // Absolute URL to the Bff host's real origin, not a relative path through the dev-server proxy.
    // /bff/login sets an OAuth correlation cookie that must round-trip back on /signin-google,
    // and Google redirects straight to that path on the host's own origin — if /bff/login itself
    // went through the proxy (localhost:5599 or wherever the consumer runs), the cookie would be
    // scoped to the wrong origin and the correlation check on the way back would fail.
    window.location.href = `${this.config.bffOrigin}/bff/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  logout() {
    return this.identityService.logout();
  }
}
