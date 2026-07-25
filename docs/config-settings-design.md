# ConfigSections / Configs / ConfigUsers / ConfigUsersHistory — per-user settings store

## Problem

A generic, per-user-editable settings/config system: a catalog of what settings exist (grouped into
sections), and each user's own value for each setting, with a full change history. Distinct from
[`ConfigTextDefinition`](config-text-design.md) (UI labels/messages): this is *data*, not display text —
though a section/config's own display label is itself a `ConfigTextDefinition` lookup, tying the two
features together (see "Display text" below).

## Ownership

All four tables live in **this repo** (`orangepuffportal`), schema `config`, next to `identity` and
`configtext` — same externalization pattern.

## Schema

```
config.ConfigSections
  iId              INT IDENTITY       PK
  sModule          VARCHAR(60)        NOT NULL   -- owning app/module, e.g. 'OCRWeb.ProjectManagement'
  sSectionDesc     NVARCHAR(255)      NOT NULL   -- plain fallback description, always present
  sTextCode        VARCHAR(100)       NOT NULL   -- pointer into ConfigTextDefinition.sTextCode for the
                                                  -- localized section label (no FK — the two tables are
                                                  -- seeded independently, by convention only)
  btShow           BIT                NOT NULL DEFAULT 1
  iInsertedUserId  INT                NULL
  dtInsertedTime   DATETIME           NULL
  iUpdatedUserId   INT                NULL
  dtUpdatedTime    DATETIME           NULL

config.Configs
  iId              INT IDENTITY       PK
  iSectionId       INT                NOT NULL   FK -> ConfigSections.iId
  sConfigCode      VARCHAR(60)        NOT NULL   -- technical lookup key, globally unique across all
                                                  -- modules (an app looks up "give me config X" by this
                                                  -- alone, without needing to know its section)
  sConfigName      NVARCHAR(255)      NOT NULL   -- plain fallback display name, always present
  sTextCode        VARCHAR(100)       NOT NULL   -- pointer into ConfigTextDefinition.sTextCode
  iConfigType      INT                NOT NULL DEFAULT 0  -- 0=String, 1=Int, 2=Decimal, 3=Boolean —
                                                  -- selects which ConfigUsers value column is live
  btShow           BIT                NOT NULL DEFAULT 1
  btAllowUserEdit  BIT                NOT NULL DEFAULT 0  -- gates self-service edit; enforced by callers
                                                  -- (e.g. only a self-service Bff endpoint checks this),
                                                  -- not by IConfigUserValueService itself
  iInsertedUserId  INT                NULL
  dtInsertedTime   DATETIME           NULL
  iUpdatedUserId   INT                NULL
  dtUpdatedTime    DATETIME           NULL

config.ConfigUsers
  iId              INT IDENTITY       PK
  iUserId          INT                NOT NULL   -- identity.Users.iId, by id only (no FK — cross-context)
  iConfigId        INT                NOT NULL   FK -> Configs.iId
  sConfigValue     NVARCHAR(255)      NULL
  iConfigValue     INT                NULL
  nConfigValue     DECIMAL(8,3)       NULL
  btConfigValue    BIT                NULL
  btActive         BIT                NOT NULL DEFAULT 1
  iInsertedUserId  INT                NULL
  dtInsertedTime   DATETIME           NULL
  iUpdatedUserId   INT                NULL
  dtUpdatedTime    DATETIME           NULL

config.ConfigUsersHistory
  iId              INT IDENTITY       PK
  iConfigUserId    INT                NOT NULL   FK -> ConfigUsers.iId — whose history this is
  iUserId          INT                NOT NULL   -- denormalized copy, as given
  iConfigId        INT                NOT NULL   -- denormalized copy, as given
  sConfigValue     NVARCHAR(255)      NULL
  iConfigValue     INT                NULL
  nConfigValue     DECIMAL(8,3)       NULL
  btConfigValue    BIT                NULL
  btActive         BIT                NOT NULL DEFAULT 1
  iInsertedUserId  INT                NULL
  dtInsertedTime   DATETIME           NULL
  iUpdatedUserId   INT                NULL   -- vestigial: rows are append-only, never updated after insert
  dtUpdatedTime    DATETIME           NULL   -- vestigial, same reason
```

