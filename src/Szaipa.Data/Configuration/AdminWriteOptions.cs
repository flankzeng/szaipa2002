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

    /// <summary>Connection string to the local writable copy. Resolved from config/env at startup.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Diagnostic label describing where <see cref="ConnectionString"/> came from.</summary>
    public string ConnectionStringSource { get; set; } = "None";

    /// <summary>True only when writes are enabled and a connection string has been resolved.</summary>
    public bool IsConfigured =>
        EnableWrites && !string.IsNullOrWhiteSpace(ConnectionString);
}
