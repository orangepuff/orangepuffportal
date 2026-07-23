# OrangepuffPortal

OrangepuffPortal is a reusable Identity and authentication SDK for ASP.NET Core + Angular applications, covering cookie-based sessions, Google OAuth sign-in, and user/permission management.

Rather than writing and maintaining this logic separately in every application, a consuming application installs the `OrangepuffPortal.Host` NuGet package (backend) and the `@orangepuff/portal-frontend`/`-shared` npm packages (frontend). Every consuming application then shares identical, centrally-maintained security behavior, and picks up updates or fixes simply by bumping the package version — no manual code copying or merging required.

## Design

- **One process, not two.** Instead of a separate API and Bff service calling each other over the network, `OrangepuffPortal.Host` runs Identity, Google OAuth, and the `/bff/*` routes all together in a single package. A body app adds one reference and gets everything.
- **Security logic lives here, not in the body app.** A body app only sets its own connection string, OAuth secrets, and app name — the actual security rules are maintained in this SDK. Bump the package version, and every body app gets the same fix at once; nobody copies logic around by hand.
- **Database setup happens automatically.** Any `IPortalModule` — Identity's own, or one a body app writes for itself — gets its database created and seeded automatically when the app starts. One setting, `DoMigration`, turns this on or off.

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

See `samples/OrangepuffPortal.SampleHost` for a working reference — it's a thin host that wires up everything below with no body-app-specific code of its own.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Diagnostics logging — required by OrangepuffPortal.Identity's command handlers (they take a hard ITransactionLogger dependency). Not wired automatically by AddOrangepuffPortal since the connection string is deployment-specific.
builder.Services.AddDiagnostics(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("DiagnosticLogs")
        ?? throw new InvalidOperationException("ConnectionStrings:DiagnosticLogs is not configured.");
    options.LoggerName = builder.Configuration["Diagnostics:LoggerName"] ?? "YourAppName";
    options.EnvironmentName = builder.Configuration["Diagnostics:EnvironmentName"] ?? "DEV";
});
builder.Services.AddDiagnosticsAspNetCore();

builder.Services.AddOrangepuffPortal(builder.Configuration);
// ...body app's own module registrations (its own IPortalModule implementations)...

var app = builder.Build();

await app.MigratePortalModulesAsync();

// Authentication must run before UseDiagnostics(): its transaction middleware auto-stamps the current user (RequestContextTransactionLogger -> CurrentUser.UserId), which throws instead of falling back to a hardcoded id — HttpContext.User has to already be populated by the time it runs.
app.UseAuthentication();
app.UseAuthorization();
app.UseDiagnostics();

app.MapOrangepuffPortal();
// ...body app's own endpoint mapping...

