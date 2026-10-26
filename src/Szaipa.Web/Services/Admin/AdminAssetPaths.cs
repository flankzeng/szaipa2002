namespace Szaipa.Web.Services.Admin;

/// <summary>
/// 后台写入 Content 目录的安全路径规则（从 <see cref="AdminAssetStorage"/> 抽出，供公众号图片存储复用）：
/// 子目录净化（拒绝 <c>..</c>、盘符、非法文件名字符）+ 目标目录不得逃出 ContentRoot。
/// </summary>
internal static class AdminAssetPaths
{
    public const string DefaultSubfolder = "newsImg";

    public static string SanitizeSubfolder(string subfolder)
    {
        if (string.IsNullOrWhiteSpace(subfolder))
        {
            return DefaultSubfolder;
        }

        // Allow safe nested folders (e.g. "ArtImg/Artist/ArtNews") but reject traversal, drive letters,
        // and any segment with invalid file-name characters.
        var invalid = Path.GetInvalidFileNameChars();
        var segments = subfolder.Trim().Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var clean = new List<string>();
        foreach (var raw in segments)
        {
            var segment = raw.Trim();
            if (segment == ".")
            {
                continue;
            }

            if (segment == ".." || segment.Contains(':') || segment.IndexOfAny(invalid) >= 0)
            {
                return DefaultSubfolder;
            }

            clean.Add(segment);
        }

        return clean.Count == 0 ? DefaultSubfolder : string.Join('/', clean);
    }

    /// <summary>
    /// 解析 <c>{contentRoot}/{subfolder}</c> 的绝对目录；contentRoot 未配置/不存在，或结果逃出 contentRoot 时返回 null。
    /// </summary>
    public static string? ResolveDirectory(string? contentRoot, string safeSubfolder)
    {
        if (string.IsNullOrWhiteSpace(contentRoot) || !Directory.Exists(contentRoot))
        {
            return null;
        }

        var targetDirectory = Path.Combine(contentRoot, safeSubfolder.Replace('/', Path.DirectorySeparatorChar));
        var rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentRoot));
        var targetFull = Path.GetFullPath(targetDirectory);
        return targetFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? targetDirectory : null;
    }
}
