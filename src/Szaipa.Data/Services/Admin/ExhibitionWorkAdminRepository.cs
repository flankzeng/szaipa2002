using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <inheritdoc cref="IExhibitionWorkAdminRepository" />
public sealed class ExhibitionWorkAdminRepository : IExhibitionWorkAdminRepository
{
    private readonly SzaipaAdminContext _db;
    private readonly IOperationRecorder _operationRecorder;

    public ExhibitionWorkAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
    {
        _db = db;
        _operationRecorder = operationRecorder;
    }

    public async Task<IReadOnlyList<ExhibitionWork>> GetByPublicationAsync(int publicationId, CancellationToken cancellationToken) =>
        await _db.ExhibitionWork
            .AsNoTracking()
            .Where(work => work.PublicationId == publicationId)
            .OrderBy(work => work.SortOrder)
            .ThenBy(work => work.Id)
            .ToListAsync(cancellationToken);

    public async Task ReplaceAsync(
        int publicationId,
        IReadOnlyList<ExhibitionWorkInput> items,
        AdminActor actor,
        CancellationToken cancellationToken)
    {
        var existing = await _db.ExhibitionWork
            .Where(work => work.PublicationId == publicationId)
            .ToListAsync(cancellationToken);
        _db.ExhibitionWork.RemoveRange(existing);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            _db.ExhibitionWork.Add(new ExhibitionWork
            {
                PublicationId = publicationId,
                Category = item.Category,
                Title = item.Title,
                Artist = item.Artist,
                Size = item.Size,
                Medium = item.Medium,
                ImagePath = item.ImagePath,
                SortOrder = i
            });
        }

        await _operationRecorder.RecordAsync(
            _db,
            actor,
            $"更新了展览（Id={publicationId}）的参展作品目录（{items.Count} 件）。",
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
