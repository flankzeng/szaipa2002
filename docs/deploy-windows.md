# Deploy to the Windows server (Mac dev → Windows publish)

Workflow: develop/debug on the Mac, publish a build artifact, host it on the existing Windows server **under IIS,
side-by-side with the live MVC5 site** (on a separate test port first — the live `szaipa.com` is never touched
until you have validated the new site on real data).

The new app is ASP.NET Core (net10.0). `dotnet publish` already emits an IIS-ready `web.config`
(`AspNetCoreModuleV2`, in-process). The existing live site stays on its own IIS site / app pool.

---

## 1. Publish on the Mac

From the repo root:

```bash
~/.dotnet/dotnet publish src/Szaipa.Web/Szaipa.Web.csproj -c Release -o ./publish
```

- Framework-dependent (small artifact; the server provides the runtime via the Hosting Bundle in step 2).
- The artifact does **NOT** contain `appsettings.Local.json` (excluded in the .csproj) — secrets never ship.
  You create the server's config in step 4.
- The artifact does **NOT** contain the ~1 GB legacy `Content` folder — it is served from an external path
  (`LegacyAssets:ContentRoot`, step 4) that must already exist on the server.

Copy `./publish` to the server, e.g. `C:\inetpub\szaipa-new\`.

## 2. Server prerequisite: ASP.NET Core 10 Hosting Bundle

Install once on the Windows server (provides the `AspNetCoreModuleV2` IIS module + the .NET 10 runtime):

```powershell
winget install Microsoft.DotNet.HostingBundle.10
# or download "ASP.NET Core Runtime 10.x - Windows Hosting Bundle" from dotnet.microsoft.com
iisreset   # so IIS picks up the new module
```

## 3. Create the IIS site (side-by-side, test port)

- IIS Manager → Sites → **Add Website**: name `szaipa-new`, physical path `C:\inetpub\szaipa-new\`,
  binding `http` port `8080` (a test port; leave the live site's 80/443 alone). A test subdomain is also fine.
- Application pool: **No Managed Code** (.NET CLR Version = "No Managed Code" — ASP.NET Core runs out of process
  of the CLR; the AspNetCoreModule launches it).
- Give the app pool identity **read** access to `C:\inetpub\szaipa-new\` and to the legacy `Content` folder.

## 4. Production config on the server (secrets live here, not in the repo/artifact)

Create `C:\inetpub\szaipa-new\appsettings.Production.json` directly on the server. It is loaded automatically
(ASPNETCORE_ENVIRONMENT defaults to `Production`). Use the **read-only** logins and `Server=localhost` (the DB
is on the same box):

```json
{
  "RuntimeSafety": { "UseLegacyDataSources": true, "AllowLiveDatabase": false, "UseHttpsRedirection": false },
  "LegacyData": {
    "Szaipa": { "AccessMode": "ReadOnly", "ConnectionString": "Server=localhost,1433;Database=Szaipa;User Id=szaipa_ro;Password=__SET__;Encrypt=True;TrustServerCertificate=True;" },
    "Tongou": { "AccessMode": "ReadOnly", "ConnectionString": "Server=localhost,1433;Database=Tongou;User Id=tongou_ro;Password=__SET__;Encrypt=True;TrustServerCertificate=True;" }
  },
  "ReadOnlyMigration": { "EnableSzaipaReadModels": true, "EnableTongouReadModels": true },
  "LegacyAssets": { "ContentRoot": "C:\\inetpub\\szaipa\\Content" }
}
```

- Set `ContentRoot` to the server's existing legacy `Content` directory (the one the live MVC5 site serves, or a
  copy). Use doubled backslashes in JSON.
- Keep `AllowLiveDatabase:false` and `AccessMode:ReadOnly`: the data layer hard-throws on any write and uses
  `AsNoTracking`, so even pointed at production the app cannot write. Use the least-privilege `db_datareader`
  logins only — never the `sa` / Web.config production credentials.
- Lock down NTFS permissions on this file (it contains the read-only password).

## 5. Validate (real data, live untouched)

Browse on the server (or from your Mac if the test port is reachable):

- `http://<server>:8080/healthz` → confirm `"liveDatabase":false`, `"hasReadWriteLegacySource":false`, and the
  Szaipa/Tongou connection sources show `HasConnectionString=true`.
- `http://<server>:8080/Home/newIndex`, `/Home/newnews`, `/Home/newnewsread/1124`, `/Home/newArt/1000`,
  `/Home/newvip`, `/Home/newabout`, `/Home/PublicationList`, `/Publication/chunyu` → 200，使用真实只读内容。
- `/Publication/index` → 301 到 `/Home/PublicationList`。
- `/Publication/zengfeng`、`/Project_Tongou/Atrist/1`、`/Project_Tongou/beini` → 410（已退役且不查询数据库）。

## 6. Cut over (only after validation)

Once the side-by-side site is verified, switch the public binding (host header / port 80/443) from the old MVC5
site to `szaipa-new`, or repoint the reverse proxy. Keep the old site stopped-but-present for fast rollback.

---

### Notes
- `dotnet run` on the Mac stays the dev/debug loop; this artifact path is only for the server.
- Config precedence: `appsettings.json` → `appsettings.{Environment}.json` (Production on the server) →
  `appsettings.Local.json` (gitignored, Mac dev only; not in the artifact) → environment variables.
- If you prefer not to keep secrets in a file, set them as env vars instead (e.g. `LegacyData__Szaipa__ConnectionString`,
  `LegacyAssets__ContentRoot`) in the app pool / system environment — the config chain reads those too.
