using System.Collections.Concurrent;

namespace Szaipa.Web.Services.Security;

/// <summary>
/// 后台登录防爆破（2026-10-28）：此前登录接口不限次数。同一 IP 在 <see cref="Window"/> 内输错 <see cref="IpLimit"/> 次、
/// 或同一账户输错 <see cref="AccountLimit"/> 次，锁定 <see cref="LockDuration"/>；锁定期内直接提示，不再查库校验密码。
/// 登录成功清零该 IP 与该账户的计数。状态在进程内存中，应用池重启即清空。
/// </summary>
public sealed class LoginThrottle
{
    public const int IpLimit = 5;

    public const int AccountLimit = 10;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private const int MaxTracked = 20_000;

    private readonly ConcurrentDictionary<string, Counter> _counters = new(StringComparer.Ordinal);
    private readonly TimeProvider _time;

    public LoginThrottle(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>该 IP 或该账户是否处于锁定期；锁定时给出剩余时间。</summary>
    public bool IsLocked(string ip, string? account, out TimeSpan remaining)
    {
        var now = _time.GetUtcNow();
        remaining = TimeSpan.Zero;
        foreach (var key in Keys(ip, account))
        {
            if (_counters.TryGetValue(key, out var counter) && counter.LockedUntil is { } until && until > now)
            {
                remaining = until - now > remaining ? until - now : remaining;
            }
        }

        return remaining > TimeSpan.Zero;
    }

    public void RecordFailure(string ip, string? account)
    {
        var now = _time.GetUtcNow();
        if (_counters.Count > MaxTracked)
        {
            Prune(now);
        }

        Bump(IpKey(ip), IpLimit, now);
        if (!string.IsNullOrWhiteSpace(account))
        {
            Bump(AccountKey(account), AccountLimit, now);
        }
    }

    public void RecordSuccess(string ip, string? account)
    {
        foreach (var key in Keys(ip, account))
        {
            _counters.TryRemove(key, out _);
        }
    }

    private void Bump(string key, int limit, DateTimeOffset now) =>
        _counters.AddOrUpdate(
            key,
            _ => new Counter(now, 1, null),
            (_, current) =>
            {
                if (current.LockedUntil is { } until && until > now)
                {
                    return current;
                }

                var next = now - current.Start > Window ? new Counter(now, 1, null) : current with { Count = current.Count + 1 };
                return next.Count >= limit ? next with { LockedUntil = now + LockDuration } : next;
            });

    private void Prune(DateTimeOffset now)
    {
        foreach (var pair in _counters)
        {
            var expired = (pair.Value.LockedUntil ?? pair.Value.Start + Window) <= now;
            if (expired)
            {
                _counters.TryRemove(pair.Key, out _);
            }
        }
    }

    private static IEnumerable<string> Keys(string ip, string? account)
    {
        yield return IpKey(ip);
        if (!string.IsNullOrWhiteSpace(account))
        {
            yield return AccountKey(account);
        }
    }

    private static string IpKey(string ip) => "ip:" + ip;

    private static string AccountKey(string account) => "user:" + account.Trim().ToLowerInvariant();

    private sealed record Counter(DateTimeOffset Start, int Count, DateTimeOffset? LockedUntil);
}
