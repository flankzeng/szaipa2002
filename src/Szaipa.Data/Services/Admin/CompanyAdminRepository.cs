using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <inheritdoc />
public sealed class CompanyAdminRepository : ICompanyAdminRepository
{
    private readonly SzaipaAdminContext _db;
    private readonly IOperationRecorder _operationRecorder;

    public CompanyAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
    {
        _db = db;
        _operationRecorder = operationRecorder;
    }

    public async Task<PagedResult<Company>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Company.AsNoTracking().OrderByDescending(c => c.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Company>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<Company?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _db.Company.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<int> CreateAsync(Company input, AdminActor actor, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var company = new Company
        {
            NameCN = input.NameCN,
            NameEN = input.NameEN,
            CEO = input.CEO,
            Address = input.Address,
            Business = input.Business,
            ImgPath = input.ImgPath,
            VisitCount = 0,
            FirstDate = now,
            LastDate = now,
            EditRecord = $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 编写了此会员企业条目。/"
        };

        _db.Company.Add(company);
        await _operationRecorder.RecordAsync(_db, actor, $"编写了 {company.NameCN}会员企业条目。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return company.Id;
    }

    public async Task<bool> UpdateAsync(Company input, AdminActor actor, CancellationToken cancellationToken)
    {
        var company = await _db.Company.FirstOrDefaultAsync(c => c.Id == input.Id, cancellationToken);
        if (company is null)
        {
            return false;
        }

        // Only the editable fields are copied; VisitCount/FirstDate/LastDate stay as originally stored
        // (matches legacy CompanyEdit, which never touches them).
        company.NameCN = input.NameCN;
        company.NameEN = input.NameEN;
        company.CEO = input.CEO;
        company.Address = input.Address;
        company.Business = input.Business;
        if (!string.IsNullOrEmpty(input.ImgPath))
        {
            company.ImgPath = input.ImgPath;
        }

        var now = DateTime.Now;
        company.EditRecord += $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 修改了此会员企业条目。/";

        await _operationRecorder.RecordAsync(_db, actor, $"修改了 {company.NameCN}会员企业条目。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken)
    {
        var company = await _db.Company.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (company is null)
        {
            return false;
        }

        _db.Company.Remove(company);
        await _operationRecorder.RecordAsync(_db, actor, $"删除了 {company.NameCN}会员企业条目。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
