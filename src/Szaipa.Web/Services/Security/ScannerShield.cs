using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Szaipa.Web.Services.Security;

/// <summary>
/// 漏洞扫描拦截（2026-10-28）：正式站近 30 天约 1.36 万次请求是在探测 .php / wp- / .env / .git 等本站根本不存在的路径。
/// <list type="bullet">
/// <item>这类路径在管道最前面直接返回 404，不再走静态文件、路由和 MVC；</item>
/// <item>同一 IP 在 <see cref="Window"/> 内探测达到 <see cref="BanThreshold"/> 次，封禁 <see cref="BanDuration"/>，
///   封禁期内该 IP 的所有请求直接 403；</item>
/// <item>本机、内网和服务器白名单（C:\Security\whitelist.txt）里的 IP 永不封禁。</item>
/// </list>
/// 状态在进程内存中，应用池重启即清空；服务器防火墙层面的封禁仍由 C:\Security\AutoBlock.ps1 负责（需防火墙开启）。
/// </summary>
public sealed partial class ScannerShield
{
    public const int BanThreshold = 10;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan BanDuration = TimeSpan.FromHours(24);

    private const int MaxTrackedIps = 20_000;

    private static readonly TimeSpan WhitelistRefresh = TimeSpan.FromMinutes(5);

    private const string ScannerPathPattern =
        @"\.(php\d?|phtml|asp|aspx|jsp|cgi|env|ini|bak|sql|swp|old|log)$" +
        @"|/\.(git|env|aws|svn|hg|ds_store|vscode|idea|htaccess|htpasswd)(/|$)" +
        @"|/wp-|/wordpress|phpmyadmin|/pma/|xmlrpc|/cgi-bin/|/vendor/phpunit|/actuator|/boaform|/hnap1" +
        @"|/druid/|/manager/html|/jenkins|/geoserver|/solr/|/owa/|/autodiscover/";

    private readonly ConcurrentDictionary<string, HitWindow> _hits = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _bans = new(StringComparer.Ordinal);
    private readonly Func<IReadOnlyList<string>> _whitelistSource;
    private readonly TimeProvider _time;
    private readonly ILogger<ScannerShield> _logger;
    private readonly object _whitelistGate = new();
    private HashSet<string> _whitelist = new(StringComparer.Ordinal);
    private DateTimeOffset _whitelistLoadedAt = DateTimeOffset.MinValue;

    public ScannerShield(IServerSecurityRepository repository, TimeProvider time, ILogger<ScannerShield> logger)
        : this(repository.ReadWhitelist, time, logger)
    {
    }

    public ScannerShield(Func<IReadOnlyList<string>> whitelistSource, TimeProvider time, ILogger<ScannerShield> logger)
    {
        _whitelistSource = whitelistSource;
        _time = time;
        _logger = logger;
    }

    /// <summary>当前封禁中的 IP 数（供监控 / 日志参考）。</summary>
    public int ActiveBanCount
    {
        get
        {
            var now = _time.GetUtcNow();
            return _bans.Count(pair => pair.Value > now);
        }
    }

    public static bool IsScannerPath(string? path) =>
        !string.IsNullOrEmpty(path) && ScannerPathRegex().IsMatch(path);

    // 只收本站不可能存在的探测路径。刻意不含 /.well-known/（证书自动续期要用）和任何真实页面前缀。
    // 2026-10-30：原先用运行时编译 + 50ms 超时，应用池刚启动、CPU 忙时首次匹配（含编译）会超时抛异常，
    // 让正常页面返回 500。改为编译期源生成；模式只是字面量的并列，没有嵌套量词，匹配耗时与路径长度成正比，不需要超时。
    [GeneratedRegex(ScannerPathPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScannerPathRegex();

    public bool IsBanned(string ip)
    {
        if (string.IsNullOrEmpty(ip) || !_bans.TryGetValue(ip, out var until))
        {
            return false;
        }

        if (until > _time.GetUtcNow())
        {
            return true;
        }

        _bans.TryRemove(ip, out _);
        return false;
    }

    /// <summary>记一次探测；达到阈值返回 true（本次起封禁）。白名单 / 本机 / 内网只记不封。</summary>
    public bool RecordScan(string ip)
    {
        if (string.IsNullOrEmpty(ip) || IsExempt(ip))
        {
            return false;
        }

        var now = _time.GetUtcNow();
        if (_hits.Count > MaxTrackedIps)
        {
            Prune(now);
        }

        var window = _hits.AddOrUpdate(
            ip,
            _ => new HitWindow(now, 1),
            (_, current) => now - current.Start > Window ? new HitWindow(now, 1) : current with { Count = current.Count + 1 });

        if (window.Count < BanThreshold)
        {
            return false;
        }

        _bans[ip] = now + BanDuration;
        _hits.TryRemove(ip, out _);
        _logger.LogWarning("漏洞扫描拦截：{Ip} 在 {Minutes} 分钟内探测 {Count} 次，封禁 {Hours} 小时",
            ip, Window.TotalMinutes, window.Count, BanDuration.TotalHours);
        return true;
    }

    private bool IsExempt(string ip)
    {
        if (IPAddress.TryParse(ip, out var address))
        {
            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            var bytes = address.GetAddressBytes();
            if (bytes.Length == 4 && (bytes[0] == 10 || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168)))
            {
                return true;
            }
        }

        return Whitelist().Contains(ip);
    }

    private HashSet<string> Whitelist()
    {
        var now = _time.GetUtcNow();
        if (now - _whitelistLoadedAt < WhitelistRefresh)
        {
            return _whitelist;
        }

        lock (_whitelistGate)
        {
            if (now - _whitelistLoadedAt >= WhitelistRefresh)
            {
                try
                {
                    _whitelist = _whitelistSource()
                        .Select(line => line.Trim())
                        .Where(line => line.Length > 0 && !line.StartsWith('#'))
                        .ToHashSet(StringComparer.Ordinal);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // 读不到白名单时沿用上次的结果，不影响请求。
                }

                _whitelistLoadedAt = now;
            }

            return _whitelist;
        }
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var pair in _hits)
        {
            if (now - pair.Value.Start > Window)
            {
                _hits.TryRemove(pair.Key, out _);
            }
        }

        foreach (var pair in _bans)
        {
            if (pair.Value <= now)
            {
                _bans.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed record HitWindow(DateTimeOffset Start, int Count);
}
