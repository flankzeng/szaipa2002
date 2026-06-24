namespace Szaipa.Data.Services.Admin;

/// <summary>The signed-in staff member performing a write, used for edit/operation-record stamping.</summary>
public sealed record AdminActor(int StaffId, string StaffName);
