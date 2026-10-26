using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side repository for the home-page 推荐展览 list (<see cref="FeaturedExhibition"/>).</summary>
public interface IFeaturedExhibitionAdminRepository
{
    Task<IReadOnlyList<FeaturedExhibition>> GetAllAsync(CancellationToken cancellationToken);

    Task<FeaturedExhibition?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Creates a row; <c>SortOrder == 0</c> means "append at the end".</summary>
    Task<int> CreateAsync(FeaturedExhibition input, AdminActor actor, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(FeaturedExhibition input, AdminActor actor, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>Moves a row one step up (<c>-1</c>) or down (<c>+1</c>) in the display order.</summary>
    Task<bool> MoveAsync(int id, int direction, AdminActor actor, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IFeaturedExhibitionAdminRepository" />
public sealed class FeaturedExhibitionAdminRepository : IFeaturedExhibitionAdminRepository
{
    private const int SortStep = 10;

    private readonly SzaipaAdminContext _db;
    private readonly IOperationRecorder _operationRecorder;

    public FeaturedExhibitionAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
    {
        _db = db;
        _operationRecorder = operationRecorder;
    }

    public async Task<IReadOnlyList<FeaturedExhibition>> GetAllAsync(CancellationToken cancellationToken) =>
        await _db.FeaturedExhibition
            .AsNoTracking()
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);

    public Task<FeaturedExhibition?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _db.FeaturedExhibition.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

    public async Task<int> CreateAsync(FeaturedExhibition input, AdminActor actor, CancellationToken cancellationToken)
    {
        var row = new FeaturedExhibition();
        Apply(row, input);

        if (row.SortOrder == 0)
        {
            var max = await _db.FeaturedExhibition.Select(x => (int?)x.SortOrder).MaxAsync(cancellationToken) ?? 0;
            row.SortOrder = max + SortStep;
        }

        _db.FeaturedExhibition.Add(row);
        await _operationRecorder.RecordAsync(_db, actor, $"新增了首页推荐展览「{row.Title}」。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    public async Task<bool> UpdateAsync(FeaturedExhibition input, AdminActor actor, CancellationToken cancellationToken)
    {
        var row = await _db.FeaturedExhibition.FirstOrDefaultAsync(x => x.Id == input.Id, cancellationToken);
        if (row is null)
        {
            return false;
        }

        Apply(row, input);
        await _operationRecorder.RecordAsync(_db, actor, $"修改了首页推荐展览「{row.Title}」。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken)
    {
        var row = await _db.FeaturedExhibition.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
        {
            return false;
        }

        _db.FeaturedExhibition.Remove(row);
        await _operationRecorder.RecordAsync(_db, actor, $"删除了首页推荐展览「{row.Title}」。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MoveAsync(int id, int direction, AdminActor actor, CancellationToken cancellationToken)
    {
        var rows = await _db.FeaturedExhibition
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var index = rows.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            return false;
        }

        var target = index + (direction < 0 ? -1 : 1);
        if (target >= 0 && target < rows.Count)
        {
            (rows[index], rows[target]) = (rows[target], rows[index]);
            await _operationRecorder.RecordAsync(
                _db,
                actor,
                $"调整了首页推荐展览「{rows[target].Title}」的显示顺序。",
                cancellationToken);
        }

        // Renumber the whole list so ties / gaps in hand-edited values can never make a move a no-op.
        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].SortOrder = (i + 1) * SortStep;
        }

        if (target < 0 || target >= rows.Count)
        {
            return true;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Copies editable fields, normalising text and refusing non site-local links / addresses.</summary>
    private static void Apply(FeaturedExhibition target, FeaturedExhibition input)
    {
        target.Title = (input.Title ?? string.Empty).Trim();
        target.YearText = string.IsNullOrWhiteSpace(input.YearText) ? null : input.YearText.Trim();
        target.LinkUrl = FeaturedExhibitionContent.SafePathOrEmpty(input.LinkUrl);
        target.InfoLines = FeaturedExhibitionContent.JoinLines(input.InfoLines);
        target.ImageUrls = FeaturedExhibitionContent.JoinPaths(input.ImageUrls);
        target.LogoUrl = NullIfEmpty(FeaturedExhibitionContent.SafePathOrEmpty(input.LogoUrl));
        target.CoverUrl = NullIfEmpty(FeaturedExhibitionContent.SafePathOrEmpty(input.CoverUrl));
        target.StartDate = input.StartDate?.Date;
        target.EndDate = input.EndDate?.Date;
        target.Status = input.Status;
        target.SortOrder = input.SortOrder;
        target.IsVisible = input.IsVisible;
        target.ShowInCarousel = input.ShowInCarousel;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
