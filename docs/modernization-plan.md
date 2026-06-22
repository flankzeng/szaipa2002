# Szaipa .NET Core Modernization Notes

## Architecture decisions

- Target runtime: `.NET 10 LTS`.
- Web framework: ASP.NET Core MVC with Razor views for the first migration phase.
- Data access: EF Core in a separate data project, generated from the existing SQL Server schema only after read-only database access is available.
- Database posture: disconnected by default; live database use must be an explicit local opt-in.
- Compatibility goal: `dotnet run` must work on both macOS and Windows with the same repository layout.

## Current findings

- The legacy site is an ASP.NET MVC 5 application targeting `.NET Framework 4.5.2`.
- Hosting depends on `System.Web` and IIS Express, which blocks normal Mac development.
- Data access uses EF6 Database First with two generated contexts: `SzaipaEntities` and `TongouEntities`.
- The legacy `Web.config` currently contains live connection strings, so the existing repository is not safe to run casually on a new machine.
- The latest publish directory at `/Users/arthur/Project/GitClone/web24.05` is a useful runtime baseline for asset and behavior parity checks.
- The current Mac has only .NET 6 and .NET 7 SDKs installed, so it needs .NET 10 SDK before the new project can be built again.

## Guardrails for migration

- The new ASP.NET Core app must default to **not connecting** to any legacy database.
- Local debugging defaults to HTTP only. HTTPS redirection can be enabled per-machine with `RuntimeSafety:UseHttpsRedirection=true`.
- ASP.NET Core DataProtection keys are stored under local ignored `src/Szaipa.Web/App_Data/` during development, not in the user profile.
- The new `src/Szaipa.Data` project defines `Szaipa` and `Tongou` as separate legacy data sources with explicit `Disabled`, `ReadOnly`, and `ReadWrite` modes.
- Any future live database access should require both:
  - `RuntimeSafety:UseLegacyDataSources=true`
  - `RuntimeSafety:AllowLiveDatabase=true`
- Read-only legacy access can stay under `RuntimeSafety:AllowLiveDatabase=false` as long as all enabled legacy sources remain in `ReadOnly` mode.
- Local connection strings should live only in `appsettings.Local.json`, which is ignored by Git.
- Read-only verification should be introduced before enabling any shared Windows-connected database in the new app.

## Suggested migration sequence

1. Freeze the old project as the reference implementation.
2. Stand up the new ASP.NET Core MVC host in `src/Szaipa.Web`.
3. Use `src/Szaipa.Data` as the home for EF Core reverse-engineered models and access policy.
4. Port one controller flow at a time, starting with read-only pages.
5. Move static assets and views in batches, comparing behavior against `web24.05`.
6. Add deployment profiles for both Mac local debug and Windows local debug before touching release publishing.

## Decisions resolved with GPT-5.5

- Use `.NET 10 LTS` instead of `.NET 8` because this modernization should not land close to the target runtime's end-of-support date.
- Keep Razor MVC for the first migration phase to reduce behavioral drift from the old MVC 5 site.
- Keep the two legacy contexts conceptually separate at first: `Szaipa` for the public site domain and `Tongou` for project/Tongou data.

## Local verification

On this Mac, .NET 10 SDK was installed to `/Users/arthur/.dotnet`.

```bash
/Users/arthur/.dotnet/dotnet build src/Szaipa.Web/Szaipa.Web.csproj
/Users/arthur/.dotnet/dotnet src/Szaipa.Web/bin/Debug/net10.0/Szaipa.Web.dll --urls http://127.0.0.1:5057
curl http://127.0.0.1:5057/healthz
```

Expected health check:

```json
{
  "status": "ok",
  "framework": "net10.0",
  "legacyDataSources": false,
  "liveDatabase": false,
  "hasReadWriteLegacySource": false,
  "configuredSources": {
    "Szaipa": "Disabled",
    "Tongou": "Disabled"
  },
  "connectionSources": {
    "Szaipa": {
      "source": "None",
      "hasConnectionString": false
    },
    "Tongou": {
      "source": "None",
      "hasConnectionString": false
    }
  }
}
```

## Modern solution

- `Szaipa.Modernization.slnx` is the new cross-platform solution entry point.
- `src/Szaipa.Web` hosts the ASP.NET Core MVC app.
- `src/Szaipa.Data` owns legacy data-source policy and the future EF Core migration surface.

## Read-only migration references

- `docs/legacy-readonly-migration.md` explains the guardrails and first-phase scope.
- `docs/home-read-batch-map.md` breaks the legacy `HomeController` flows into read-only migration batches.
- `docs/read-model-field-map.md` maps legacy entity fields to the new read models so EF Core implementation can stay mechanical.
- `docs/tongou-read-batch-map.md` breaks the public Tongou flows into read-only migration batches.
- `docs/tongou-field-map.md` maps Tongou legacy fields to the new Tongou read models.
- `src/Szaipa.Data/Scaffolding/LegacyRepositoryPlans.cs` defines the first-pass repository implementation blueprint shown by the new migration workspace.
- `src/Szaipa.Data/Composition/LegacyReadModelComposer.cs` centralizes first-pass snapshot composition and display rules that must stay consistent across repository implementations.
- `docs/pre-efcore-readiness-audit.md` is the Stage 3 sign-off: a per-route readiness matrix plus the legacy behavior source-of-truth values that activation must reproduce.
- `docs/efcore-first-implementation-checklist.md` defines the exact order and safety checks for the first real EF Core implementation pass.
- `docs/local-readonly-bootstrap.md` explains how to prepare a machine for the first verified read-only connection without weakening the current safety posture.
- `docs/local-debug-profiles.md` documents the current cross-platform launch profiles for Mac and Windows local debugging.
- `docs/view-and-asset-migration-map.md` defines where legacy publish views and assets should land in the new ASP.NET Core app.
- `docs/route-activation-map.md` tracks which legacy public routes already have ASP.NET Core endpoints reserved and what each route still needs before real activation.
- `docs/first-pass-asset-batches.md` narrows the first asset moves down to the route groups we are planning to activate first.