app.Run();
```

**Required config** (e.g. `appsettings.json`):

```json
{
  "ConnectionStrings": {
    "Portal": "...",
    "DiagnosticLogs": "..."
  },
  "Portal": {
    "AppName": "Your App Name"
  },
  "Authentication": {
    "Google": {
      "ClientId": "...",
      "ClientSecret": "..."
    }
  },
  "Frontend": {
    "BaseUrl": "https://your-frontend-origin"
  },
  "Seed": {
    "AdminUsername": "...",
    "AdminEmail": "...",
    "AdminDisplayName": "...",
    "AdminPassword": "..."
  },
  "DoMigration": true
}
```

- `ConnectionStrings:Portal` — Identity's own tables (`identity` schema).
- `ConnectionStrings:DiagnosticLogs` — the diagnostics logging database (see
  [DiagnosticLog](https://github.com/orangepuff/DiagnosticLog)) — a separate database from `Portal` above.
- `Authentication:Google:*` — see [Configuring Google sign-in](#configuring-google-sign-in).
- `Frontend:BaseUrl` — the frontend app's real origin; see the note right below.
- `Seed:*` — the admin account created the first time migrations run against an empty database.
- `DoMigration` — whether to automatically run migrations and seed data on startup.

**Never put secret values in `appsettings.json`** — it's committed to source control. Where a
secret actually lives depends on how you're running:

| Situation | Where the secret goes |
|---|---|
| Local dev, via `docker compose up` | `.env` (git-ignored) — Docker Compose injects it as an env var into the container at runtime |
| Local dev, via `dotnet run` | [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) (`dotnet user-secrets set ...`) — stored outside the repo entirely |
| Production | Environment variables from your platform (Azure App Service settings, Kubernetes Secrets, AWS Secrets Manager, Key Vault, etc.) — never `.env`, never `appsettings.json` |

`appsettings.json` should only hold non-secret values (`Portal:AppName`, `DoMigration`, etc.) —
secret keys shouldn't appear in it at all; whichever provider above is active fills them in at
runtime.

For the sample host specifically (Docker Compose), create a `.env` file (see `.env.example` at the
repo root and [Development certs](#development-certs) below):

```
DEV_CERT_PASSWORD=
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
SEED_ADMIN_PASSWORD=
```

`DEV_CERT_PASSWORD` must match the password used to generate the dev cert itself — see
[Development certs](#development-certs) below for the full explanation, or just run:

```powershell
mkdir certs
dotnet dev-certs https --trust
dotnet dev-certs https -ep .\certs\orangepuffportal-devcert.pfx -p "Your_dev_cert_password123!"
```

`Frontend:BaseUrl` must be the exact absolute origin your Angular app (below) actually serves from —
it's where the Bff redirects the browser back to after a successful Google sign-in.

## Consuming the frontend shell from a body app

An Angular 22 (standalone components) app gets the whole portal shell — Landing, `/home`
(iframes a separate body app via `bodyAppUrl`), session-aware header, admin CRUD UI, Settings,
auth/admin guards — from two packages, the frontend analogue of `AddOrangepuffPortal()` above.

**Install**:

```powershell
npm install @orangepuff/portal-frontend @orangepuff/portal-frontend-shared
```

(The sample app in this repo instead depends on packed `.tgz` files built locally — see
`samples/OrangepuffPortal.SampleHost.Frontend/package.json` — so it verifies the exact artifact
that gets published, not just something similar to it; see [samples/README.md](samples/README.md).)

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

## Configuring ports

There is no single "port setting" — the backend (Bff) port and frontend port each come from a
different file depending on how you run them, and several *other* files have to be kept in sync
with whichever port is actually in use. For the sample apps (`OrangepuffPortal.SampleHost` +
`OrangepuffPortal.SampleHost.Frontend`), here's exactly where to look:

| What | File | Setting |
|---|---|---|
| Backend port, via `docker compose up` | `docker-compose.yml` | `ports:` mapping (`host:container`); container is HTTPS-only, listening on `8081` via `ASPNETCORE_URLS` |
| Backend port, via `dotnet run` / IDE | `samples/OrangepuffPortal.SampleHost/Properties/launchSettings.json` | `applicationUrl`; also HTTPS-only |
| Frontend port | `samples/OrangepuffPortal.SampleHost.Frontend/angular.json` | `projects.*.architect.serve.options.port` (or pass `ng serve --port <n>`) |
| Frontend dev-proxy target | `samples/OrangepuffPortal.SampleHost.Frontend/proxy.conf.json` | `target` — the frontend dev-server proxies same-origin `/bff/*` calls here |
| Frontend's `bffOrigin` | `samples/OrangepuffPortal.SampleHost.Frontend/src/app/app.config.ts` | `providePortalShell({ bffOrigin: ... })` — see the "one absolute-URL gotcha" note above |
| Backend's `Frontend:BaseUrl` | `samples/OrangepuffPortal.SampleHost/appsettings.Development.json` | `Frontend:BaseUrl` — where the Bff redirects the browser back to post-login |
| Google OAuth client | Google Cloud Console (not a repo file) | Authorized JavaScript origin / redirect URI — see [Configuring Google sign-in](#configuring-google-sign-in) below |

Both backend rows are **HTTPS-only, on `7100`, on purpose** — the backend never binds a plaintext
HTTP port, in either `docker-compose.yml` or `launchSettings.json`, so there's no accidental
unencrypted path for cookies or OAuth tokens to leak over. Keep the two rows in sync: if you ever
change one, change the other to match, or every row below will need two different values again.

Concretely, for the sample apps:

| Setting | Value | File |
|---|---|---|
| Backend HTTPS | `https://localhost:7100` | `docker-compose.yml` / `samples/OrangepuffPortal.SampleHost/Properties/launchSettings.json` |
| Frontend | `https://localhost:5599` | `samples/OrangepuffPortal.SampleHost.Frontend/angular.json` |
| `proxy.conf.json` target | `https://localhost:7100` | `samples/OrangepuffPortal.SampleHost.Frontend/proxy.conf.json` |
| `bffOrigin` | `https://localhost:7100` | `samples/OrangepuffPortal.SampleHost.Frontend/src/app/app.config.ts` |
| `Frontend:BaseUrl` | `https://localhost:5599` | `samples/OrangepuffPortal.SampleHost/appsettings.Development.json` |
| Google OAuth client registered against | `https://localhost:7100` | Google Cloud Console (not a repo file) |

## Development certs

Both the backend and the frontend are HTTPS-only (see above), so both need a local dev
certificate — but they load it in different *formats*, because Kestrel-in-Docker and Angular's dev
server read certs differently:

| Consumer | Format | File | How it's loaded |
|---|---|---|---|
| Backend, via `docker compose up` | PKCS#12 (`.pfx`), password-protected | `certs/orangepuffportal-devcert.pfx` | Mounted into the container (`./certs:/https:ro` in `docker-compose.yml`), read via `ASPNETCORE_Kestrel__Certificates__Default__Path`/`Password` — the password comes from `.env`'s `DEV_CERT_PASSWORD` |
| Backend, via `dotnet run` / IDE | *(none needed)* | — | Kestrel auto-discovers the trusted local dev cert from the OS certificate store — no explicit config at all, as long as it's been trusted once (see below) |
| Frontend, via `ng serve` | PEM + key pair, no password | `certs/orangepuffportal-devcert.pem` / `.key` | Read directly by Angular's dev server via `angular.json`'s `sslCert`/`sslKey` |

The Docker path needs an explicit file because a container can't reach the host's local trusted-cert
store the way a directly-run process can — the cert has to be handed to it explicitly instead.

Generate both files, once per machine, from a `certs/` folder at the repo root:

```powershell
mkdir certs

# Trust the local dev cert once — also what dotnet run relies on to find it automatically
dotnet dev-certs https --trust

# Backend (Docker) — PFX, password-protected; put the same password in .env as DEV_CERT_PASSWORD
dotnet dev-certs https -ep .\certs\orangepuffportal-devcert.pfx -p "Your_dev_cert_password123!"

# Frontend (ng serve) — PEM + key pair, no password
dotnet dev-certs https --format Pem -ep .\certs\orangepuffportal-devcert.pem -np
```

`certs/` is `.gitignore`d — neither file is ever committed.

## Configuring Google sign-in

### 1. Configure the OAuth consent screen

Google Cloud Console → APIs & Services → OAuth consent screen. Only needed once per Google Cloud
project, before a client ID can be created.

- **User Type**: `External` is fine for local dev/testing.
- **Scopes**: add `email` and `profile` (the two scopes `AddGoogle()` requests).
- **Test users**: while the app is in "Testing" publishing status, only accounts listed here can
  sign in — add every Google account you'll use locally. Move to "In production" once you want
  unlisted accounts to sign in too (requires Google's verification for sensitive scopes, but not
  for `email`/`profile`).

### 2. Create an OAuth client

Same Console section → Credentials → Create Credentials → OAuth client ID → Web application.
Create one client per environment that needs to sign in (e.g. one for local dev, one per deployed
environment) — each has its own origin/redirect URI and its own Client ID/Secret pair.

- **Authorized JavaScript origin**: the Bff's origin, e.g. `https://localhost:7100`.
- **Authorized redirect URI**: the Bff's origin + `/signin-google`, e.g.
  `https://localhost:7100/signin-google`. This is ASP.NET Core's default Google-handler callback
  path — the code doesn't override it, so it's always `/signin-google` off the Bff's origin, never
  a `/bff/*`-prefixed path.

> If the Bff's port changes (see [Configuring ports](#configuring-ports) above), update both
> values here — in Google Cloud Console and in your backend config — to match.

Copy the generated Client ID and Client Secret into:

- `Authentication:Google:ClientId` / `ClientSecret` in your config, or
- `Authentication__Google__ClientId` / `Authentication__Google__ClientSecret` as env vars (e.g.
  via Docker Compose — see `.env.example` at the repo root for the sample host's own
  `GOOGLE_CLIENT_ID`/`GOOGLE_CLIENT_SECRET` vars).

### 3. Sign-in behavior

`AddGoogle()` requests the `email` and `profile` scopes and maps Google's `email_verified` claim.
`ProvisionGoogleUserCommandHandler` then decides what happens:

| Condition | Result |
|---|---|
| Google identity already linked to a user | Signed in |
| Email not verified with Google | Rejected — `/auth-error?reason=email_not_verified` |
| Email matches an existing user (first Google sign-in for them) | Auto-links and signs in |
| No existing user, and `Identity:AllowSelfRegistrationViaGoogle` is `false` | Rejected — `/auth-error?reason=registration_disabled` |
| No existing user, self-registration allowed (default) | Provisions a new user (`google_<email-local-part>`) and signs in |
| Any other OAuth failure | Rejected — `/auth-error?reason=unknown` |

Notes:

- `Identity:AllowSelfRegistrationViaGoogle` defaults to `true` if unset. Set it to `false` to
  require an admin to pre-create every user (via the admin Users UI/`/bff/admin/users`) before
  they can sign in.
- The frontend's `auth-error-page` only has copy for `email_not_verified` and
  `registration_disabled` — every other `reason` value falls back to a generic message.

## Running the sample host locally

`samples/OrangepuffPortal.SampleHost` is a thin, unpublished host that exercises the full
`/bff/login` → Google OAuth → `/bff/me` flow without any body app.

```powershell
docker compose up -d --build
```

- Host: https://localhost:7100 (HTTPS only)

Requires the dev cert at `certs/orangepuffportal-devcert.pfx` and `.env` filled in (including
`DEV_CERT_PASSWORD`) — see [Development certs](#development-certs) above.

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

Requires the dev cert pair at `certs/orangepuffportal-devcert.pem`/`.key` — see
[Development certs](#development-certs) above.

## Development

```powershell
dotnet build OrangepuffPortal.slnx
dotnet test OrangepuffPortal.slnx
```

Generating a fresh migration once `OrangepuffPortal.Identity`'s model changes:

```powershell
dotnet ef migrations add <Name> -p src/OrangepuffPortal.Identity -s samples/OrangepuffPortal.SampleHost -o Infrastructure/Migrations
```
