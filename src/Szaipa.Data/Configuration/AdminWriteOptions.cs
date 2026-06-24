namespace Szaipa.Data.Configuration;

/// <summary>
/// Gates the writable staff-admin path. The admin backend mutates data, so — unlike the read-only public
/// migration — it needs a write-capable context. To honour the project's safety posture (read-only by
/// default, never the production / Windows-connected database, never production credentials), writes are
/// off unless <see cref="EnableWrites"/> is explicitly true AND a dedicated connection string is supplied.
/// That connection must point at a LOCAL writable copy of the data, kept in machine-specific config
/// (<c>appsettings.Local.json</c> / env var <c>SZAIPA_ADMIN_CONNECTION</c>), never committed.
/// </summary>
public sealed class AdminWriteOptions
{
    public const string SectionName = "AdminWrite";

    /// <summary>Master switch. When false, the admin write context is never constructed.</summary>
    public bool EnableWrites { get; set; }

    /// <summary>
    /// Local-debug convenience: when true and no admin connection string is supplied, the admin context
    /// reuses the existing READ-ONLY Szaipa connection (the least-privilege <c>db_datareader</c> login). This
    /// lets a developer log in and inspect the backend without standing up a writable copy — reads work, and
    /// any write fails at the SQL level because the account cannot write (provably read-only). Never set this
    /// in committed/production config; production must point at a real writable copy.
    /// </summary>
    public bool UseReadOnlyConnectionForDebug { get; set; }

    /// <summary>
    /// Username for the local-debug fixed account. Only honoured when <see cref="UseReadOnlyConnectionForDebug"/>
    /// is true — lets a developer sign in WITHOUT a real Staff row (so inspection works even when the prod
    /// Staff table is unreachable). Never enabled in committed/production config; defaults to <c>debug</c>.
    /// </summary>
    public string DebugUserName { get; set; } = "debug";

    /// <summary>Password for the local-debug fixed account (see <see cref="DebugUserName"/>); defaults to <c>debug</c>.</summary>
    public string DebugPassword { get; set; } = "debug";

    /// <summary>True when the local-debug fixed-account login is active (the read-only-debug flag is on).</summary>
    public bool DebugLoginEnabled => UseReadOnlyConnectionForDebug;

    /// <summary>Connection string to the local writable copy. Resolved from config/env at startup.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Diagnostic label describing where <see cref="ConnectionString"/> came from.</summary>
    public string ConnectionStringSource { get; set; } = "None";

    /// <summary>True only when writes are enabled and a connection string has been resolved.</summary>
    public bool IsConfigured =>
        EnableWrites && !string.IsNullOrWhiteSpace(ConnectionString);
}
