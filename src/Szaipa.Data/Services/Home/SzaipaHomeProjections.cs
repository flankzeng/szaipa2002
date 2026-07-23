using System.Linq.Expressions;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Models.Home;

namespace Szaipa.Data.Services.Home;

/// <summary>
/// Single source of the read-only entity-to-read-model projections for the Szaipa public link. Keeping
/// every projection here prevents the contract drift the pre-EF-Core audit caught (e.g. a publication
/// summary that forgot Status/FolderName). All projections are read-only and are translated by EF Core.
/// </summary>
internal static class SzaipaHomeProjections
{
    public static readonly Expression<Func<News, NewsSummaryModel>> NewsSummary =
        news => new NewsSummaryModel
        {
            Id = news.Id,
            Title = news.Title ?? string.Empty,
            Subtitle = news.Subtitle ?? string.Empty,
            Date = news.Date,
            CoverPath = news.CoverPath ?? string.Empty,
            // Preserve the raw nullable flag; each surface applies its own null rule (see NewsSummaryModel).
            Important = news.Important
        };

    public static readonly Expression<Func<News, NewsDetailModel>> NewsDetail =
        news => new NewsDetailModel
        {
            Id = news.Id,
            Title = news.Title ?? string.Empty,
            Subtitle = news.Subtitle ?? string.Empty,
            Author = news.Autor ?? string.Empty,
            Date = news.Date,
            Content = news.Content ?? string.Empty,
            CoverPath = news.CoverPath ?? string.Empty
        };

    public static readonly Expression<Func<Publication, PublicationSummaryModel>> PublicationSummary =
        publication => new PublicationSummaryModel
        {
            Id = publication.Id,
            TitleCn = publication.TitleCN ?? string.Empty,
            TitleEn = publication.TitleEN ?? string.Empty,
            StartDate = publication.StartDate,
            EndDate = publication.EndDate,
            CoverPath = publication.CoverPath ?? string.Empty,
            LogoPath = publication.LogoPath ?? string.Empty,
            Location = publication.Location ?? string.Empty,
            FolderName = publication.FolderName ?? string.Empty,
            Organizer = publication.zhuban ?? string.Empty,
            Host = publication.chengban ?? string.Empty,
            CoHost = publication.xieban ?? string.Empty,
            Status = publication.Status
        };

    /// <summary>
    /// The fields in this projection are present in the legacy publication schema. Additive template
    /// fields (Type/Preface/Signature) are loaded separately so an un-migrated read-only database can
    /// continue serving the original gallery.
    /// </summary>
    public static readonly Expression<Func<Publication, PublicationDetailModel>> PublicationDetailLegacy =
        publication => new PublicationDetailModel
        {
            Id = publication.Id,
            TitleCn = publication.TitleCN ?? string.Empty,
            TitleEn = publication.TitleEN ?? string.Empty,
            StartDate = publication.StartDate,
            EndDate = publication.EndDate,
            FolderName = publication.FolderName ?? string.Empty,
            MaxImg = publication.MaxImg,
            CoverPath = publication.CoverPath ?? string.Empty,
            LogoPath = publication.LogoPath ?? string.Empty,
            // MaxImagePath is derived from an asset naming convention; deferred until asset verification.
            MaxImagePath = string.Empty,
            Location = publication.Location ?? string.Empty,
            Organizer = publication.zhuban ?? string.Empty,
            Host = publication.chengban ?? string.Empty,
            CoHost = publication.xieban ?? string.Empty,
            EditRecord = publication.EditRecord ?? string.Empty,
            Type = 0,
            Preface = string.Empty,
            Signature = string.Empty
        };

    public static readonly Expression<Func<ExhibitionWork, ExhibitionWorkModel>> ExhibitionWorkSummary =
        work => new ExhibitionWorkModel
        {
            Id = work.Id,
            Category = work.Category ?? string.Empty,
            Title = work.Title ?? string.Empty,
            Artist = work.Artist ?? string.Empty,
            Size = work.Size ?? string.Empty,
            Medium = work.Medium ?? string.Empty,
            ImagePath = work.ImagePath ?? string.Empty
        };

