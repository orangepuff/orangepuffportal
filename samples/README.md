# Samples

Two thin, unpublished apps that exercise the `OrangepuffPortal.*` packages exactly the way an
external body app would — this is what makes them useful as a pre-publish check, not just a demo.

| Sample | Proves |
|---|---|
| `OrangepuffPortal.SampleHost` | The backend package set (`OrangepuffPortal.Host` + what it pulls in) wires up and runs standalone: `/bff/login` → Google OAuth → `/bff/me`, no body app needed |
| `OrangepuffPortal.SampleHost.Frontend` | The frontend package set (`@orangepuff/portal-frontend` + `@orangepuff/portal-frontend-shared`) renders and boots standalone against that host |

## OrangepuffPortal.SampleHost (backend)

References `OrangepuffPortal.Host` via `ProjectReference` (in-repo), not `PackageReference` — once
the NuGet packages are actually published, a real body app would instead add
`<PackageReference Include="OrangepuffPortal.Host" />`. There's no local-consumption gotcha on the
.NET side equivalent to the frontend one below (see [Configuring ports](../README.md#configuring-ports)
in the root README for why: a `ProjectReference` just compiles straight into the same assembly graph,
there's no separate-package-instance problem the way there is with two copies of `@angular/core`).

```powershell
docker compose up -d --build
```

- Host: http://localhost:5100 / https://localhost:7100

Requires (see the root README for details): the dev cert at `certs/orangepuffportal-devcert.pfx`,
an external SQL Server reachable at `host.docker.internal` with the `OrangepuffPortalSample`
database created, and `.env` filled in from `.env.example` (`DEV_CERT_PASSWORD`, `GOOGLE_CLIENT_ID`/
`GOOGLE_CLIENT_SECRET` from a Google Cloud OAuth client registered against `https://localhost:7100`
— see [Configuring Google sign-in](../README.md#configuring-google-sign-in), `SEED_ADMIN_PASSWORD`).

## OrangepuffPortal.SampleHost.Frontend (frontend)

**Why this isn't a plain `file:` reference to the library's `dist/` folder.** Its
[package.json](OrangepuffPortal.SampleHost.Frontend/package.json) depends on
`@orangepuff/portal-frontend`/`-shared` via `file:` paths that point at each library's **packed
`.tgz`**, not its `dist/` directory:

```json
"@orangepuff/portal-frontend": "file:../../src/OrangepuffPortal.Frontend/dist/portal-frontend/orangepuff-portal-frontend-0.0.1.tgz",
"@orangepuff/portal-frontend-shared": "file:../../src/OrangepuffPortal.Frontend.Shared/dist/portal-frontend-shared/orangepuff-portal-frontend-shared-0.0.1.tgz"
```

A plain `file:` reference to a directory makes npm **symlink** it into `node_modules` instead of
copying it. A symlinked package resolves its own `@angular/core` import from *its own*
`node_modules` (this workspace's), not the consumer's — two separate `@angular/core` instances end
up loaded at once, which breaks Angular DI at runtime (`NG0203`). Depending on the packed `.tgz`
instead makes npm **extract a real copy** into the sample's `node_modules`, exactly like a real
`npm install` from the registry would — so `@angular/core` resolves to the sample's single copy,
same as any other real dependency. `scripts/pack.js` (Frontend) and `scripts/copy-assets.js`
(Frontend.Shared) run `npm pack` as the last step of each library's `npm run build` specifically to
produce that tarball.

This is also why both libraries build with `"compilationMode": "partial"` (see each project's
`tsconfig.lib.prod.json`) rather than `"full"` — `partial` is what `npm publish` requires (it refuses
`full`-mode output, see below), and the whole point of consuming the packed `.tgz` here is to prove
the *exact* artifact that will eventually be published actually works, not something merely similar
to it.

**Build order matters** — Frontend depends on Frontend.Shared's tarball:

```powershell
# 1. Build Frontend.Shared first — produces dist/portal-frontend-shared/*.tgz
cd src/OrangepuffPortal.Frontend.Shared
npm install
npm run build

# 2. Build Frontend — its package.json already points at Frontend.Shared's tarball above,
#    so this must run after step 1 produced it
cd ../OrangepuffPortal.Frontend
npm install
npm run build

# 3. Install the sample frontend — picks up both freshly-built tarballs
cd ../../samples/OrangepuffPortal.SampleHost.Frontend
npm install

# 4. Run it
ng serve
```

- Frontend: https://localhost:5599 (proxies `/bff/*` to the sample host at https://localhost:7100 —
  see `proxy.conf.json`; both must agree with whatever port the host is actually running on, see
  [Configuring ports](../README.md#configuring-ports))

Requires the dev cert pair at `certs/orangepuffportal-devcert.pem`/`.key` (same `certs/` folder as
the host's `.pfx` — Angular's dev server needs the PEM/key form, Kestrel needs the `.pfx` form).

**If you change Frontend/Frontend.Shared source and want the sample to pick it up**: rebuilding the
library alone isn't enough — restarting `ng serve` won't reinstall anything, and npm has already
extracted the old tarball's contents into the sample's `node_modules`. Rebuild the changed
library/libraries (step 1 and/or 2 above), then re-run `npm install` in the sample frontend (step 3)
before restarting `ng serve`. If npm still doesn't pick up the change (same version number, cached
resolution), force it:

```powershell
Remove-Item -Recurse -Force node_modules\@orangepuff
npm install
```
