using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side repository for the ArtistBook (艺术家出版著作) module.</summary>
public sealed class ArtistBookAdminRepository : ArtistScopedAdminRepository<ArtistBook>
{
    public ArtistBookAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
        : base(db, operationRecorder)
    {
    }

    protected override DbSet<ArtistBook> Set => Db.ArtistBook;

    protected override string Noun => "出版著作";

    protected override void ApplyEditableFields(ArtistBook target, ArtistBook input)
    {
        target.ArtistId = input.ArtistId;
        target.Title = input.Title;
        target.Publisher = input.Publisher;
        target.PublishYear = input.PublishYear;
        target.Description = input.Description;
        target.Sort = input.Sort;
        if (!string.IsNullOrEmpty(input.CoverPath))
        {
            target.CoverPath = input.CoverPath;
        }
    }
}
