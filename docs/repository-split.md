# Independent modern repository and release contract

## Purpose and current boundary

The modern ASP.NET Core application has reached a clean project boundary and can be moved to a dedicated repository without carrying the MVC5 source or its large tracked asset tree. This document defines the allowed paths, external runtime dependencies, migration sequence, release acceptance criteria, and the manual decision required before enabling backend writes.

The user has created the dedicated target repository at `https://gitee.com/arthur953/szaipa2026.git` and a local clone at `~/Project/GitClone/szaipa2026`. At the current checkpoint it contains only its initial README commit: no modern source has been exported, no artifact has been published, and no server, IIS site, hook, database, or production `Content` directory has been changed.

The target's existing README commit creates one unresolved release decision. Keeping the strict single-fresh-root rule below requires explicit approval to replace the target `master` history; preserving that initial commit instead creates a documented two-commit deviation. Do not choose or force-push either outcome before the user's UI acceptance and explicit direction.

## Repository whitelist

The new repository may be created from the following tracked paths only:

| Path | Purpose |
|---|---|
| `src/Szaipa.Web/` | ASP.NET Core web application, Razor views, modern static assets, and staff backend |
| `src/Szaipa.Data/` | EF Core read/write contexts, entities, projections, and repositories |
| `tests/` | Web and data unit tests |
| `scripts/` | Font, Content, IIS audit, and future release scripts |
| `docs/` | Architecture, SQL prerequisites, handoff history, and deployment documentation |
| `Szaipa.Modernization.slnx` | The only solution used by the modern build |
| `global.json` | .NET SDK contract |
| `.editorconfig`, `.gitattributes`, `.gitignore` | Repository policy |
| `README.md` | Independent repository entry point |
| `.github/` | Future CI/release workflows, once reviewed and added |

Do not migrate:

- `szaipa2022/` or `szaipa2022.sln`;
- legacy `Web.config`, packages, DLLs, crash dumps, `.vs/`, `bin/`, or `obj/`;
- any copy of the legacy `Content` tree;
- `node_modules/`;
- `src/Szaipa.Web/appsettings.Local.json` or a real `appsettings.Production.json`;
- `src/Szaipa.Web/App_Data/` or Data Protection keys;
- machine-specific IDE state. The current `.vscode` settings contain a user-specific SDK path and should be excluded until generalized.

The modern projects have no `ProjectReference` to the MVC5 project and no runtime dependency on its assemblies. References to `web24.05` in comments and audit documents are historical or configuration examples, not source-code dependencies.

## Clean migration procedure

The split must be based on a clean committed revision, not a copy of the current working directory. This prevents ignored secrets, build outputs, local Data Protection keys, and unrelated legacy edits from entering the new history. The original remote already preserves the complete history and the pushed `legacy/archive-before-frontend-prune-20260710` branch; the new application repository does not need to duplicate it.

1. Finish and verify the intended modern branch in the original repository.
2. Confirm that only deliberate modern changes are committed; leave all unrelated legacy working-tree changes untouched.
3. Create a signed or annotated source-baseline tag and record its full commit SHA.
4. Export only the whitelist paths from that exact commit into a new disposable directory. Do not copy the active working directory and do not run history-rewriting commands in it.
5. Initialize a new repository in the exported directory and create one reviewed root commit. Its commit message and repository metadata must record the original baseline SHA and archive branch.
6. Scan the exported tree, the new commit, and every ref/object in the new repository for secrets. A clean checkout alone is insufficient. Any real credential or private key must be removed and rotated before any modern source or history is pushed to the target remote.
7. Perform all acceptance checks from a fresh clone of this local single-root repository.
8. Only after all checks pass, attach and populate the already-created target remote using the user-approved root-history choice.

The fresh-root rule is deliberate. A masked audit found that early modern `src/`/`docs/` history contained
connection-string examples without explicit placeholder markers. They do not match the known legacy
`Web.config` credentials and are not high-entropy, but their validity cannot be proven. Current tracked examples
are sanitized; carrying the old blobs into a new remote is unnecessary risk. `.vscode/` remains outside the
whitelist because its current SDK path is user-specific.

### Split acceptance checks

The fresh-root checkout is acceptable only when:

- `git status` is clean;
- `git ls-files` contains no `szaipa2022/`, legacy solution, `Web.config`, `Content`, `App_Data`, `bin`, `obj`, `node_modules`, or machine-local appsettings;
- no project XML references a path outside `src/` and `tests/`;
- the root commit records the original source-baseline SHA and legacy archive branch;
- a secret scanner has covered all refs and historical blobs, with its tool/version/result recorded; any real hit has been rotated and removed before the target remote is populated with modern source/history;
- npm build, .NET build, and all 245 current tests pass from a fresh clone;
- Release publish succeeds without access to the legacy source repository;
- the publish output contains neither secrets nor the external `Content` tree.

## External `Content` contract

The legacy `Content` directory remains an external runtime volume. The modern application maps the configured physical directory to request path `/Content` through `LegacyAssets:ContentRoot`.

This externalization does not discard images. At the current baseline the modern tracked boundary is about 20.4 MiB, the tracked legacy Content tree alone is about 1,082.99 MiB, and the local reference `web24.05/Content` is about 1.2 GiB. The deployed site still requires that full external Content volume; it is deliberately excluded only from the replaceable Git application artifact and history.

It currently supplies more than database images. Required roots include:

- `images`, `ArtImg`, and `newsImg`;
- `_preview` derivatives;
- `icon` and `123` branding assets;
- `Model` CSS/JavaScript still used on demand such as Magnify and jQuery (public Swiper 9.0.3 is now a versioned modern bundle);
- remaining shared publication CSS and dynamically addressed upload folders.

Production rules:

- locate `Content` outside both old and new versioned release directories;
- give the public application read permission only;
- grant narrowly scoped write permission only if the modern staff backend becomes the approved sole writer;
- never let a deploy or rollback script copy, clean, rename, or delete this directory;
- validate `/Content/123/favicon.ico` plus representative dynamic images before cutover;
- preserve the old site and Content path until the new deployment has passed its observation window.

Moving the production Content path to a neutral shared directory is a separate server change and requires explicit approval, its own backup, validation, and rollback plan.

## Database contract

The public application shares the existing `Szaipa` and `Tongou` SQL Server databases. Public contexts use read-only access and no-tracking queries. Production configuration must keep:

- `RuntimeSafety:UseLegacyDataSources=true`;
- `RuntimeSafety:AllowLiveDatabase=false`;
- both legacy sources at `AccessMode=ReadOnly`;
- least-privilege `db_datareader` credentials supplied outside Git.

Repository SQL files are prerequisites and review records, not automatic migrations. Before side-by-side validation, use read-only checks to confirm the expected `Publication` columns, `ExhibitionWork` table, and reserved publication IDs. Never place `dotnet ef database update` or automatic execution of `docs/sql/*.sql` in CI or the deployment hook.

The rewritten `/Staff` area uses separate admin connection strings when writes are enabled. `AllowLiveDatabase=false` protects the legacy read-source policy; it is not a universal kill switch for separately configured admin contexts.

## Data Protection contract

Atomic release directories require authentication keys that survive physical-path changes. The production contract is:

- a persistent keys directory outside `releases/<commit>`;
- a stable Data Protection application name, such as `Szaipa.Web`;
- Windows machine-scoped DPAPI encryption for keys at rest;
- IIS read/write permission only on that key directory;
- no key files in Git or the release artifact;
- key backup and restore behavior documented for the server. Machine-DPAPI ciphertext is bound to that Windows machine, so cross-machine disaster recovery requires a separately approved certificate-based migration strategy.

The code now reads `DataProtection:ApplicationName` and `DataProtection:KeysPath`. Development may omit the path and use the ignored project-local fallback; every non-Development environment fails before startup when the path is missing, relative, inside the current release root, or running on a non-Windows platform. Windows Production encrypts the explicit file key ring with machine-scoped DPAPI and performs a protect/unprotect round trip during startup so an unusable key path or DPAPI context fails before the site accepts traffic. `appsettings.Production.example.json` documents the secret-free contract and is excluded from publish output. The implemented acceptance tests verify:

- Production rejects a missing, relative, or release-local key path;
- Production requires the supported Windows DPAPI platform while Development remains cross-platform;
- Development retains a project-local fallback;
- two application instances with different release content roots, the same persistent key directory, and the same application name decrypt each other's protected token;
- a different application name cannot decrypt the token.

