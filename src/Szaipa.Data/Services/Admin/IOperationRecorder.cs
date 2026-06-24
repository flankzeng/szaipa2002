using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <summary>
/// Centralizes the operation-record logging the legacy controller open-coded everywhere: it appends a
/// message to today's <c>Diary.OperationRecord</c> and the acting staff member's <c>Staff.OperationRecord</c>.
/// It does NOT call SaveChanges — the caller saves so the log is committed in the same transaction as the
/// write it describes.
/// </summary>
public interface IOperationRecorder
{
    Task RecordAsync(SzaipaAdminContext db, AdminActor actor, string message, CancellationToken cancellationToken);
}
