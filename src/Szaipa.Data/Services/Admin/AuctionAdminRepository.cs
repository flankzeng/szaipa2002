using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side repository for the Auction (artist auction record) module.</summary>
public sealed class AuctionAdminRepository : ArtistScopedAdminRepository<Auction>
{
    public AuctionAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
        : base(db, operationRecorder)
    {
    }

    protected override DbSet<Auction> Set => Db.Auction;

    protected override string Noun => "拍卖";

    protected override void ApplyEditableFields(Auction target, Auction input)
    {
        target.ArtistId = input.ArtistId;
        target.Title = input.Title;
        target.Price = input.Price;
        target.RMB = input.RMB;
        target.HKD = input.HKD;
        target.USD = input.USD;
        target.Date = input.Date;
        if (!string.IsNullOrEmpty(input.CoverPath))
        {
            target.CoverPath = input.CoverPath;
        }
    }
}
