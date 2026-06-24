namespace Szaipa.Web.Authorization;

/// <summary>
/// Authorization constants for the staff/admin backend (Areas/Admin). The <see cref="StaffPolicy"/> gates
/// every admin controller and replaces the legacy per-action <c>Session["Staff"]</c> null check.
/// </summary>
public static class AdminAuthorization
{
    public const string StaffPolicy = "Staff";
}
