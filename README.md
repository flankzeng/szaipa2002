# Szaipa modern web

This repository is the modernization workspace for the Shenzhen Art Industry Promotion Association website. The deployable application is ASP.NET Core on .NET 10; the legacy ASP.NET MVC5 project is retained only in the original repository as historical reference and is not part of the intended modern repository.

## Modern source boundary

The independent modern repository should contain only:

- `src/Szaipa.Web/`
- `src/Szaipa.Data/`
- `tests/`
- `scripts/`
- `docs/`
- `Szaipa.Modernization.slnx`
- `global.json`
- `.editorconfig`, `.gitattributes`, and `.gitignore`
- this `README.md`
- future `.github/` CI configuration

It must not contain `szaipa2022/`, either legacy solution, legacy `Web.config`, the legacy `Content` tree, `.vs/`, `bin/`, `obj/`, `node_modules/`, machine-local appsettings, or Data Protection keys.

See [docs/repository-split.md](docs/repository-split.md) for the migration procedure and release contract.

## Build and verify

From the repository root:

```bash
npm --prefix src/Szaipa.Web ci
npm --prefix src/Szaipa.Web run build
dotnet build Szaipa.Modernization.slnx --disable-build-servers -m:1
dotnet test Szaipa.Modernization.slnx --no-build --disable-build-servers -m:1
dotnet publish src/Szaipa.Web/Szaipa.Web.csproj -c Release -o ./artifacts/publish
```

The current verified baseline is a clean .NET build, 244 passing tests, and a successful npm build. A release artifact is not production-ready merely because it builds: the external runtime contracts below must also be satisfied.

## External runtime contracts

The modern repository deliberately does not own these production resources:

- **Legacy assets:** `LegacyAssets:ContentRoot` must point to the existing external `Content` directory. The public site still uses `/Content` images, previews, icons, fonts, and shared CSS/JavaScript.
- **Databases:** the public site reads the existing `Szaipa` and `Tongou` SQL Server databases through least-privilege read-only accounts. Production connection strings belong in IIS/app-pool environment variables or protected server configuration, never Git.
- **Data Protection:** production must configure `DataProtection:KeysPath` as an absolute persistent directory outside versioned release folders and keep `ApplicationName` stable as `Szaipa.Web`. The supported Production target is Windows IIS; persisted keys are encrypted at rest with machine-scoped DPAPI and verified at startup. Production fails closed when the path/platform contract is invalid.
- **Staff writes:** the rewritten `/Staff` backend and the legacy backend must never be enabled as simultaneous writers without explicit human approval. During side-by-side validation, keep the modern backend write path disabled unless it is the chosen sole writer.

No deployment process may copy, delete, rename, or clean the external `Content` tree, apply database SQL automatically, or commit production secrets.

This is separation, not deletion: the current modern tracked boundary is about 20.4 MiB, while the original tracked legacy `Content` tree is about 1,082.99 MiB and the local reference `web24.05/Content` is about 1.2 GiB. A complete deployment therefore consists of the replaceable modern application plus the preserved external Content volume. Pushing the existing branch history directly would also retain roughly 1.8 GiB of packed legacy history. The new repository must therefore start from a sanitized whitelist snapshot as one fresh root commit, with the original source SHA recorded; neither changing the remote nor retaining path-filtered historical blobs is acceptable.

## Deployment posture

CI should build an immutable artifact identified by commit SHA. The Windows deployment step should unpack it into a new release directory, validate it on the side-by-side IIS site, switch the IIS physical path atomically, and restore the previous path if health checks fail. Direct `git pull` or in-place publish into the active IIS directory is not the release contract.

The current operational guide is [docs/deploy-windows.md](docs/deploy-windows.md). It must be updated alongside the future CI and atomic deployment implementation before production cutover.

## Current scope

The repository split is documented but has not been executed. No new remote repository is created by this documentation change, and no server, IIS binding, deployment hook, database, or production asset is modified.
