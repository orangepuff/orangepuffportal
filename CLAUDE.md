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
