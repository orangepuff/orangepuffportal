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
  sSectionDesc     NVARCHAR(255)      NOT NULL   -- plain fallback description, always present
  sTextCode        VARCHAR(100)       NOT NULL   -- pointer into ConfigTextDefinition.sTextCode for the
                                                  -- localized section label (no FK — the two tables are
                                                  -- seeded independently, by convention only)
  btShow           BIT                NOT NULL DEFAULT 1
  iSortOrder       INT                NULL       -- display order among sections; null sorts after any
                                                  -- ordered sections, by iId (same convention as
                                                  -- identity.SecurityRuleItems.iSortOrder)
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
  iSortOrder       INT                NULL       -- display order within its section; null sorts after
                                                  -- any ordered configs, by iId
  sDefaultValue    NVARCHAR(255)      NULL       -- exactly one of these four should be set, matching
  iDefaultValue    INT                NULL       -- iConfigType — see "Default values" below. Absence
  nDefaultValue    DECIMAL(8,3)       NULL       -- means no default is configured for this config.
  btDefaultValue   BIT                NULL
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
- `ConfigSections`: `sTextCode` (standalone, globally unique across every consuming app sharing this
  catalog — a seeding app is expected to prefix its own `STextCode` values, e.g.
  `ocrProjectManagement.general`, same convention already used for `ConfigTextDefinition.sTextCode`.
  There is deliberately no `sModule` column here, unlike `ConfigTextDefinition` — a settings UI never
  filters sections by owning app, and admin-created sections have no "module" a human would know how
  to fill in, so it was pure friction with no read-path payoff)
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
    int? ISortOrder = null,
    ConfigValueInput? DefaultValue = null,
    bool BtReplace = false);

public sealed record ConfigSectionSeedEntry(
    string SSectionDesc,
    string STextCode,
    IReadOnlyCollection<ConfigSeedEntry> Configs,
    bool BtShow = true,
    int? ISortOrder = null,
    bool BtReplace = false);

public interface IConfigCatalogWriter
{
    Task UpsertAsync(string module, IReadOnlyCollection<ConfigSectionSeedEntry> sections, CancellationToken cancellationToken = default);
}
```

`module` is only used for log attribution now — it plays no role in the lookup/uniqueness key. Per section:
look up by `STextCode` alone (global); insert if missing, update `sSectionDesc`/`btShow` only if
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
    Task<IReadOnlyList<UserConfigSectionDto>> GetSectionsForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task SetValueAsync(int userId, string configCode, ConfigValueInput value, CancellationToken cancellationToken = default);
}
```

`GetSectionsForUserAsync` is the read path a settings UI actually needs: every visible section
(`btShow = 1`) and its visible configs, each annotated with that user's current value (all four value
fields null if they've never set one), ordered by `iSortOrder` (nulls last) then `iId` at both the section
and config level. Unlike `GetValuesAsync` (which only returns configs the user already has a `ConfigUsers`
row for), this always returns the full catalog so a settings page can render every setting, set or not.

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

## Bff endpoints

`OrangepuffPortal.Bff` exposes this module through `IConfigGateway`/`ConfigGateway`
(`Infrastructure/ConfigGateway/`) — a thin in-process wrapper over `IConfigUserValueService`, same shape as
`IIdentityGateway` but calling the module's plain service directly (Config has no MediatR *commands/queries*
of its own — it does have one MediatR *notification handler*, see "Default values" below):

- `GET /bff/users/{userId}/config` — self-or-admin only (manual check inside the endpoint: the caller must
  be the target user or hold the `AdminOnly` claim). Returns `GetSectionsForUserAsync(userId)`. Any signed
  in user can read their own config; an admin can read anyone's.
- `PUT /bff/admin/users/{userId}/config/{configCode}` — mapped under the existing `/bff/admin` group
  (`AdminOnly` policy already required there). Body is a `ConfigValueInput`. Calls `SetValueAsync`
  directly — admin writes are not gated by `btAllowUserEdit` (see above, that flag is a signal for a future
  self-service write endpoint, not built yet since the current settings UI is admin-write / user-read-only
  by design).

`ConfigGateway` also forwards to `IConfigCatalogAdminService` (`ConfigCatalogAdminService`, added alongside
the seed-only `ConfigCatalogWriter`) for the "Manage config" admin screen (`admin/config` in
`@orangepuff/portal-frontend`) — full CRUD over the catalog itself, not just a user's values. The screen has
two tabs: **Sections** (small, unpaged CRUD, `ConfigSections` is expected to stay a hand-curated list) and
**Configs** (paginated — default page size 50, 50/100/200/500 selectable — and filterable by section,
config code, and config name):

- `GET/POST /bff/admin/config/sections`, `PUT/DELETE /bff/admin/config/sections/{id}`
- `GET /bff/admin/config/items?sectionId=&configCode=&configName=&configType=&page=&pageSize=`,
  `POST /bff/admin/config/items`, `PUT/DELETE /bff/admin/config/items/{id}`

