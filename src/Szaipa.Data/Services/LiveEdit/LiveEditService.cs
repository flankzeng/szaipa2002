using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;

namespace Szaipa.Data.Services.LiveEdit;

/// <summary>一次现场编辑保存的结果；<see cref="Value"/> 为清洗/规范化后实际入库并应显示的值。</summary>
public sealed record LiveEditResult(bool Success, string? Value, string? Error)
{
    public static LiveEditResult Ok(string value) => new(true, value, null);

    public static LiveEditResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// 站点文案读取（单例缓存）。模板通过它取「可改的写死文字」；没有记录或表未建时返回 null，由模板显示原文。
/// 缓存 5 分钟，现场编辑保存后立即失效，所以编辑者刷新即见。
/// </summary>
public interface ISiteTextProvider
{
    string? Get(string key);

    void Invalidate();
}

public sealed class SiteTextProvider : ISiteTextProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SiteTextProvider> _logger;
    private readonly object _gate = new();
    private IReadOnlyDictionary<string, string>? _cache;
    private DateTime _loadedAt;

    public SiteTextProvider(IServiceScopeFactory scopeFactory, ILogger<SiteTextProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string? Get(string key) => Load().TryGetValue(key, out var value) ? value : null;

    public void Invalidate()
    {
        lock (_gate)
        {
            _cache = null;
        }
    }

    private IReadOnlyDictionary<string, string> Load()
    {
        lock (_gate)
        {
            if (_cache is not null && DateTime.UtcNow - _loadedAt < CacheDuration)
            {
                return _cache;
            }

            _cache = Fetch();
            _loadedAt = DateTime.UtcNow;
            return _cache;
        }
    }

    private IReadOnlyDictionary<string, string> Fetch()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetService<SzaipaLegacyReadContext>();
            if (context is null)
            {
                return new Dictionary<string, string>();
            }

            return context.SiteText.AsNoTracking().ToDictionary(t => t.Key, t => t.Value, StringComparer.Ordinal);
        }
        catch (DbException exception) when (SchemaCompatibility.IsMissingTable(exception, "SiteText"))
        {
            return new Dictionary<string, string>();
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException)
        {
            // 读侧未启用/库不可达：页面照常显示模板原文，绝不因文案表 500。
            _logger.LogWarning("站点文案读取失败，使用模板原文：{Type}", exception.GetType().Name);
            return new Dictionary<string, string>();
        }
    }
}

/// <summary>现场编辑的写入：白名单校验 → 规范化/清洗 → 写入后台可写库 → 记操作日志（同一次 SaveChanges）。</summary>
public sealed class LiveEditService
{
    private readonly SzaipaAdminContext _db;
    private readonly IOperationRecorder _recorder;
    private readonly ISiteTextProvider _siteText;

    public LiveEditService(SzaipaAdminContext db, IOperationRecorder recorder, ISiteTextProvider siteText)
    {
        _db = db;
        _recorder = recorder;
        _siteText = siteText;
    }

    public async Task<LiveEditResult> SaveFieldAsync(
        string? entity, int id, string? field, string? value, AdminActor actor, CancellationToken cancellationToken)
    {
        if (!LiveEditRegistry.TryGet(entity, field, out var entry, out var fieldEntry))
        {
            return LiveEditResult.Fail("该内容不支持现场编辑。");
        }

        var normalized = Normalize(value, fieldEntry!.Kind);
        if (fieldEntry.Required && normalized.Length == 0)
        {
            return LiveEditResult.Fail("该项不能为空。");
        }

        if (normalized.Length > fieldEntry.MaxLength)
        {
            return LiveEditResult.Fail($"内容过长（最多 {fieldEntry.MaxLength} 字）。");
        }

        try
        {
            var record = await _db.FindAsync(entry!.ClrType, new object[] { id }, cancellationToken);
            if (record is null)
            {
                return LiveEditResult.Fail($"未找到该{entry.Noun}（可能已被删除）。");
            }

            _db.Entry(record).Property(fieldEntry.Name).CurrentValue = normalized;
            await _recorder.RecordAsync(_db, actor, $"现场编辑：{entry.Noun} {id} 的 {fieldEntry.Name}", cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return LiveEditResult.Ok(normalized);
        }
        catch (Exception exception) when (IsMissingSchema(exception))
        {
            return LiveEditResult.Fail("数据库尚未执行本次升级 SQL，暂时无法保存该内容。");
        }
    }

    public async Task<LiveEditResult> SaveSiteTextAsync(
        string? key, string? value, LiveEditKind kind, AdminActor actor, CancellationToken cancellationToken)
    {
        if (!LiveEditRegistry.IsValidSiteTextKey(key))
        {
            return LiveEditResult.Fail("文案键无效。");
        }

        var normalized = Normalize(value, kind);
        if (normalized.Length > LiveEditRegistry.SiteTextMaxLength)
        {
            return LiveEditResult.Fail("内容过长。");
        }

        try
        {
            var record = await _db.SiteText.FindAsync(new object[] { key! }, cancellationToken);
            if (record is null)
            {
                record = new SiteText { Key = key! };
                _db.SiteText.Add(record);
            }

            record.Value = normalized;
            record.UpdatedAt = DateTime.Now;
            record.UpdatedBy = actor.StaffName.Length > 100 ? actor.StaffName[..100] : actor.StaffName;
            await _recorder.RecordAsync(_db, actor, $"现场编辑：站点文案 {key}", cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            _siteText.Invalidate();
            return LiveEditResult.Ok(normalized);
        }
        catch (Exception exception) when (IsMissingSchema(exception))
        {
            return LiveEditResult.Fail("数据库尚未建立 SiteText 表，请先执行 docs/sql/2026-10-26-site-text.sql。");
        }
    }

    /// <summary>纯文本：统一换行、去首尾空白，单行去掉换行；富文本：白名单清洗。</summary>
    public static string Normalize(string? value, LiveEditKind kind)
    {
        var text = (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Replace(' ', ' ');
        return kind switch
        {
            LiveEditKind.Html => RichTextSanitizer.Sanitize(text).Trim(),
            LiveEditKind.Lines => string.Join('\n', text.Split('\n').Select(line => line.TrimEnd())).Trim('\n', ' '),
            _ => string.Join(' ', text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim())).Trim(),
        };
    }

    private static bool IsMissingSchema(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException db
                && (SchemaCompatibility.IsMissingColumn(db)
                    || SchemaCompatibility.IsMissingTable(db, "SiteText")
                    || LiveEditTables.Any(table => SchemaCompatibility.IsMissingTable(db, table))))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] LiveEditTables =
        { nameof(FeaturedExhibition), nameof(OrgMember), nameof(SaleWork), nameof(Partner), nameof(ArtistBook) };
}