The final side-by-side IIS test must still prove that a real authenticated `/Staff` cookie remains valid across a release switch; that requires the server test site and is intentionally not simulated by local unit tests. The test site must use its own key directory, application name (for example `Szaipa.Web.Test`), cookie name, and dedicated hostname so it cannot decrypt, mint, or overwrite production Staff cookies. A different port alone does not isolate cookies. Perform the login and release A-to-B switch over HTTPS inside that same test app pool/ring; use the production ring and `Szaipa.Admin` cookie only at final cutover. Start with a fresh empty key directory because enabling DPAPI does not retroactively rewrite an existing plaintext ring.

## CI artifact contract

CI should verify source and create an immutable Release artifact; it must not deploy production from pull requests.

Minimum sequence:

1. create an ignored `artifacts/` workspace, then install a pinned supported Node version and the .NET 10 SDK from `global.json`;
2. run `npm ci` and `npm run build` under `src/Szaipa.Web`;
3. fail if the generated `admin.css`, `editor.js`, or `dashboard.js` differs from the committed output;
4. restore, build, and test `Szaipa.Modernization.slnx`;
5. publish `src/Szaipa.Web/Szaipa.Web.csproj` in Release mode;
6. audit the publish directory;
7. produce a zip, manifest, commit SHA, and SHA-256 checksum.

The artifact must include `Szaipa.Web.dll`, generated IIS `web.config`, compiled modern static assets, and local font subsets. It must not include local/production secrets, appsettings examples, package manifests, admin source files, `node_modules`, `App_Data`, `Content`, `.gitkeep`, or redundant `.br`/`.gz` files.

Only a successful protected branch or release tag may yield a deployable artifact. The server-side hook must verify the event source, selected branch/tag, artifact commit SHA, and checksum before invoking deployment.

## Windows atomic deployment contract

The release mechanism should consume the CI artifact instead of running `git pull` inside the active IIS directory.

Required behavior:

1. verify checksum and artifact contents before touching IIS;
2. extract to a new immutable `releases/<commit-sha>` directory;
3. obtain Production configuration from a protected external location or IIS environment variables;
4. leave Content and Data Protection directories external;
5. remember the current IIS physical path;
6. switch the side-by-side IIS site's physical path to the new release and recycle its app pool;
7. verify `/healthz`, representative public database routes, `/Content/123/favicon.ico`, and an exhibition route;
8. automatically restore the previous physical path and recycle again if any check fails;
9. retain the previous release for immediate rollback.

The deployment script should offer a validation-only mode that checks an artifact without changing IIS. A deliberate failed health check on the test site must prove that rollback restores the prior physical path. The hook must not modify bindings on the stable public site until side-by-side acceptance is complete.

## Manual confirmation: choose one backend writer

Sharing database tables and Content does not make concurrent backends risk-free. The old MVC backend and modern `/Staff` backend have different authentication, upload, gallery-ordering, and operation-record implementations. Before public cutover, the owner must explicitly choose one of these modes:

### Mode A: legacy backend remains the writer

- modern public routes use read-only database accounts;
- modern admin writes remain disabled;
- the legacy backend stays available through a separate internal host, port, or routing rule;
- only the legacy app receives database and Content write permission.

### Mode B: modern `/Staff` becomes the writer

- stop or disable write access through the legacy backend first;
- configure dedicated admin database credentials outside Git;
- grant the modern app narrowly scoped Content write permission;
- verify real staff login, anti-forgery, password change, each CRUD module, uploads, gallery ordering, works catalog, and operation records on the side-by-side site;
- confirm database schema prerequisites and complete backup/rollback preparation.

There is no implicit default for this decision. Production write credentials, IIS routing, or Content write permissions must not be enabled until the user confirms Mode A or Mode B.

## Cutover gate

Repointing a GitHub hook is the final integration step, not the first. It is permitted only after:

- the clean repository split and fresh-clone CI pass;
- the immutable artifact and checksum contract pass;
- external Content, database, and Data Protection contracts are configured on the test site;
- database schema and representative routes are verified against real read-only data;
- atomic switch and forced-failure rollback are demonstrated;
- the sole backend writer is explicitly chosen;
- the stable old IIS site and previous release remain available for rollback.

Until those gates are met, the current server, IIS bindings, deployment hook, databases, and production assets remain unchanged.
