# Local Read-Only Bootstrap

## Goal

This note describes how to prepare a Mac or Windows machine for the first read-only EF Core pass without normalizing dangerous defaults.

## Current configuration chain

`src/Szaipa.Web/Program.cs` loads configuration in this order:

1. `appsettings.json`
2. `appsettings.Development.json`
3. `appsettings.Local.json`
4. environment variables
5. command-line arguments

This means local machine overrides can stay untracked and still win over the repo defaults.

## Safe starting point

- Copy the shape from `src/Szaipa.Web/appsettings.Local.example.json`.
- Keep `RuntimeSafety:UseLegacyDataSources=false`.
- Keep `RuntimeSafety:AllowLiveDatabase=false`.
- Keep unused legacy sources at `AccessMode=Disabled`.
- Only flip `ReadOnlyMigration` feature flags after scaffolded code exists and has been reviewed.

## Connection-string rule

- Real SQL Server credentials must stay out of Git.
- Prefer environment variables such as:
  - `SZAIPA_READONLY_CONNECTION`
  - `TONGOU_READONLY_CONNECTION`
- If a local `appsettings.Local.json` temporarily carries a connection string for debugging, it must remain ignored and read-only in intent.
- Current resolution order inside `src/Szaipa.Data` is:
  - `LegacyData:{Source}:ConnectionString`
  - source-specific read-only environment variable
  - `ConnectionStrings:{Source}`

## First enablement sequence

1. Prepare local config with all runtime safety flags still false.
2. Export the verified read-only connection string as an environment variable.
3. Run the scaffold command from the migration dashboard or `docs/efcore-scaffold-checklist.md`.
4. Review generated contexts and entities.
5. Implement repository methods with `AsNoTracking()`.
6. Only then consider enabling `RuntimeSafety:UseLegacyDataSources=true` in a local debug session while keeping all active legacy sources in `ReadOnly`.

## Important runtime guard

The current app throws on startup when:

- `RuntimeSafety:UseLegacyDataSources=true`
- and at least one configured legacy source is `ReadWrite`
- and `RuntimeSafety:AllowLiveDatabase=false`

This guard is intentional. It prevents casual startup against the old Windows-connected database before we deliberately approve write access.

## Diagnostics you can trust

- The dashboard home page shows each legacy source's access mode.
- It also shows where the resolved connection string came from:
  - `LegacyData`
  - `Environment`
  - `ConnectionStrings`
  - `None`
- `/healthz` exposes the same information without printing the actual connection string.

## Recommendation for the first real connection day

- Keep one branch dedicated to scaffold + repository wiring.
- Touch only one repository family at a time.
- Verify output against `/Users/arthur/Project/GitClone/web24.05` before turning on the next page group.

## Required environment variables

| Variable | Used for | Notes |
| --- | --- | --- |
| `SZAIPA_READONLY_CONNECTION` | Scaffolding + read access to the public site database (`Szaipa` source) | Read-only SQL login; never committed |
| `TONGOU_READONLY_CONNECTION` | Scaffolding + read access to the Tongou database (`Tongou` source) | Read-only SQL login; never committed |

Both are resolved by `src/Szaipa.Data` after `LegacyData:{Source}:ConnectionString` and before `ConnectionStrings:{Source}`. They never need to be tracked in Git.

## Read-permission verification points (do this before scaffolding)

Run these against the exact login string you plan to put in the environment variable. The login should be a member of `db_datareader` only — not `db_datawriter`, `db_ddladmin`, or `db_owner`.

1. **Confirm role membership is read-only:**
   ```sql
   SELECT r.name AS role_name
   FROM sys.database_role_members m
   JOIN sys.database_principals r ON m.role_principal_id = r.principal_id
   JOIN sys.database_principals u ON m.member_principal_id = u.principal_id
   WHERE u.name = USER_NAME();
   ```
   Expect `db_datareader` and nothing that grants writes.
2. **Positive read check:** `SELECT TOP (1) * FROM News;` (and `SELECT TOP (1) * FROM TongouWorks;` against the Tongou login) should succeed.
3. **Negative write check (proof of no write permission):** deliberately attempt a no-op write and confirm SQL Server **denies** it rather than running it:
   ```sql
   -- must fail with: The UPDATE permission was denied...
   UPDATE News SET ReadCount = ReadCount WHERE 1 = 0;
   ```
   If this succeeds, the login is **not** safe — stop and request a true read-only login before continuing.

## Proving the app cannot write

- App settings: keep every active legacy source at `AccessMode=ReadOnly`. The startup guard (see below) hard-fails if a `ReadWrite` source is enabled while `AllowLiveDatabase=false`.
- Repository code: the first EF Core pass uses `AsNoTracking()` and never calls `SaveChanges()`. The dashboard **Public read-only link readiness** table lists, per route, the legacy write behaviors that must stay removed (`ReadCount++`, `VisitCount++`, `VisityCount++`, `Diary` writes, `SaveChanges()`).
- Defense in depth: even with a correct read-only SQL login (the real guarantee), the app keeps the no-write posture so a future credential mistake cannot silently start mutating data.

## Debugging on macOS vs Windows

Both platforms use the same repository layout and the same environment-variable names; only the shell mechanics differ. The connection strings below contain placeholders only; never replace them in this tracked document.

### macOS (this machine)

```bash
export SZAIPA_READONLY_CONNECTION='Server=__SQL_HOST__;Database=...;User Id=__READONLY_USER__;Password=__SET_ME__;TrustServerCertificate=True'
export TONGOU_READONLY_CONNECTION='Server=__SQL_HOST__;Database=...;User Id=__READONLY_USER__;Password=__SET_ME__;TrustServerCertificate=True'
/Users/arthur/.dotnet/dotnet run --project src/Szaipa.Web/Szaipa.Web.csproj --urls http://127.0.0.1:5057
```

- HTTP only by default; leave `RuntimeSafety:UseHttpsRedirection` unset.
- Persist the variables in `~/.zshrc` for repeat sessions, or keep them in an ignored `appsettings.Local.json`.
- For the read/write proof above, use `sqlcmd` (from `mssql-tools`) or Azure Data Studio.

### Windows

```powershell
setx SZAIPA_READONLY_CONNECTION "Server=__SQL_HOST__;Database=...;User Id=__READONLY_USER__;Password=__SET_ME__;TrustServerCertificate=True"
setx TONGOU_READONLY_CONNECTION "Server=__SQL_HOST__;Database=...;User Id=__READONLY_USER__;Password=__SET_ME__;TrustServerCertificate=True"
# open a new shell so setx values are visible, then:
dotnet run --project src\Szaipa.Web\Szaipa.Web.csproj --urls http://127.0.0.1:5057
```

- `dotnet` is on `PATH`; no absolute path needed.
- HTTPS redirection can be enabled per-machine with `RuntimeSafety:UseHttpsRedirection=true`.
- For the read/write proof above, use SSMS or Azure Data Studio.
- See `docs/local-debug-profiles.md` for the launch-profile details shared by both platforms.

Confirm `/healthz` reports `liveDatabase:false` and `hasReadWriteLegacySource:false` on either platform before and after the first read-only connection.
