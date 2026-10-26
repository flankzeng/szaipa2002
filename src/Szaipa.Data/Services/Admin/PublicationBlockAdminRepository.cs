using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <inheritdoc cref="IPublicationBlockAdminRepository" />
public sealed class PublicationBlockAdminRepository : IPublicationBlockAdminRepository
{
    /// <summary>Upper bound on blocks per page; guards against a runaway/malicious payload.</summary>
    public const int MaxBlocksPerPublication = 60;

    private readonly SzaipaAdminContext _db;
    private readonly IOperationRecorder _operationRecorder;

    public PublicationBlockAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
    {
        _db = db;
        _operationRecorder = operationRecorder;
    }

    public async Task<IReadOnlyList<PublicationBlock>> GetByPublicationAsync(int publicationId, CancellationToken cancellationToken) =>
        await _db.PublicationBlock
            .AsNoTracking()
            .Where(block => block.PublicationId == publicationId)
            .OrderBy(block => block.SortOrder)
            .ThenBy(block => block.Id)
            .ToListAsync(cancellationToken);

    public async Task<int> ReplaceAsync(
        int publicationId,
        IReadOnlyList<PublicationBlockInput> items,
        AdminActor actor,
        CancellationToken cancellationToken)
    {
        var existing = await _db.PublicationBlock
            .Where(block => block.PublicationId == publicationId)
            .ToListAsync(cancellationToken);
        _db.PublicationBlock.RemoveRange(existing);

        var sortOrder = 0;
        foreach (var item in items)
        {
            if (sortOrder >= MaxBlocksPerPublication)
            {
                break;
            }

            var normalized = PublicationBlockContent.Normalize(item.BlockType, item.Content);
            if (normalized is null)
            {
                continue; // unknown block type: ignore instead of storing something the renderer can't show
            }

            _db.PublicationBlock.Add(new PublicationBlock
            {
                PublicationId = publicationId,
                SortOrder = sortOrder++,
                BlockType = item.BlockType!,
                Content = normalized
            });
        }

        await _operationRecorder.RecordAsync(
            _db,
            actor,
            $"更新了专题（Id={publicationId}）的页面积木（{sortOrder} 块）。",
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return sortOrder;
    }
}
