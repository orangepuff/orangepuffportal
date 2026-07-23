import { EnvironmentProviders, InjectionToken, makeEnvironmentProviders } from '@angular/core';

export interface PortalShellConfig {
  /** Header brand string and Landing's default title. */
  appName: string;
  /**
   * Absolute origin of the OrangepuffPortal Bff, e.g. 'https://localhost:7100'.
   * Must be absolute (not a relative/proxied path) — the OAuth correlation cookie set by
   * /bff/login has to round-trip to the real origin Google redirects back to.
   */
  bffOrigin: string;
  /** Home's iframe src. Unset renders the "no body app configured" placeholder. */
  bodyAppUrl?: string;
  landing?: {
    tagline?: string;
    heroImageUrl?: string;
  };
}

export const PORTAL_SHELL_CONFIG = new InjectionToken<PortalShellConfig>('PORTAL_SHELL_CONFIG');

export function providePortalShell(config: PortalShellConfig): EnvironmentProviders {
  return makeEnvironmentProviders([{ provide: PORTAL_SHELL_CONFIG, useValue: config }]);
}
