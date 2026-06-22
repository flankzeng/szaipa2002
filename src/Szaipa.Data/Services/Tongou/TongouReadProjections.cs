using System.Linq.Expressions;
using Szaipa.Data.Contexts.Tongou;
using Szaipa.Data.Models.Tongou;

namespace Szaipa.Data.Services.Tongou;

/// <summary>Single source of the read-only Tongou entity-to-read-model projections.</summary>
internal static class TongouReadProjections
{
    public static readonly Expression<Func<TongouAtrist, TongouArtistModel>> Artist =
        artist => new TongouArtistModel
        {
            Id = artist.id,
            Name = artist.Name ?? string.Empty,
            Title = artist.Title ?? string.Empty,
            HeardPath = artist.HeardPath ?? string.Empty,
            AboutText = artist.AboutText ?? string.Empty,
            WorksCount = artist.WorksCount
        };

    public static readonly Expression<Func<TongouWorks, TongouWorkModel>> Work =
        work => new TongouWorkModel
        {
            Id = work.id,
            ArtistId = work.Atristid,
            ArtistName = work.AtristidName ?? string.Empty,
            Title = work.Title ?? string.Empty,
            ImgPath = work.ImgPath ?? string.Empty,
            // Raw nullable passthrough so the Work view's `!= null` branches match legacy exactly.
            Size = work.Size,
            Type = work.Type,
            CreationDate = work.CreationDate
        };
}
