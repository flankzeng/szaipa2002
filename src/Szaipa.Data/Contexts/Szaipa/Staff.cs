namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Writable entity for the legacy <c>dbo.Staff</c> table (admin accounts). Property names mirror the EF6
/// Database-First model so a later <c>dotnet ef dbcontext scaffold</c> diffs cleanly. <see cref="Password"/>
/// is a lowercase 32-char MD5 hex digest of the UTF-8 password (legacy <c>Get_MD5</c>).
/// </summary>
public sealed class Staff
{
    public int Id { get; set; }

    public string? StaffName { get; set; }

    public string? Password { get; set; }

    public string? OperationRecord { get; set; }
}
