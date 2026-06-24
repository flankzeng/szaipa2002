using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <inheritdoc />
public sealed class OperationRecorder : IOperationRecorder
{
    public async Task RecordAsync(
        SzaipaAdminContext db,
        AdminActor actor,
        string message,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var today = now.Date;

        // Today's diary row (create if missing) — mirrors the legacy today() helper, minus its eager save.
        var diary = await db.Diary.FirstOrDefaultAsync(d => d.Date == today, cancellationToken);
        if (diary is null)
        {
            diary = new Diary { Date = today, VistiTotal = 0 };
            db.Diary.Add(diary);
        }

        diary.OperationRecord += $"{now:HH:mm:ss}{actor.StaffName} {message}/";

        var staff = await db.Staff.FirstOrDefaultAsync(s => s.Id == actor.StaffId, cancellationToken);
        if (staff is not null)
        {
            staff.OperationRecord += $"{now:yyyy年MM月dd日 HH:mm:ss} {message}/";
        }
    }
}
