// Packs the built library into a .tgz after `ng build`: consuming the dist folder via a
// plain `file:` dependency makes npm create a symlink, and a symlinked package's imports of
// @angular/core resolve from ITS OWN node_modules (this workspace's), not the consumer's —
// two separate @angular/core instances in memory breaks DI (NG0203 at runtime). Depending on
// the packed .tgz instead makes npm extract a real copy into the consumer's node_modules, so
// @angular/core resolves to the consumer's single copy like any other real dependency.
const { execSync } = require('node:child_process');
const { join } = require('node:path');

const distDir = join(__dirname, '..', 'dist', 'portal-frontend');

execSync('npm pack', { cwd: distDir, stdio: 'inherit' });