Unique keys:
- `ConfigSections`: `(sModule, sTextCode)`
- `Configs`: `sConfigCode` (standalone — a config is always looked up by this alone; a settings UI browses
  by listing sections then their configs, so sections don't need an equivalent standalone lookup code)
- `ConfigUsers`: `(iUserId, iConfigId)` — one current value per user per config
- `ConfigUsersHistory`: none (many rows accumulate per `ConfigUsers` row over time); indexed on
  `iConfigUserId` for lookup

See [`config-settings-schema.sql`](config-settings-schema.sql) for the exact script (reference only — the
real schema is owned by `ConfigDbContext`'s EF Core migration).

## Two very different write paths

### Catalog (ConfigSections + Configs) — seeded by modules, same pattern as ConfigText

Exactly mirrors [`ConfigTextDefinition`'s seeding contract](config-text-design.md): each consuming app
ships its own default sections/configs as a source-controlled seed file and pushes them in-process at its
own startup through `IConfigCatalogWriter` — insert-only unless a seed entry sets `BtReplace`. A section's
configs are seeded together as one nested unit (a config always seeds alongside the section it belongs to,
so the writer can resolve the section's `iSectionId` without a second round-trip):

```csharp
public sealed record ConfigSeedEntry(
    string SConfigCode,
    string SConfigName,
    string STextCode,
    int IConfigType,
    bool BtShow = true,
    bool BtAllowUserEdit = false,
    bool BtReplace = false);

public sealed record ConfigSectionSeedEntry(
    string SSectionDesc,
    string STextCode,
    IReadOnlyCollection<ConfigSeedEntry> Configs,
    bool BtShow = true,
    bool BtReplace = false);

public interface IConfigCatalogWriter
{
    Task UpsertAsync(string module, IReadOnlyCollection<ConfigSectionSeedEntry> sections, CancellationToken cancellationToken = default);
}
```

Per section: look up by `(module, STextCode)`; insert if missing, update `sSectionDesc`/`btShow` only if
`BtReplace`, otherwise skip. Per config within it: look up by `SConfigCode` alone (global); insert if
missing (linked to the resolved `iSectionId`), update all fields including `iSectionId` (a config can move
sections on a deliberate `BtReplace` re-seed) only if `BtReplace`, otherwise skip.

### Values (ConfigUsers + ConfigUsersHistory) — live application/user data, never seeded

Not pushed at startup. Written on demand, when a user's setting actually changes, through
`IConfigUserValueService`:

```csharp
public interface IConfigUserValueService
{
    Task<ConfigUserValueDto?> GetValueAsync(int userId, string configCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConfigUserValueDto>> GetValuesAsync(int userId, CancellationToken cancellationToken = default);
    Task SetValueAsync(int userId, string configCode, ConfigValueInput value, CancellationToken cancellationToken = default);
}
```

`SetValueAsync` flow, in one `SaveChanges`:
1. Resolve `Configs` by `sConfigCode` (throws if unknown — the catalog is the source of truth for what
   configs exist).
2. Look up the existing `ConfigUsers` row for `(userId, configId)`.
3. If one exists: **first** insert a `ConfigUsersHistory` row that snapshots its *current* (about-to-be-
   overwritten) values, **then** overwrite the `ConfigUsers` row with the new value and stamp
   `UpdatedUserId`/`UpdatedTime`. History always holds the value that was just replaced, never the new one.
4. If none exists: insert a new `ConfigUsers` row — nothing to snapshot, no history row.

There is deliberately no default-value column on `Configs` — `GetValueAsync` returns `null` when a user has
never set a value; callers decide what "unset" means for that particular setting.

`btAllowUserEdit` is **not** enforced inside `IConfigUserValueService` — it's a signal for callers (e.g. a
self-service "my settings" Bff endpoint checks it before calling `SetValueAsync`; an admin-only endpoint can
set any config regardless).

## Display text

`ConfigSections.sTextCode` / `Configs.sTextCode` are plain string pointers into
`ConfigTextDefinition.sTextCode` — there is no FK, the two tables are seeded independently and by
convention only. A settings UI resolves a section/config's label the same way it resolves any other label:
look it up via `IConfigTextReader`/`GET /bff/config-text`, falling back to `sSectionDesc`/`sConfigName` if
the text code hasn't been seeded into `ConfigTextDefinition` yet.

## What's out of scope for now

- No Bff read/write endpoints yet (no `GET /bff/config`, no self-service "my settings" endpoint) — this
  doc covers the module and its in-process contract only; add endpoints in `OrangepuffPortal.Bff` when a
  consuming app actually needs a settings UI.
- No default-value column on `Configs` (see above).
- No `CHECK` constraint enforcing "only the column matching `iConfigType` is non-null" on `ConfigUsers`/
  `ConfigUsersHistory` — enforced by `ConfigUserValueService` only, at the application layer.
