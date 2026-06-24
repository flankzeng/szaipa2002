using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side repository for the Fav (artist collection) module.</summary>
public sealed class FavAdminRepository : ArtistScopedAdminRepository<Fav>
{
    public FavAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
        : base(db, operationRecorder)
    {
    }

    protected override DbSet<Fav> Set => Db.Fav;

    protected override string Noun => "收藏";

    protected override void ApplyEditableFields(Fav target, Fav input)
    {
        target.ArtistId = input.ArtistId;
        target.Title = input.Title;
        target.Creator = input.Creator;
        target.Year = input.Year;
        target.Location = input.Location;
        target.Size = input.Size;
        target.Material = input.Material;
        target.Type = input.Type;
        target.Province = input.Province;
        target.CollectNumber = input.CollectNumber;
        if (!string.IsNullOrEmpty(input.CoverPath))
        {
            target.CoverPath = input.CoverPath;
        }
    }
}