A section can't be deleted while any config still references it (`section_has_configs` rejection). Admin
writes go through `ConfigSection.AdminUpdate`/`ConfigItem.AdminUpdate` (full-field update, distinct from the
seed-only `Replace`) and reject on a duplicate key (`sTextCode` for sections, `sConfigCode` for configs).

`AddSectionAsync`/`AddConfigAsync` auto-assign `iSortOrder` when the admin leaves it blank (`ISortOrder`
null in the request): one past the current highest `SortOrder` in scope — every other section for
`AddSectionAsync`, every other config in the *same* section for `AddConfigAsync` (`IConfigRepository.
GetMaxSectionSortOrderAsync`/`GetMaxConfigSortOrderAsync`, both ignoring null-valued rows), or `1` if
nothing in scope has one set yet. An explicitly given `ISortOrder` (including `0`) is always kept as-is —
this only fills the gap when the admin doesn't set one, so a brand-new row still gets a real position
instead of sorting last forever via the `null` fallback. `UpdateSectionAsync`/`UpdateConfigAsync` do not
auto-assign — editing an existing row is expected to set (or clear) sort order deliberately.

## Default values

`Configs` carries an optional default (`sDefaultValue`/`iDefaultValue`/`nDefaultValue`/`btDefaultValue`,
exactly one set matching `iConfigType`, checked via `ConfigItem.HasDefaultValue`). Two things apply it,
both only the *first* time a given (config, user) pair comes into existence — neither ever overwrites an
already-set `ConfigUsers` value:

1. **New config, existing users** — two independent call sites reach the same "someone just created a
   config with a default" moment, one per write path:
   - **Seeded at startup**: when `ConfigCatalogWriter.UpsertAsync` *creates* a config (not a `BtReplace`
     update of one already there) with a default set, it backfills that default onto every id in the
     `existingUserIds` list the caller passed in (Config has no way to enumerate users itself — the
     consuming app's own startup code fetches ids from Identity and passes them through). Re-seeding an
     existing config with a changed default (`BtReplace`) only updates the stored default for *future*
     new users/configs — it never retroactively touches anyone's existing value.
   - **Created from the admin CRUD screen**: `ConfigCatalogAdminService.AddConfigAsync` calls
     `IConfigUserValueService.ApplyDefaultForNewConfigAsync(configId, actorUserId)` right after insert,
     which reads every user id via `IUserDirectory` (`OrangepuffPortal.Shared.Auditing` — a thin
     cross-module reader over `identity.Users`, implemented in `OrangepuffPortal.Host` since only the
     composition root may reference another module's DbContext directly) and bulk-inserts a `ConfigUsers`
     row for each one via `AddUserValuesAsync`. Swallowed/logged as a warning, same reasoning as path 2
     below — a hiccup here must not fail the "config created" response since the catalog row already
     committed.
2. **New user, existing configs** — Identity's `AddUserCommandHandler` and `ProvisionGoogleUserCommandHandler`
   (Google self-registration) both publish a `UserCreatedNotification(UserId, ActorUserId)` (MediatR, defined
   in `OrangepuffPortal.Shared.Events` since Identity and Config don't otherwise reference each other) after
   creating a user. Config's `ApplyConfigDefaultsOnUserCreatedHandler` reacts by calling
   `IConfigUserValueService.ApplyDefaultsForNewUserAsync(userId, actorUserId)`, which inserts a `ConfigUsers`
   row for every config that has a default configured. The handler swallows any exception (logs, doesn't
   rethrow) — applying defaults can never fail the user-creation request that triggered it.

Both paths write with a real actor when one exists, `0` otherwise. `identity.Users.iId` is an `IDENTITY`
starting at 1, so `0` can never collide with a real user — it marks a `ConfigUsers` row as system-applied
rather than deliberately set by anyone:
- Admin-added user (`AddUserCommand`): the acting admin's id.
- Google self-registration (`ProvisionGoogleUserCommand`): `0` — no admin performed this.
- Startup-seed backfill onto existing users: `0` — unattended startup code has no signed-in user
  (`ICurrentUser` would throw outside a real request).

No `ConfigUsersHistory` row is written for either path — both only ever *insert* a brand-new `ConfigUsers`
row (guarded by an idempotency check against an existing row for path 2), and there's nothing to snapshot
on a first-ever insert, matching `SetValueAsync`'s existing rule.

## What's out of scope for now

- No self-service (non-admin) config write endpoint — `btAllowUserEdit` is populated by seed data but not
  yet consulted by any caller; the current Settings UI only lets an admin edit values, any user (including
  the owner) only ever reads their own. Add a `PUT /bff/me/config/{configCode}` gated on
  `btAllowUserEdit` if/when self-service editing is actually needed.
- No `CHECK` constraint enforcing "only the column matching `iConfigType` is non-null" on `Configs`'
  default columns, or on `ConfigUsers`/`ConfigUsersHistory` — enforced by `ConfigCatalogWriter`/
  `ConfigUserValueService` only, at the application layer.
