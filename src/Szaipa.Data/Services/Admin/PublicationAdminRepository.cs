using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <inheritdoc />
public sealed class PublicationAdminRepository : IPublicationAdminRepository
{
    private readonly SzaipaAdminContext _db;
    private readonly IOperationRecorder _operationRecorder;

    public PublicationAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
    {
        _db = db;
        _operationRecorder = operationRecorder;
    }

    public async Task<PagedResult<Publication>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Publication.AsNoTracking().OrderByDescending(p => p.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<Publication>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<Publication?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _db.Publication.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<int> CreateAsync(Publication input, AdminActor actor, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var publication = new Publication
        {
            TitleCN = input.TitleCN,
            TitleEN = input.TitleEN,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            FolderName = input.FolderName,
            MaxImg = input.MaxImg,
            LogoPath = input.LogoPath,
            CoverPath = input.CoverPath,
            Location = input.Location,
            Status = input.Status,
            zhuban = input.zhuban,
            chengban = input.chengban,
            xieban = input.xieban,
            Type = input.Type,
            Preface = input.Preface,
            Signature = input.Signature,
            ReadCount = 0,
            EditRecord = $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 新建了此展览。/"
        };

        _db.Publication.Add(publication);
        await _operationRecorder.RecordAsync(_db, actor, $"新建了 {publication.TitleCN}的展览。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return publication.Id;
    }

    public async Task<bool> UpdateAsync(Publication input, AdminActor actor, CancellationToken cancellationToken)
    {
        var publication = await _db.Publication.FirstOrDefaultAsync(p => p.Id == input.Id, cancellationToken);
        if (publication is null)
        {
            return false;
        }

        publication.TitleCN = input.TitleCN;
        publication.TitleEN = input.TitleEN;
        publication.StartDate = input.StartDate;
        publication.EndDate = input.EndDate;
        publication.FolderName = input.FolderName;
        publication.Location = input.Location;
        publication.Status = input.Status;
        publication.zhuban = input.zhuban;
        publication.chengban = input.chengban;
        publication.xieban = input.xieban;
        publication.Type = input.Type;
        publication.Preface = input.Preface;
        publication.Signature = input.Signature;
        if (!string.IsNullOrEmpty(input.CoverPath))
        {
            publication.CoverPath = input.CoverPath;
        }

        if (!string.IsNullOrEmpty(input.LogoPath))
        {
            publication.LogoPath = input.LogoPath;
        }

        var now = DateTime.Now;
        publication.EditRecord += $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 修改了此展览。/";

        await _operationRecorder.RecordAsync(_db, actor, $"修改了 {publication.TitleCN}的展览。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetMaxImgAsync(int id, int maxImg, CancellationToken cancellationToken)
    {
        var publication = await _db.Publication.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (publication is null)
        {
            return false;
        }

        publication.MaxImg = maxImg;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken)
    {
        var publication = await _db.Publication.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (publication is null)
        {
            return false;
        }

        _db.Publication.Remove(publication);
        await _operationRecorder.RecordAsync(_db, actor, $"删除了 {publication.TitleCN}的展览。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
