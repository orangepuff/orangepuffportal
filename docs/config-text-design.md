# ConfigTextDefinition — shared UI text store

## Problem

Label and validation-message strings for portal frontends were hardcoded per consuming app (e.g. OCRWeb's
`src/Frontend/OCRWeb.Frontend/src/app/i18n/*`). Moving them to the database lets every app share one store and
lets the value be changed without a frontend redeploy, at the cost of the compile-time typo-safety the static
TypeScript objects had (each consuming app keeps its own typed key surface locally and treats the DB as the
value source — see that app's own docs for how it re-introduces safety on its side).

## Ownership

`ConfigTextDefinition` lives in **this repo** (`orangepuffportal`), schema `configtext`, next to `identity` —
same externalization pattern as Identity: schema + migration are centrally owned and versioned, consuming apps
(OCRWeb, future portal apps) never migrate this table themselves.

## Schema

```
[iId]             int IDENTITY(1,1)   PK
[sModule]         varchar(60)  NOT NULL   -- owning app/module, e.g. 'OCRWeb.ProjectManagement'
[sTextCode]       varchar(60)  NOT NULL
[sCultureCode]    varchar(10)  NOT NULL   -- '*' = fallback row, or a real culture e.g. 'en-US'
[sTextType]       varchar(10)  NOT NULL   -- 'msg', 'lbl'
[sText]           nvarchar(1000) NOT NULL
[iInsertedUserId] int NULL
[dtInsertedTime]  datetime NULL
[iUpdatedUserId]  int NULL
[dtUpdatedTime]   datetime NULL
[sNote]           nchar(255) NULL
```

Unique key (enforced by a unique index, not just convention): `(sModule, sTextCode, sCultureCode, sTextType)`.

See [`config-text-schema.sql`](config-text-schema.sql) for the exact `CREATE TABLE`/index script (reference
only — the real schema is owned by the `ConfigTextDbContext` EF Core migration under
`src/OrangepuffPortal.ConfigText/Infrastructure/Migrations`).

## Admin CRUD

A "Manage config text" admin screen (`admin/config-text` in `@orangepuff/portal-frontend`, backed by
`OrangepuffPortal.ConfigText.Contract.Interfaces.IConfigTextAdminService` / `ConfigTextAdminService`) lets an
admin list, add, edit, and delete rows directly, independent of the seed path below. It is paginated
(default page size 50; 50/100/200/500 selectable) and filterable by module, text code, culture code, text
type, and a free-text search over `sText`. Mapped under the `AdminOnly`-gated `/bff/admin` group:

- `GET /bff/admin/config-text?module=&textCode=&cultureCode=&textType=&text=&page=&pageSize=`
- `POST /bff/admin/config-text`
- `PUT /bff/admin/config-text/{id}`
- `DELETE /bff/admin/config-text/{id}`

Admin writes go through `ConfigTextDefinition.AdminUpdate` (full-field update, including the row's identity
fields) rather than the seed-only `Replace`, reject on a duplicate `(module, textCode, cultureCode, textType)`
key, and invalidate `ConfigTextCache` the same way `ConfigTextWriter` does.

## Write path — seeding, not admin editing

Each consuming app also owns its own default text as a
source-controlled seed file (JSON, one per culture, living in that app's own repo) and pushes it into this
table at its own startup, in-process, through `IConfigTextWriter` (`OrangepuffPortal.ConfigText.Contract`):

```csharp
public sealed record ConfigTextSeedEntry(
    string SModule,
    string STextCode,
    string STextType,
    string SText,
    string? SNote,
    bool BtReplace = false);

public interface IConfigTextWriter
{
    /// <summary>
    /// Upserts one culture's worth of entries. For every entry, writes two rows: one at the given
    /// <paramref name="cultureCode"/> and one at the wildcard culture "*" (see "Culture fallback" below).
    /// A row that already exists is left untouched unless <see cref="ConfigTextSeedEntry.BtReplace"/> is true.
    /// </summary>
    Task UpsertManyAsync(string cultureCode, IReadOnlyCollection<ConfigTextSeedEntry> entries, CancellationToken cancellationToken = default);
}
```

Registered as a scoped/singleton DI service by `AddConfigTextModule` (see `ModuleRegistration.cs`), flowing to
consuming apps transitively through `OrangepuffPortal.Host`, matching how `ICurrentUser` and the Identity
module already flow. This is an **in-process call**, not an HTTP round-trip — the consuming app's host process
already has this package's DI container wired in via `AddOrangepuffPortal()`.

### Insert-only semantics

`UpsertManyAsync` looks up each entry by the natural key `(sModule, sTextCode, sCultureCode, sTextType)`:

- Not found → insert.
- Found, `BtReplace == false` (the default) → **skip**, leave the existing row untouched. This protects any
  value that was edited directly in the database from being clobbered by the next deploy's seed run.
- Found, `BtReplace == true` → update `sText`/`sNote` and stamp `iUpdatedUserId`/`dtUpdatedTime`.

`BtReplace` is a seed-file-only flag — it is never persisted as a database column. The intended workflow for a
developer who needs to push a corrected value into an already-seeded environment: set `"btReplace": true` on
that JSON entry, deploy once, then set it back to `false` and commit. Forgetting to revert it is not harmful —
the row just keeps re-syncing from the JSON on every subsequent deploy until the flag is flipped back.

### Culture fallback

Every seed entry produces **two rows**: one at `sCultureCode = "*"` and one at the entry's actual culture
(currently always `en-US`, the only seed culture that exists so far). A read for a culture that has no
matching row falls back to the `"*"` row — see `IConfigTextReader` below.

## Read path

```csharp
public sealed record ConfigTextEntryDto(string SModule, string STextCode, string STextType, string SText);

public interface IConfigTextReader
{
    /// <summary>
    /// Returns one resolved row per (SModule, STextCode, STextType): the row matching
    /// <paramref name="cultureCode"/> if one exists, otherwise the "*" fallback row.
    /// </summary>
    Task<IReadOnlyList<ConfigTextEntryDto>> GetAllAsync(string cultureCode, CancellationToken cancellationToken = default);
}
```

Backed by an in-memory cache (`IMemoryCache`, key per culture, no expiry) inside
`OrangepuffPortal.ConfigText`'s `ConfigTextReader`. `ConfigTextWriter` evicts the whole cache after any
successful write so the next read picks up new/changed rows. The table is expected to stay small (low
thousands of rows across all consuming apps), so caching the fully-resolved set per culture in memory is
cheap and avoids a DB round-trip on every page load.

`OrangepuffPortal.Bff` exposes this over HTTP for frontends to call directly:

```
GET /bff/config-text?culture=en-US
```

Returns the array of `ConfigTextEntryDto` for that culture (fallback already resolved server-side). No
pagination — the whole set is small enough to return in one response, matching how the frontend is expected
to fetch it once at app bootstrap and cache it client-side for the session.

## Module wiring

`OrangepuffPortal.ConfigText` implements `IPortalModule` (`ConfigTextPortalModule`), so
`MigratePortalModulesAsync()` on the host migrates its schema automatically whenever `DoMigration: true` —
no manual migration step required from consuming apps, only the manual `IConfigTextWriter.UpsertManyAsync`
seed call, which is intentionally separate from migration (seeding is data, not schema).

## What's out of scope for now

- No per-app authorization on the read endpoint beyond normal portal cookie auth — any signed-in session can
  read any module's text, which is fine since none of this is sensitive data.
- Only `en-US` seed content exists; the `sCultureCode` design already supports more cultures, nothing else
  needs to change to add one.
