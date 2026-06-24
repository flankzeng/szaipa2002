namespace Szaipa.Data.Configuration;

/// <summary>
/// Gates the writable Tongou(同构) admin path, mirroring <see cref="AdminWriteOptions"/> for the physically
/// separate Tongou database (same SQL Server instance as Szaipa, but a different DB — see
/// <see cref="Contexts.Tongou.TongouLegacyReadContext"/>). Writes are off unless <see cref="EnableWrites"/>
/// is explicitly true AND a dedicated connection string is supplied, pointing at a LOCAL writable copy.
/// </summary>
public sealed class TongouAdminWriteOptions
{
    public const string SectionName = "TongouAdminWrite";

    /// <summary>Master switch. When false, the Tongou admin write context is never constructed.</summary>
    public bool EnableWrites { get; set; }

    /// <summary>
    /// Local-debug convenience (mirrors <see cref="AdminWriteOptions.UseReadOnlyConnectionForDebug"/>): when
    /// true and no Tongou admin connection string is supplied, the Tongou admin context reuses the existing
    /// READ-ONLY Tongou connection (least-privilege <c>db_datareader</c>). Reads/login/inspection work; writes
    /// fail at the SQL level. Never set in committed/production config.
    /// </summary>
    public bool UseReadOnlyConnectionForDebug { get; set; }

    /// <summary>Connection string to the local writable copy. Resolved from config/env at startup.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Diagnostic label describing where <see cref="ConnectionString"/> came from.</summary>
    public string ConnectionStringSource { get; set; } = "None";

    /// <summary>True only when writes are enabled and a connection string has been resolved.</summary>
    public bool IsConfigured =>
        EnableWrites && !string.IsNullOrWhiteSpace(ConnectionString);
}
