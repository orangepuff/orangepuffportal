# OrangepuffPortal

A reusable Identity/Bff-auth SDK, extracted from [OCRWeb](https://github.com/orangepuff/ocrweb)'s
`OCRWeb.Shared`/`OCRWeb.Identity`/`OCRWeb.Bff` auth surface. Any "body app" references
`OrangepuffPortal.Host` instead of cloning a template repo, gets identical centrally-controlled
Identity/security behavior for free, and updates by bumping a package version.

## Design

- **Single in-process host.** Unlike OCRWeb's current split of a separate API + Bff process
  talking over an internal-token-secured HTTP hop, `OrangepuffPortal.Host` runs everything —
  Identity's MediatR handlers, cookie + Google OAuth, the `/bff/*` routes — in one process. A
  body app references one package (`OrangepuffPortal.Host`) and gets all of it.
- **Identity/security rules are fixed by the SDK, not overridable per body app.** Only
  deployment-level config varies (connection string, OAuth secrets, `Portal:AppName`). If a rule
  needs to change, it changes here and every consuming body app picks it up on the next version
  bump — no drift between body apps.
- **`IPortalModule`** is the generic migration/seed contract. `OrangepuffPortal.Host` discovers
  every registered `IPortalModule` (Identity's own, plus any body-app module that registers
  itself the same way) and migrates/seeds them all, in DI-registration order, gated by a single
  `DoMigration` config flag.

## Packages (versioned and released together)

| Package | Contents |
|---|---|
| `OrangepuffPortal.Shared` | DDD primitives (`ICurrentUser`, `IAuditable`, `AuditableEntity`), `IPortalModule`, design-time EF factory base |
| `OrangepuffPortal.Identity.Contract` | Cross-module DTOs |
| `OrangepuffPortal.Identity` | Identity bounded context — users, security rules/permissions, Google-provisioning, `identity` schema |
| `OrangepuffPortal.Bff` | Cookie + Google OAuth, `IIdentityGateway` (in-process facade over Identity's application layer), `/bff/*` route handlers |
| `OrangepuffPortal.Host` | Umbrella `AddOrangepuffPortal()`/`MapOrangepuffPortal()`/`MigratePortalModulesAsync()`, the real `ICurrentUser`, transaction-logger auto-stamping |
| `@orangepuff/portal-frontend-shared` (npm) | Angular library — `IdentityService` (`/bff/me`, `/bff/me/permissions`), `Avatar`, `ConfirmDialog`, `theme.scss` (M3 baseline). Same-origin, cookie-authenticated, zero backend-specific coupling — usable by the shell below and by any body app embedded in its iframe |
| `@orangepuff/portal-frontend` (npm) | Angular library — the "portal shell": Landing (Sign in with Google), `/home` (iframes a separate body app, e.g. an OCR web app, via configurable `bodyAppUrl`), session-aware header, admin CRUD UI for Users/SecurityRuleCategories/SecurityRuleItems, Settings, auth/admin guards. Exports `PORTAL_SHELL_ROUTES` (spread into a consumer's own routes), `PortalShell` (root component), and `providePortalShell(config)` for per-deployment config (`appName`, `bffOrigin`, `bodyAppUrl`, landing content) — the frontend analogue of `AddOrangepuffPortal(configuration)` |

## Consuming from a body app

```csharp
builder.Services.AddOrangepuffPortal(builder.Configuration);
// ...body app's own module registrations (its own IPortalModule implementations)...

var app = builder.Build();
await app.MigratePortalModulesAsync();

app.UseAuthentication();
app.UseAuthorization();
app.MapOrangepuffPortal();
// ...body app's own endpoint mapping...

app.Run();
```

Required config: `ConnectionStrings:Portal`, `Portal:AppName`, `Authentication:Google:ClientId`/
`ClientSecret`, `Frontend:BaseUrl`, `Seed:AdminUsername`/`AdminEmail`/`AdminDisplayName`/
`AdminPassword`, `DoMigration`. The host must also call `AddDiagnostics()`/`AddDiagnosticsAspNetCore()`
itself (see [DiagnosticLog](https://github.com/orangepuff/DiagnosticLog)) — Identity's command
handlers have a hard `ITransactionLogger` dependency.

`Frontend:BaseUrl` must be the exact absolute origin your Angular app (below) actually serves from —
it's where the Bff redirects the browser back to after a successful Google sign-in.

## Configuring Google sign-in

**1. Configure the OAuth consent screen** (Google Cloud Console → APIs & Services → OAuth consent
screen — only needed once per Google Cloud project, before a client ID can be created):

- **User Type**: `External` is fine for local dev/testing.
- **Scopes**: add `email` and `profile` (the two scopes `AddGoogle()` requests — see step 3).
- **Test users**: while the app is in "Testing" publishing status, only accounts listed here can
  complete sign-in — add every Google account you'll use to log in locally. Move to "In production"
  (requires Google's verification for sensitive scopes, not needed for `email`/`profile`) once you
  want unlisted accounts to sign in too.

**2. Create an OAuth client** (same Console section → Credentials → Create Credentials → OAuth
client ID → Web application). Create one client per environment that needs to sign in (e.g. one for
local dev, one per deployed environment) — each has its own origin/redirect URI and its own
Client ID/Secret pair:

- **Authorized JavaScript origin**: the Bff's origin, e.g. `https://localhost:7100`
- **Authorized redirect URI**: the Bff's origin + `/signin-google`, e.g. `https://localhost:7100/signin-google`
  — this is ASP.NET Core's default Google-handler callback path; the code doesn't override it, so it's
  always `/signin-google` off whatever origin the Bff serves from, never a `/bff/*`-prefixed path.
  If the Bff's port changes (see [Configuring ports](#configuring-ports) below), this URI — and the
  origin above — must be updated to match, both in Google Cloud Console and in your backend config.

Copy the generated Client ID and Client Secret into your config as `Authentication:Google:ClientId`/
`ClientSecret` (env var form, e.g. via Docker Compose: `Authentication__Google__ClientId`/
`Authentication__Google__ClientSecret` — see `.env.example` at the repo root for the sample host's
own `GOOGLE_CLIENT_ID`/`GOOGLE_CLIENT_SECRET` vars).

**3. Sign-in outcomes** — `AddGoogle()` requests the `email` and `profile` scopes and maps Google's
`email_verified` claim; `ProvisionGoogleUserCommandHandler` then decides what happens:

| Condition | Result |
|---|---|
| Google identity already linked to a user | Signed in |
| Email not verified with Google | Rejected — `/auth-error?reason=email_not_verified` |
| Email matches an existing user (first Google sign-in for them) | Auto-links and signs in |
| No existing user, and `Identity:AllowSelfRegistrationViaGoogle` is `false` | Rejected — `/auth-error?reason=registration_disabled` |
| No existing user, self-registration allowed (default) | Provisions a new user (`google_<email-local-part>`) and signs in |

`Identity:AllowSelfRegistrationViaGoogle` defaults to `true` if unset — set it to `false` to require
an admin to pre-create every user (via the admin Users UI/`/bff/admin/users`) before they can sign in.

Any other OAuth failure redirects with `reason=unknown` — the frontend's `auth-error-page` only has
copy for the two reasons above, falling back to a generic message for anything else.

## Configuring ports

There is no single "port setting" — the backend (Bff) port and frontend port each come from a
different file depending on how you run them, and several *other* config values have to be kept in
sync with whichever port is actually in use.

**Backend port**, depending on how you run it:

| Run method | Where the port is set | Sample host's actual values |
|---|---|---|
| `docker compose up` | `docker-compose.yml`'s `ports:` mapping (`host:container`); container always listens on `8080`/`8081` via `ASPNETCORE_URLS` | `5100:8080` (HTTP), `7100:8081` (HTTPS) |
| `dotnet run` / IDE | the project's `Properties/launchSettings.json` → `applicationUrl` | `OrangepuffPortal.SampleHost`: `57606` (HTTP) / `57605` (HTTPS) |

These two are independent and **do not match by default** in this repo (Docker uses 7100, `dotnet run`
uses 57605) — pick one way of running the host and make sure every other value below points at *that*
one's HTTPS port, not the other.

**Frontend port** — `angular.json`'s `projects.*.architect.serve.options.port` (e.g. `5599` for
`OrangepuffPortal.SampleHost.Frontend`). Change it there (or pass `ng serve --port <n>`) if it
conflicts with something else on your machine.

**Values that must stay in sync with whichever backend port you're actually running:**

- `proxy.conf.json`'s `target` — the frontend dev-server proxies same-origin `/bff/*` calls here.
- `providePortalShell({ bffOrigin: ... })` (frontend) — used for the one non-proxied, full-page
  navigation (`AuthService.login()`); see the "one absolute-URL gotcha" note below.
- `Frontend:BaseUrl` (backend config, e.g. `appsettings.Development.json`) — must equal the
  frontend's real origin (its port from `angular.json` above), not the backend's own port; this is
  where the Bff redirects the browser back to post-login.
- The Google Cloud Console OAuth client's authorized origin/redirect URI (see
  [Configuring Google sign-in](#configuring-google-sign-in) above) — must equal the backend's origin.

Concretely, for the sample apps running via `docker compose up` (backend) + `ng serve` (frontend):
backend HTTPS is `https://localhost:7100`, frontend is `https://localhost:5599`, so
`proxy.conf.json`/`bffOrigin` → `7100`, `Frontend:BaseUrl` → `5599`, and the Google OAuth client is
registered against `7100`. If you instead run the backend via `dotnet run` (port `57605`), all three
of those need to move from `7100` to `57605` together.

## Consuming the frontend shell from a body app

An Angular 22 (standalone components) app gets the whole portal shell — Landing, `/home`
(iframes a separate body app via `bodyAppUrl`), session-aware header, admin CRUD UI, Settings,
auth/admin guards — from two packages, the frontend analogue of `AddOrangepuffPortal()` above.

**Install** (until published to npm, depend on the packed tarballs — see
`samples/OrangepuffPortal.SampleHost.Frontend/package.json` for the exact `file:` paths this repo
uses locally):

```powershell
npm install @orangepuff/portal-frontend @orangepuff/portal-frontend-shared
```

**Wire it up** — `app.config.ts`:

```ts
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { providePortalShell } from '@orangepuff/portal-frontend';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withFetch()),
    provideAnimationsAsync(),
    providePortalShell({
      appName: 'Your App Name',                    // header brand + Landing's default title
      bffOrigin: 'https://your-bff-host:port',      // absolute — see the cookie-correctness note below
      bodyAppUrl: 'https://your-body-app:port',     // optional — omit to show the placeholder
      landing: { tagline: '...', heroImageUrl: '...' } // both optional
    })
  ]
};
```

`app.routes.ts` — spread `PORTAL_SHELL_ROUTES` in; add your own body-app-specific routes alongside
them if this Angular app also serves the body app rather than embedding a separate one:

```ts
import { Routes } from '@angular/router';
import { PORTAL_SHELL_ROUTES } from '@orangepuff/portal-frontend';

export const routes: Routes = [...PORTAL_SHELL_ROUTES /* , ...your own routes */];
```

`main.ts` — bootstrap `PortalShell` directly (or your own root component that wraps it, if you need
additional global chrome):

```ts
import { bootstrapApplication } from '@angular/platform-browser';
import { PortalShell } from '@orangepuff/portal-frontend';
import { appConfig } from './app/app.config';

bootstrapApplication(PortalShell, appConfig).catch((err) => console.error(err));
```

`index.html` — root tag must match: `<lib-portal-shell></lib-portal-shell>`.

`styles.scss` — pull in the shared M3 theme so your app and the shell match:

```scss
@use '@orangepuff/portal-frontend-shared/theme.scss' as shared-theme;
html { @include shared-theme.apply-theme; }
```

**Dev-server proxy** — `/bff/*` calls are relative, same-origin, cookie-authenticated; proxy them to
your Bff host in dev (`proxy.conf.json`, wired via `angular.json`'s `serve.options.proxyConfig`):

```json
{ "/bff": { "target": "https://your-bff-host:port", "secure": false, "changeOrigin": true } }
```

**The one absolute-URL gotcha (`bffOrigin`):** `AuthService.login()` does a full-page
`window.location.href` navigate to `${bffOrigin}/bff/login?...`, not a relative/proxied call. This
has to be the Bff's *real* origin — Google's redirect after sign-in lands straight back on that
origin, and if the initial hop instead went through the dev-proxy, the OAuth correlation cookie
would be scoped to the wrong origin and the round-trip would fail. Set it to whatever origin the
Bff (the `OrangepuffPortal.Host`-based app from the section above) actually serves HTTPS from, and
make sure that same value matches the backend's `Frontend:BaseUrl` config so the post-login redirect
lands back on your Angular app.

## Running the sample host locally

`samples/OrangepuffPortal.SampleHost` is a thin, unpublished host that exercises the full
`/bff/login` → Google OAuth → `/bff/me` flow without any body app.

```powershell
docker compose up -d --build
```

- Host: http://localhost:5100 / https://localhost:7100

SQL Server is external (not managed by compose) — see `appsettings.Development.json` for the
expected connection string shape, and create the `OrangepuffPortalSample` database first.

## Running the sample frontend locally

`samples/OrangepuffPortal.SampleHost.Frontend` is a thin Angular app proving
`@orangepuff/portal-frontend`/`-shared` work standalone — it's just `PORTAL_SHELL_ROUTES` plus a
`providePortalShell({...})` call pointed at the sample host above, no body app configured.

```powershell
# Build the two library packages first (Frontend.Shared before Frontend — the latter depends on it)
cd src/OrangepuffPortal.Frontend.Shared; npm install; npm run build; cd ../..
cd src/OrangepuffPortal.Frontend; npm install; npm run build; cd ../..

# Then the sample app itself
cd samples/OrangepuffPortal.SampleHost.Frontend
npm install
ng serve
```

- Frontend: https://localhost:5599 (proxies `/bff/*` to the sample host at https://localhost:7100)

Requires the dev cert pair at `certs/orangepuffportal-devcert.pem`/`.key` (see `certs/` — only a
`.pfx` is used by the Docker host above; the Angular dev-server needs the PEM/key form).

## Development

```powershell
dotnet build OrangepuffPortal.slnx
dotnet test OrangepuffPortal.slnx
```

Generating a fresh migration once `OrangepuffPortal.Identity`'s model changes:

```powershell
dotnet ef migrations add <Name> -p src/OrangepuffPortal.Identity -s samples/OrangepuffPortal.SampleHost -o Infrastructure/Migrations
```
