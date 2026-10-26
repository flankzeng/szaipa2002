using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <inheritdoc cref="IOrgMemberAdminRepository" />
public sealed class OrgMemberAdminRepository : SortableAdminRepository<OrgMember>, IOrgMemberAdminRepository
{
    public OrgMemberAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
        : base(db, operationRecorder)
    {
    }

    protected override DbSet<OrgMember> Set => Db.OrgMember;

    protected override string Noun => "组织架构成员";

    protected override string DisplayName(OrgMember entity) => entity.Name;

    protected override string ScopeKey(OrgMember entity) => entity.GroupName;

    protected override Expression<Func<OrgMember, bool>> InScope(string scopeKey) => m => m.GroupName == scopeKey;

    protected override void ApplyEditableFields(OrgMember target, OrgMember input)
    {
        target.Name = input.Name.Trim();
        target.Role = Clean(input.Role);
        target.Title = Clean(input.Title);
        target.GroupName = string.IsNullOrWhiteSpace(input.GroupName) ? OrgGroups.Core : input.GroupName.Trim();
        target.ArtistId = input.ArtistId;
        target.AvatarPath = Clean(input.AvatarPath);
        target.ImageWidth = input.ImageWidth;
        target.ImageHeight = input.ImageHeight;
    }

    /// <summary>List order: known groups first (核心成员, 顾问团队), then any custom group; within a group by Sort.</summary>
    public override async Task<IReadOnlyList<OrgMember>> GetAllAsync(CancellationToken cancellationToken)
    {
        var all = await base.GetAllAsync(cancellationToken);
        return all
            .OrderBy(m => GroupRank(m.GroupName))
            .ThenBy(m => m.GroupName, StringComparer.Ordinal)
            .ThenBy(m => m.Sort)
            .ThenBy(m => m.Id)
            .ToList();
    }

    private static int GroupRank(string group)
    {
        for (var i = 0; i < OrgGroups.All.Count; i++)
        {
            if (string.Equals(OrgGroups.All[i], group, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return OrgGroups.All.Count;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <inheritdoc cref="ISaleWorkAdminRepository" />
public sealed class SaleWorkAdminRepository : SortableAdminRepository<SaleWork>, ISaleWorkAdminRepository
{
    public SaleWorkAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
        : base(db, operationRecorder)
    {
    }

    protected override DbSet<SaleWork> Set => Db.SaleWork;

    protected override string Noun => "在售作品";

    protected override string DisplayName(SaleWork entity) => entity.Title;

    protected override void ApplyEditableFields(SaleWork target, SaleWork input)
    {
        target.ArtistId = input.ArtistId;
        target.ArtistName = Clean(input.ArtistName);
        target.Title = input.Title.Trim();
        target.Size = Clean(input.Size);
        target.Medium = Clean(input.Medium);
        target.ImagePath = Clean(input.ImagePath);
        target.ImageWidth = input.ImageWidth;
        target.ImageHeight = input.ImageHeight;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <inheritdoc cref="IPartnerAdminRepository" />
public sealed class PartnerAdminRepository : SortableAdminRepository<Partner>, IPartnerAdminRepository
{
    public PartnerAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
        : base(db, operationRecorder)
    {
    }

    protected override DbSet<Partner> Set => Db.Partner;

    protected override string Noun => "合作伙伴";

    protected override string DisplayName(Partner entity) => entity.Name.Replace("\n", string.Empty);

    protected override void ApplyEditableFields(Partner target, Partner input)
    {
        // Browsers submit textarea line breaks as CRLF; store plain \n.
        target.Name = input.Name.Replace("\r\n", "\n").Trim();
        target.LogoPath = Clean(input.LogoPath);
        target.ImageWidth = input.ImageWidth;
        target.ImageHeight = input.ImageHeight;
        target.Link = Clean(input.Link);
        target.Description = Clean(input.Description?.Replace("\r\n", "\n"));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
