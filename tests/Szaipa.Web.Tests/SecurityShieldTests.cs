using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Szaipa.Web.Middleware;
using Szaipa.Web.Services.Security;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class SecurityShieldTests
{
    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static ScannerShield Shield(ManualTime time, params string[] whitelist) =>
        new(() => whitelist, time, NullLogger<ScannerShield>.Instance);

    [Theory]
    [InlineData("/wp-login.php", true)]
    [InlineData("/index.php", true)]
    [InlineData("/.env", true)]
    [InlineData("/.git/config", true)]
    [InlineData("/phpmyadmin/", true)]
    [InlineData("/vendor/phpunit/src/Util/PHP/eval-stdin.php", true)]
    [InlineData("/cgi-bin/luci", true)]
    [InlineData("/backup.sql", true)]
    [InlineData("/", false)]
    [InlineData("/Home/News", false)]
    [InlineData("/Home/Art/1009", false)]
    [InlineData("/Content/images/a.jpg", false)]
    [InlineData("/Staff/Account/Login", false)]
    [InlineData("/.well-known/acme-challenge/abc", false)] // 证书自动续期
    [InlineData("/_img/960/Content/WeChat/2026/10/x.jpg", false)]
    public void Scanner_paths_are_recognised_without_hitting_real_pages(string path, bool expected) =>
        Assert.Equal(expected, ScannerShield.IsScannerPath(path));

    [Fact]
    public void Ip_is_banned_after_threshold_within_window_and_released_after_duration()
    {
        var time = new ManualTime();
        var shield = Shield(time);
        const string ip = "203.0.113.7";

        for (var i = 1; i < ScannerShield.BanThreshold; i++)
        {
            Assert.False(shield.RecordScan(ip));
        }

        Assert.False(shield.IsBanned(ip));
        Assert.True(shield.RecordScan(ip));
        Assert.True(shield.IsBanned(ip));
        Assert.Equal(1, shield.ActiveBanCount);

        time.Now += ScannerShield.BanDuration + TimeSpan.FromSeconds(1);
        Assert.False(shield.IsBanned(ip));
    }

    [Fact]
    public void Slow_scans_outside_the_window_do_not_accumulate()
    {
        var time = new ManualTime();
        var shield = Shield(time);
        for (var i = 0; i < ScannerShield.BanThreshold * 2; i++)
        {
            shield.RecordScan("203.0.113.8");
            time.Now += ScannerShield.Window / (ScannerShield.BanThreshold - 1) + TimeSpan.FromSeconds(10);
        }

        Assert.False(shield.IsBanned("203.0.113.8"));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("192.168.50.191")]
    [InlineData("172.18.253.196")]
    [InlineData("157.120.59.35")] // 白名单
    public void Loopback_private_and_whitelisted_ips_are_never_banned(string ip)
    {
        var shield = Shield(new ManualTime(), "# 注释", "157.120.59.35");
        for (var i = 0; i < ScannerShield.BanThreshold * 3; i++)
        {
            shield.RecordScan(ip);
        }

        Assert.False(shield.IsBanned(ip));
    }

    [Fact]
    public void Login_locks_ip_after_repeated_failures_and_success_resets()
    {
        var time = new ManualTime();
        var throttle = new LoginThrottle(time);
        const string ip = "198.51.100.9";

        for (var i = 1; i < LoginThrottle.IpLimit; i++)
        {
            throttle.RecordFailure(ip, "user" + i); // 换账户也按 IP 计
        }

        Assert.False(throttle.IsLocked(ip, "anyone", out _));
        throttle.RecordFailure(ip, "admin");
        Assert.True(throttle.IsLocked(ip, "anyone", out var remaining));
        Assert.True(remaining > TimeSpan.FromMinutes(14));

        time.Now += LoginThrottle.LockDuration + TimeSpan.FromSeconds(1);
        Assert.False(throttle.IsLocked(ip, "anyone", out _));

        throttle.RecordFailure(ip, "admin");
        throttle.RecordSuccess(ip, "admin");
        Assert.False(throttle.IsLocked(ip, "admin", out _));
    }

    [Fact]
    public void Login_locks_account_attacked_from_many_ips()
    {
        var throttle = new LoginThrottle(new ManualTime());
        for (var i = 0; i < LoginThrottle.AccountLimit; i++)
        {
            throttle.RecordFailure("198.51.100." + i, "Admin ");
        }

        Assert.True(throttle.IsLocked("198.51.100.200", "admin", out _));
        Assert.False(throttle.IsLocked("198.51.100.200", "other", out _));
    }

    [Fact]
    public async Task Middleware_returns_404_for_scans_403_when_banned_and_adds_security_headers()
    {
        var shield = Shield(new ManualTime());
        var reached = 0;
        var middleware = new SecurityShieldMiddleware(_ => { reached++; return Task.CompletedTask; }, shield);

        async Task<HttpContext> Send(string path)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.50");
            context.Request.Path = path;
            await middleware.InvokeAsync(context);
            return context;
        }

        var normal = await Send("/Home/News");
        Assert.Equal(1, reached);
        Assert.Equal(200, normal.Response.StatusCode);

        for (var i = 0; i < ScannerShield.BanThreshold; i++)
        {
            Assert.Equal(404, (await Send("/wp-login.php")).Response.StatusCode);
        }

        Assert.Equal(403, (await Send("/Home/News")).Response.StatusCode);
        Assert.Equal(1, reached); // 扫描与封禁请求都没进入后续管道
    }
}
