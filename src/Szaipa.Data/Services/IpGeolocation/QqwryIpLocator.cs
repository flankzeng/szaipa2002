namespace Szaipa.Data.Services.IpGeolocation;

/// <summary>
/// <see cref="IQqwryIpLocator"/> 的默认实现：qqwry.dat 路径解析 + 首用懒加载（避免启动时读 25MB 文件）。
/// 路径解析顺序：显式配置（IpGeolocation:QqwryPath）→ 当前目录 App_Data/security/qqwry.dat →
/// 当前目录 App_Data/qqwry.dat。宿主可按 content root 显式传路径（见 Szaipa.Web Program.cs 覆盖注册）。
/// </summary>
public sealed class QqwryIpLocator : IQqwryIpLocator
{
    private readonly string? _configuredPath;
    private readonly object _sync = new();
    private IpRegion? _db;
    private string? _loadedPath;

    /// <param name="configuredPath">显式 qqwry.dat 路径；空/不存在时回退到当前目录的 App_Data 候选。</param>
    public QqwryIpLocator(string? configuredPath = null)
    {
        _configuredPath = string.IsNullOrWhiteSpace(configuredPath) ? null : configuredPath;
    }

    public bool IsAvailable => EnsureLoaded() != null;

    public IpRegion.Result? Lookup(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        return EnsureLoaded()?.Lookup(ip);
    }

    private IpRegion? EnsureLoaded()
    {
        var path = ResolvePath();
        lock (_sync)
        {
            if (_db == null || _loadedPath != path)
            {
                _db = IpRegion.TryLoad(path);
                _loadedPath = path;
            }
            return _db;
        }
    }

    private string? ResolvePath()
    {
        if (_configuredPath is not null && File.Exists(_configuredPath)) return _configuredPath;
        var cwd = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(cwd, "App_Data", "security", "qqwry.dat"),
            Path.Combine(cwd, "App_Data", "qqwry.dat")
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
