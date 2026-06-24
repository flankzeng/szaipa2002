using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <summary>
/// Write-side repository for the Works (artist artwork) module, replacing the legacy <c>ArtWorks*</c> actions
/// (the parallel <c>WorkAdd/WorkEdit</c> flow wrote a different, unrendered image folder and a Width/Height/
/// transverse/long set that nothing on the public site reads — dropped rather than ported; see Noun below).
/// </summary>
public sealed class WorksAdminRepository : ArtistScopedAdminRepository<Works>
{
    public WorksAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
        : base(db, operationRecorder)
    {
    }

    protected override DbSet<Works> Set => Db.Works;

    protected override string Noun => "作品";

    protected override void ApplyEditableFields(Works target, Works input)
    {
        target.ArtistId = input.ArtistId;
        target.Title = input.Title;
        target.Content = input.Content;
        target.Tags = input.Tags;
        if (!string.IsNullOrEmpty(input.Path))
        {
            target.Path = input.Path;
        }
    }
}
