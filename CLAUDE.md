# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Frontend library local dev loop

`src/OrangepuffPortal.Frontend` (`@orangepuff/portal-frontend`) and `src/OrangepuffPortal.Frontend.Shared`
(`@orangepuff/portal-frontend-shared`) are Angular libraries consumed by `samples/OrangepuffPortal.SampleHost.Frontend`
(and by downstream apps like OCRWeb) via a packed **tarball**, not a symlink — each consumer's `package.json` points at
`file:.../dist/<package>/orangepuff-<package>-0.0.1.tgz`.

**After changing anything under either library's `projects/*/src`, rebuilding is not enough to see the change in
`OrangepuffPortal.SampleHost.Frontend` (or any other tarball consumer).** Both npm and the Angular/Vite dev-server
dependency optimizer key their caches off the package **version string**, not tarball content. Since the version stays
`0.0.1` across rebuilds, `npm install` treats the tarball as unchanged and the consumer keeps running the stale
compiled bundle — even after fully killing and restarting `ng serve` and deleting `.angular/` /
`node_modules/.vite`. This is easy to misdiagnose as "the change isn't working" when the change is actually fine.

To force a rebuild to actually take effect in a tarball consumer while iterating locally:

1. In the changed library's `package.json` (e.g. `projects/portal-frontend/package.json`), temporarily bump
   `version` to something new (e.g. `0.0.1-dev1`).
2. Rebuild + repack: `npm run build` in the library's root (`src/OrangepuffPortal.Frontend` or
   `src/OrangepuffPortal.Frontend.Shared`). This regenerates the `.tgz` under `dist/<package>/` with the new
   version in its filename.
3. In the consumer's `package.json` (e.g. `samples/OrangepuffPortal.SampleHost.Frontend/package.json`), update the
   `file:` dependency to point at the new tarball filename.
4. In the consumer: delete `node_modules/@orangepuff/<package>`, `.angular/`, and `node_modules/.vite`, then
   `npm install`.
5. Restart `ng serve`.
6. Once verified, **revert the version bump** in the library's `package.json` back to `0.0.1`, rebuild/repack at
   that version, and point the consumer's `file:` reference back at the `-0.0.1.tgz` filename — don't leave a
   `-devN` version committed.

Do this every time you change frontend library code and need to see the effect in a sample/consumer app, not just
once per session — each new rebuild needs its own fresh version bump to bust the cache again.

## No hardcoded user-facing text

Every string a user sees — labels, buttons, column headers, hints, dialog titles, snackbar/toast
messages (both success and failure) — comes from `ConfigTextDefinition`, not a literal in source.
The only exception is log messages (`logger.LogInformation(...)`, `console.log(...)`, etc.) — those
stay as plain hardcoded strings, they're for developers, not end users.

**Backend**: inject `ITranslation` (`OrangepuffPortal.Shared.Translation`) — a single shared service,
not scoped to one module — and call `await translation.TranslateAsync(code, module, ct)`. Resolves
for the current user's culture (`ICurrentUser.CultureCode`), falls back to the `"*"` wildcard row,
and finally to `code` itself if nothing matches at all, so a missing translation is visibly wrong
(shows the raw code) instead of silently blank. Registered once by `AddConfigTextModule` (see
`OrangepuffPortal.ConfigText/Infrastructure/Translation.cs`), so it's available to every module and
any consuming app's own modules without extra wiring.

Where to call it depends on the module's shape:
- **A module with a separate Bff-facing DTO from its Application-layer result** (MediatR-based, e.g.
  Identity): translate at the point the Bff gateway remaps the Application result onto its own DTO —
  see `OrangepuffPortal.Bff/Infrastructure/IdentityGateway/IdentityGateway.cs`. The Application layer
  itself keeps returning raw codes (e.g. `AddUserResult.Rejected("username_taken")`); only the
  gateway resolves them to text. Success codes are hardcoded per gateway method (e.g. `AddUserAsync`
  always uses `"user_created"`) since the Application layer has no success-code concept of its own.
- **A module whose own Result type flows straight through to the Bff response** (no MediatR, e.g.
  Config, ConfigText): translate inline in the admin/application service itself, right before
  constructing the result — see `OrangepuffPortal.Config/Infrastructure/ConfigCatalogAdminService.cs`'s
  `TranslateAsync` helper.

Every mutation Result record carries **both** `RejectionReason` (translated, null on success) and
`SuccessMessage` (translated, null on failure) — see any file under `Infrastructure/IdentityGateway/`
or `*.Contract/*AdminResult.cs` for the shape.

New seed entries go in `OrangepuffPortal.Host/ConfigText/en-US.json` (loaded automatically by
`PortalShellTextPortalModule` for the portal's own shell/admin text, or a consuming app's own
`ConfigText/{culture}.json` for its own module text — see `docs/config-text-design.md`). Reuse
`OrangepuffPortal.Common` for truly universal words (Save/Cancel/Edit/Delete/etc.) instead of
duplicating them per screen.

**Frontend**: inject `TranslationService` (`portal-frontend/src/lib/translation/`) or use the
`translate` pipe in templates — `{{ 'admin.config.title' | translate:'OrangepuffPortal.Frontend' }}`.
Same fallback rule as the backend (falls back to the code itself). The whole culture's text set is
preloaded once at app bootstrap via `provideAppInitializer()` inside `providePortalShell()`, using the
guest culture `"en-US"` — no signed-in user is known yet at that point. Once `AuthService.checkSession()`
resolves a user (called by the auth/admin guards, the header, and the landing page), `AuthService`
switches `TranslationService` over to that user's own `CurrentUser.cultureCode` (`TranslationService.reloadForCulture`,
a no-op if unchanged) via `identity`.`Users`.`sCultureCode`; `AuthService.logout()` switches it back to
`"en-US"`. No extra wiring is needed in a consuming app — this all happens inside `AuthService`. For
interpolated messages (e.g. `Delete user "{0}"?`), seed the `{0}`-style placeholder text and resolve
with `TranslationService.getFormatted(code, module, ...args)`.
