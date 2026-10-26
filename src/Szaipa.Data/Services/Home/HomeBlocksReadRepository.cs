using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Composition;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contracts.Home;
using Szaipa.Data.Models.Home;

namespace Szaipa.Data.Services.Home;

/// <summary>
/// Read-only implementation of <see cref="IHomeBlocksReadRepository"/>. Rows are projected with
/// <c>AsNoTracking().Select(...)</c>, hidden rows are dropped, and order is <c>Sort, Id</c>. The tables are
/// additive (docs/sql/2026-10-26-org-and-home-blocks.sql): a missing table/column — or a table with no rows at
/// all — yields <see cref="HomeBlocksDefaults"/>. A table that has rows but none visible deliberately yields an
/// EMPTY block (the editor hid everything), not the defaults.
/// </summary>
public sealed class HomeBlocksReadRepository : IHomeBlocksReadRepository
{
    private readonly SzaipaLegacyReadContext _context;
    private readonly HomeBlocksSchemaState _schema;

    public HomeBlocksReadRepository(SzaipaLegacyReadContext context, HomeBlocksSchemaState schema)
    {
        _context = context;
        _schema = schema;
    }

    public async Task<HomeStaticBlocksModel> GetHomeBlocksAsync(CancellationToken cancellationToken = default)
    {
        var members = await TryReadAsync(nameof(OrgMember), ReadMembersAsync, cancellationToken);
        var saleWorks = await TryReadAsync(nameof(SaleWork), ReadSaleWorksAsync, cancellationToken);

        var artists = await ReadArtistLookupAsync(
            (members ?? []).Where(m => m.IsVisible && string.IsNullOrWhiteSpace(m.AvatarPath)).Select(m => m.ArtistId)
                .Concat((saleWorks ?? []).Where(w => w.IsVisible && string.IsNullOrWhiteSpace(w.ArtistName)).Select(w => w.ArtistId)),
            cancellationToken);

        IReadOnlyList<OrgMemberModel> Members(string group, IReadOnlyList<OrgMemberModel> fallback) =>
            members is null or { Count: 0 }
                ? fallback
                : members
                    .Where(m => m.IsVisible && string.Equals(m.GroupName, group, StringComparison.Ordinal))
                    .Select(m => ToModel(m, artists))
                    .ToArray();

        return new HomeStaticBlocksModel
        {
            CoreMembers = Members(OrgGroups.Core, HomeBlocksDefaults.CoreMembers),
            AdvisoryMembers = Members(OrgGroups.Advisory, HomeBlocksDefaults.AdvisoryMembers),
            SaleWorks = saleWorks is null or { Count: 0 }
                ? HomeBlocksDefaults.SaleWorks
                : saleWorks.Where(w => w.IsVisible).Select(w => ToModel(w, artists)).ToArray()
        };
    }

    public async Task<IReadOnlyList<PartnerModel>> GetPartnersAsync(CancellationToken cancellationToken = default)
    {
        var partners = await TryReadAsync(
            nameof(Partner),
            async token => (IReadOnlyList<Partner>)await _context.Partner
                .AsNoTracking()
                .OrderBy(p => p.Sort)
                .ThenBy(p => p.Id)
                .ToListAsync(token),
            cancellationToken);

        return partners is null or { Count: 0 }
            ? HomeBlocksDefaults.Partners
            : partners
                .Where(p => p.IsVisible)
                .Select(p => new PartnerModel(p.Name, p.LogoPath, p.ImageWidth, p.ImageHeight, p.Link, p.Description))
                .ToArray();
    }

    private async Task<IReadOnlyList<OrgMember>> ReadMembersAsync(CancellationToken cancellationToken) =>
        await _context.OrgMember.AsNoTracking().OrderBy(m => m.Sort).ThenBy(m => m.Id).ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<SaleWork>> ReadSaleWorksAsync(CancellationToken cancellationToken) =>
        await _context.SaleWork.AsNoTracking().OrderBy(w => w.Sort).ThenBy(w => w.Id).ToListAsync(cancellationToken);

    /// <summary>Returns null when the table (or one of its columns) does not exist yet.</summary>
    private async Task<IReadOnlyList<T>?> TryReadAsync<T>(
        string table,
        Func<CancellationToken, Task<IReadOnlyList<T>>> read,
        CancellationToken cancellationToken)
    {
        if (_schema.ShouldSkip(table))
        {
            return null;
        }

        try
        {
            return await read(cancellationToken);
        }
        catch (DbException exception) when (IsMissingSchemaObject(exception))
        {
            // The additive tables are created by the operator-run SQL script; until then use the defaults,
            // and stop re-querying (and re-logging the EF failure) on every request for a short while.
            _schema.MarkMissing(table);
            return null;
        }
    }

    private async Task<Dictionary<int, (string? Path, string? Name)>> ReadArtistLookupAsync(
        IEnumerable<int?> artistIds,
        CancellationToken cancellationToken)
    {
        var ids = artistIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, (string?, string?)>();
        }

        var rows = await _context.Artist
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.Path, a.ArtistNameCN })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(a => a.Id, a => (a.Path, a.ArtistNameCN));
    }

    private static OrgMemberModel ToModel(OrgMember member, Dictionary<int, (string? Path, string? Name)> artists)
    {
        var avatar = member.AvatarPath;
        var width = member.ImageWidth;
        var height = member.ImageHeight;
        if (string.IsNullOrWhiteSpace(avatar)
            && member.ArtistId is { } artistId
            && artists.TryGetValue(artistId, out var artist)
            && !string.IsNullOrWhiteSpace(artist.Path))
        {
            // No own picture: borrow the linked artist's avatar (size unknown, so no width/height hint).
            avatar = $"/Content/ArtImg/Artist/{artist.Path}";
            width = null;
            height = null;
        }

        return new OrgMemberModel(member.Name, member.Role, member.Title, member.ArtistId, avatar, width, height);
    }

    private static SaleWorkModel ToModel(SaleWork work, Dictionary<int, (string? Path, string? Name)> artists)
    {
        var artistName = work.ArtistName;
        if (string.IsNullOrWhiteSpace(artistName) && work.ArtistId is { } artistId
            && artists.TryGetValue(artistId, out var artist))
        {
            artistName = artist.Name;
        }

        return new SaleWorkModel(
            work.ArtistId, artistName, work.Title, work.Size, work.Medium, work.ImagePath, work.ImageWidth, work.ImageHeight);
    }

    /// <summary>
    /// SQL Server error 208 (invalid object name) / 207 (invalid column name), or the SQLite/English-message
    /// equivalents. The error NUMBER is checked first because production SQL Server messages may be localized.
    /// </summary>
    public static bool IsMissingSchemaObject(DbException exception)
    {
        if (exception is SqlException { Number: 208 or 207 })
        {
            return true;
        }

        var message = exception.Message;
        return message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase)
            || message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
            || message.Contains("no such column", StringComparison.OrdinalIgnoreCase);
    }
}