    public static readonly Expression<Func<Artist, ArtistSummaryModel>> ArtistSummary =
        artist => new ArtistSummaryModel
        {
            Id = artist.Id,
            ArtistNameCn = artist.ArtistNameCN ?? string.Empty,
            ArtistNameEn = artist.ArtistNameEN ?? string.Empty,
            Title = artist.Title ?? string.Empty,
            Path = artist.Path ?? string.Empty,
            Introduction = artist.Introduction ?? string.Empty
        };

    public static readonly Expression<Func<Artist, ArtistDetailModel>> ArtistDetail =
        artist => new ArtistDetailModel
        {
            Id = artist.Id,
            ArtistNameCn = artist.ArtistNameCN ?? string.Empty,
            ArtistNameEn = artist.ArtistNameEN ?? string.Empty,
            Title = artist.Title ?? string.Empty,
            Nation = artist.Nation ?? string.Empty,
            City = artist.City ?? string.Empty,
            Honor = artist.Honor ?? string.Empty,
            Introduction = artist.Introduction ?? string.Empty,
            Path = artist.Path ?? string.Empty,
            Path1 = artist.Path1 ?? string.Empty,
            Path2 = artist.Path2 ?? string.Empty,
            Position = artist.Position ?? string.Empty,
            DeedsThings = artist.DeedsThings ?? string.Empty,
            Color1 = artist.Color1 ?? string.Empty,
            Color2 = artist.Color2 ?? string.Empty
        };

    public static readonly Expression<Func<Works, WorkSummaryModel>> WorkSummary =
        work => new WorkSummaryModel
        {
            Id = work.Id,
            ArtistId = work.ArtistId,
            Title = work.Title ?? string.Empty,
            Path = work.Path ?? string.Empty,
            Width = work.Width,
            Height = work.Height,
            Tags = work.Tags ?? string.Empty,
            Content = work.Content ?? string.Empty
        };

    public static readonly Expression<Func<ArtNews, ArtistSidePanelItemModel>> ArtNewsSidePanel =
        artNews => new ArtistSidePanelItemModel
        {
            Id = artNews.Id,
            ArtistId = artNews.ArtistId,
            Title = artNews.Title ?? string.Empty,
            Subtitle = artNews.SubTitle ?? string.Empty,
            Date = artNews.Date,
            CoverPath = artNews.CoverPath ?? string.Empty
        };

    public static readonly Expression<Func<Fav, ArtistSidePanelItemModel>> FavSidePanel =
        fav => new ArtistSidePanelItemModel
        {
            Id = fav.Id,
            ArtistId = fav.ArtistId,
            Title = fav.Title ?? string.Empty,
            Subtitle = fav.Location ?? string.Empty,
            Date = null,
            CoverPath = fav.CoverPath ?? string.Empty
        };

    public static readonly Expression<Func<Auction, ArtistSidePanelItemModel>> AuctionSidePanel =
        auction => new ArtistSidePanelItemModel
        {
            Id = auction.Id,
            ArtistId = auction.ArtistId,
            Title = auction.Title ?? string.Empty,
            Subtitle = auction.Price ?? string.Empty,
            Date = null,
            CoverPath = auction.CoverPath ?? string.Empty
        };

    public static readonly Expression<Func<Exhibition, ArtistExhibitionItemModel>> ArtistExhibition =
        exhibition => new ArtistExhibitionItemModel
        {
            Id = exhibition.Id,
            ArtistId = exhibition.ArtistId,
            Title = exhibition.Title ?? string.Empty,
            CoverPath = exhibition.CoverPath ?? string.Empty,
            Location = exhibition.Location ?? string.Empty,
            Link = exhibition.Link ?? string.Empty,
            StartDate = exhibition.StartDate ?? string.Empty,
            EndDate = exhibition.EndDate ?? string.Empty
        };
}
