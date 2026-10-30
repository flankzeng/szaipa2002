namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Writable entity for the legacy <c>dbo.Staff</c> table (admin accounts). Property names mirror the EF6
/// Database-First model so a later <c>dotnet ef dbcontext scaffold</c> diffs cleanly. <see cref="Password"/>
/// is either the salted PBKDF2 form <c>p1$…</c> or, until that account next signs in, the legacy lowercase
/// 32-char MD5 hex digest (see <c>StaffPasswordHasher</c>).
/// </summary>
public sealed class Staff
{
    public int Id { get; set; }

    public string? StaffName { get; set; }

    public string? Password { get; set; }

    public string? OperationRecord { get; set; }
}
