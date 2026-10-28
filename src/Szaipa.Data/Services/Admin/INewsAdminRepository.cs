using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side repository for the staff backend's News module (over <c>SzaipaAdminContext</c>).</summary>
public interface INewsAdminRepository
{
    Task<PagedResult<News>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<News?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Creates a news row from the editable fields of <paramref name="input"/>; returns the new Id.</summary>
    Task<int> CreateAsync(News input, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>Applies the editable fields of <paramref name="input"/> to the stored row. False if not found.</summary>
    Task<bool> UpdateAsync(News input, AdminActor actor, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>
    /// 只改"重要"（Important）：true = 前台新闻列表与首页显示封面大卡片，false = 日期+标题普通行。False if not found.
    /// </summary>
    Task<bool> SetImportantAsync(int id, bool important, AdminActor actor, CancellationToken cancellationToken);
}
