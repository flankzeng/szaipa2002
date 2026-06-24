using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side repository for the Exhibition (per-artist exhibition feed) module.</summary>
public sealed class ExhibitionAdminRepository : ArtistScopedAdminRepository<Exhibition>
{
    public ExhibitionAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
        : base(db, operationRecorder)
    {
    }

    protected override DbSet<Exhibition> Set => Db.Exhibition;

    protected override string Noun => "展览";

    protected override void ApplyEditableFields(Exhibition target, Exhibition input)
    {
        target.ArtistId = input.ArtistId;
        target.Title = input.Title;
        target.Location = input.Location;
        target.StartDate = input.StartDate;
        target.EndDate = input.EndDate;
        target.Link = input.Link;
        if (!string.IsNullOrEmpty(input.CoverPath))
        {
            target.CoverPath = input.CoverPath;
        }
    }
}
