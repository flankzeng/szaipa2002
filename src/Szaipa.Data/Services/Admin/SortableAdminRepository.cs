using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <summary>
/// Generic write-side repository for the hand-ordered homepage/About blocks (<see cref="OrgMember"/>,
/// <see cref="SaleWork"/>, <see cref="Partner"/>). Provides list, lookup, create (appended to the end of its
/// scope), update, delete, move up/down and show/hide, each logged through <see cref="IOperationRecorder"/>
/// in the same transaction. Derived classes supply the DbSet, a display noun, the display name used in log
/// messages, and how to copy the editable fields.
/// </summary>
/// <remarks>
/// A "scope" is the set of rows that are ordered against each other: one <c>OrgMember.GroupName</c> for the org
/// chart, the whole table for sale works and partners. Moving never crosses scopes.
/// </remarks>
public abstract class SortableAdminRepository<T>
    where T : class, ISortableRecord, new()
{
    protected SortableAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
    {
        Db = db;
        OperationRecorder = operationRecorder;
    }

    protected SzaipaAdminContext Db { get; }

    protected IOperationRecorder OperationRecorder { get; }

    protected abstract DbSet<T> Set { get; }

    /// <summary>Display noun for audit/log messages, e.g. "组织架构成员".</summary>
    protected abstract string Noun { get; }

    protected abstract string DisplayName(T entity);

    /// <summary>Copies the editable fields (everything except Id / Sort / IsVisible) from <paramref name="input"/>.</summary>
    protected abstract void ApplyEditableFields(T target, T input);

    /// <summary>Key identifying the ordering scope of a row (default: a single scope for the whole table).</summary>
    protected virtual string ScopeKey(T entity) => string.Empty;

    /// <summary>Predicate selecting all rows of the scope identified by <paramref name="scopeKey"/>.</summary>
    protected virtual Expression<Func<T, bool>> InScope(string scopeKey) => _ => true;

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken) =>
        await Set.AsNoTracking().OrderBy(e => e.Sort).ThenBy(e => e.Id).ToListAsync(cancellationToken);

    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        Set.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<int> CreateAsync(T input, AdminActor actor, CancellationToken cancellationToken)
    {
        var entity = new T();
        ApplyEditableFields(entity, input);
        entity.IsVisible = input.IsVisible;
        entity.Sort = await NextSortAsync(ScopeKey(entity), cancellationToken);

        Set.Add(entity);
        await OperationRecorder.RecordAsync(Db, actor, $"新增了 {DisplayName(entity)}的{Noun}。", cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(T input, AdminActor actor, CancellationToken cancellationToken)
    {
        var entity = await Set.FirstOrDefaultAsync(e => e.Id == input.Id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        var scopeBefore = ScopeKey(entity);
        ApplyEditableFields(entity, input);
        entity.IsVisible = input.IsVisible;

        // Moved to another scope (e.g. 核心成员 -> 顾问团队): go to the end of the new scope.
        var scopeAfter = ScopeKey(entity);
        if (!string.Equals(scopeBefore, scopeAfter, StringComparison.Ordinal))
        {
            entity.Sort = await NextSortAsync(scopeAfter, cancellationToken);
        }

        await OperationRecorder.RecordAsync(Db, actor, $"修改了 {DisplayName(entity)}的{Noun}。", cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken)
    {
        var entity = await Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        Set.Remove(entity);
        await OperationRecorder.RecordAsync(Db, actor, $"删除了 {DisplayName(entity)}的{Noun}。", cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Moves a row one place up (<paramref name="direction"/> &lt; 0) or down (&gt; 0) inside its scope and
    /// renumbers the scope contiguously (1..n), which also repairs ties/gaps left by earlier edits.
    /// False when the row does not exist; moving past either end is a no-op that still returns true.
    /// </summary>
    public async Task<bool> MoveAsync(int id, int direction, AdminActor actor, CancellationToken cancellationToken)
    {
        var entity = await Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        var scope = await Set
            .Where(InScope(ScopeKey(entity)))
            .OrderBy(e => e.Sort)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        var index = scope.FindIndex(e => e.Id == id);
        var target = index + Math.Sign(direction);
        if (direction != 0 && target >= 0 && target < scope.Count)
        {
            (scope[index], scope[target]) = (scope[target], scope[index]);
        }

        for (var i = 0; i < scope.Count; i++)
        {
            scope[i].Sort = i + 1;
        }

        if (direction != 0)
        {
            await OperationRecorder.RecordAsync(
                Db, actor, $"调整了 {DisplayName(entity)}的{Noun}排序。", cancellationToken);
        }

        await Db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReorderAsync(IReadOnlyList<int> orderedIds, AdminActor actor, CancellationToken cancellationToken)
    {
        if (orderedIds.Count == 0 || orderedIds.Distinct().Count() != orderedIds.Count)
        {
            return false;
        }

        var first = await Set.FirstOrDefaultAsync(e => e.Id == orderedIds[0], cancellationToken);
        if (first is null)
        {
            return false;
        }

        var scope = await Set
            .Where(InScope(ScopeKey(first)))
            .OrderBy(e => e.Sort)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);
        var byId = scope.ToDictionary(e => e.Id);
        if (orderedIds.Any(id => !byId.ContainsKey(id)))
        {
            return false;
        }

        var listed = orderedIds.ToHashSet();
        var reordered = orderedIds.Select(id => byId[id]).Concat(scope.Where(e => !listed.Contains(e.Id))).ToList();
        for (var i = 0; i < reordered.Count; i++)
        {
            reordered[i].Sort = i + 1;
        }

        await OperationRecorder.RecordAsync(Db, actor, $"拖拽调整了{Noun}排序（{string.Join("、", orderedIds.Select(id => DisplayName(byId[id])))}）。", cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetVisibleAsync(int id, bool visible, AdminActor actor, CancellationToken cancellationToken)
    {
        var entity = await Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.IsVisible = visible;
        await OperationRecorder.RecordAsync(
            Db, actor, $"{(visible ? "显示" : "隐藏")}了 {DisplayName(entity)}的{Noun}。", cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ArtistAvatarOption>> GetArtistOptionsAsync(CancellationToken cancellationToken) =>
        await Db.Artist.AsNoTracking()
            .OrderBy(a => a.ArtistNameCN)
            .Select(a => new ArtistAvatarOption(a.Id, a.ArtistNameCN ?? $"#{a.Id}", a.Path))
            .ToListAsync(cancellationToken);

    private async Task<int> NextSortAsync(string scopeKey, CancellationToken cancellationToken)
    {
        var max = await Set
            .Where(InScope(scopeKey))
            .MaxAsync(e => (int?)e.Sort, cancellationToken);
        return (max ?? 0) + 1;
    }
}

/// <summary>An artist choice for the "关联艺术家" dropdown; <see cref="Path"/> is the avatar file under /Content/ArtImg/Artist/.</summary>
public sealed record ArtistAvatarOption(int Id, string Name, string? Path);
