# ConfigData — Application Config Module Design

## Purpose

`ConfigData` stores **application-level** key/value configuration — values that are global, user-independent, and managed by an admin through the portal UI. Examples: feature toggles, threshold values, API URLs, display labels that aren't part of ConfigText translations.

Unlike `ConfigText` (UI translations keyed by module/code/culture) and `Config` (per-user preference values), ConfigData has no user dimension and no culture dimension — it is a flat, named dictionary of strings.

## Database

| Schema   | Table        | History table                                    |
|----------|--------------|--------------------------------------------------|
| `configdata` | `ConfigData` | `configdata.__EFMigrationsHistory` |

### Columns

| Column                | Type             | Nullable | Notes                              |
|-----------------------|------------------|----------|------------------------------------|
| `iId`                 | `int IDENTITY`   | No       | PK                                 |
| `sKey`                | `varchar(100)`   | No       | Unique index `UQ_ConfigData_Key`   |
| `sValue`              | `varchar(max)`   | Yes      |                                    |
| `bAllowEditByScreen`  | `bit`            | Yes      | UI hint; does not enforce security |
| `sDescription`        | `varchar(255)`   | Yes      | Admin-facing description           |
| `iInsertedUserId`     | `int`            | Yes      | Nullable — can be seeded by code   |
| `dtInsertedTime`      | `datetime`       | Yes      |                                    |
| `iUpdatedUserId`      | `int`            | Yes      |                                    |
| `dtUpdatedTime`       | `datetime`       | Yes      |                                    |

## Caching

All rows are cached together under a single Redis key `ConfigData:all` (via `IDistributedCache`). A **24-hour absolute TTL** acts as a safety net; admin mutations always invalidate the key immediately.

**Startup warm**: `ConfigDataCacheWarmer` (registered as `IHostedService`) pre-loads all rows from the DB into Redis before the app starts serving traffic. This runs on every app start and app pool recycle, regardless of `DoMigration`.

**Read path**: `ConfigDataService` checks the cache first; on a miss (only possible in the brief startup window before the warmer completes), it loads from DB and repopulates the cache.

**Invalidation**: every Create, Update, and Delete in `ConfigDataAdminService` calls `cache.InvalidateAsync()` after `SaveChangesAsync()`. The next read reloads from DB.

## API surface (public contract)

### `IConfigDataService` — read-only, for any caller app

```csharp
Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
Task<IReadOnlyList<ConfigDataDto>> GetAllAsync(CancellationToken cancellationToken = default);
```

Resolved from DI as `IConfigDataService`. Available to any project that takes a dependency on `OrangepuffPortal.Host` (directly or via NuGet).

### `IConfigDataAdminService` — admin CRUD (Bff-internal)

Called from the Bff gateway; not exposed directly to caller apps.

## Admin HTTP endpoints

All routes live under `/bff/admin` (AdminOnly policy):

| Method   | Route                  | Action          |
|----------|------------------------|-----------------|
| `GET`    | `/bff/admin/config-data`       | List all rows   |
| `POST`   | `/bff/admin/config-data`       | Create a row    |
| `PUT`    | `/bff/admin/config-data/{id}`  | Update a row    |
| `DELETE` | `/bff/admin/config-data/{id}`  | Delete a row    |

## Projects

| Project                              | Role                                                        |
|--------------------------------------|-------------------------------------------------------------|
| `OrangepuffPortal.ConfigData.Contract` | Public DTOs, result types, `IConfigDataService`, `IConfigDataAdminService` |
| `OrangepuffPortal.ConfigData`          | Entity (`ConfigDataEntry`), EF Core, repository, cache, services, `IHostedService` warmer, `IPortalModule` |
| `OrangepuffPortal.Bff`                 | `IConfigDataGateway` + `ConfigDataAdminEndpoints` (Minimal API) |
| `OrangepuffPortal.Host`                | Calls `AddConfigDataModule()` from `AddOrangepuffPortal()`  |

## Usage in a caller app

```csharp
// Inject IConfigDataService — no extra registration needed if AddOrangepuffPortal() is called
public class MyService(IConfigDataService configData)
{
    public async Task DoSomethingAsync(CancellationToken ct)
    {
        var maxRetries = await configData.GetAsync("MyFeature:MaxRetries", ct);
        // ...
    }
}
```
