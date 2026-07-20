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

## Running the sample host locally

`samples/OrangepuffPortal.SampleHost` is a thin, unpublished host that exercises the full
`/bff/login` → Google OAuth → `/bff/me` flow without any body app.

```powershell
docker compose up -d --build
```

- Host: http://localhost:5100 / https://localhost:7100

SQL Server is external (not managed by compose) — see `appsettings.Development.json` for the
expected connection string shape, and create the `OrangepuffPortalSample` database first.

## Development

```powershell
dotnet build OrangepuffPortal.slnx
dotnet test OrangepuffPortal.slnx
```

Generating a fresh migration once `OrangepuffPortal.Identity`'s model changes:

```powershell
dotnet ef migrations add <Name> -p src/OrangepuffPortal.Identity -s samples/OrangepuffPortal.SampleHost -o Infrastructure/Migrations
```
