using Microsoft.Extensions.Options;
using Szaipa.Data.Services.WeChat;
using Szaipa.Web.Configuration;

namespace Szaipa.Web.Services.Admin;

/// <summary>
/// <see cref="IWeChatImageStore"/> over <see cref="LegacyAssetsOptions.ContentRoot"/>，与 <see cref="AdminAssetStorage"/>
/// 共用同一套安全路径规则（<see cref="AdminAssetPaths"/>）。管理员机器的 ContentRoot 指向<b>本地可写副本</b>，
/// 绝不指向生产资料（web24.05/Content 是只读副本，验证时请另配临时目录）。
/// </summary>
public sealed class WeChatImageStore : IWeChatImageStore
{
    private readonly IOptions<LegacyAssetsOptions> _legacyAssets;

    public WeChatImageStore(IOptions<LegacyAssetsOptions> legacyAssets)
    {
        _legacyAssets = legacyAssets;
    }

    public bool IsAvailable
    {
        get
        {
            var root = _legacyAssets.Value.ContentRoot;
            return !string.IsNullOrWhiteSpace(root) && Directory.Exists(root);
        }
    }

    public string? TryGetExistingUrl(string subfolder, string fileName)
    {
        var directory = ResolveDirectory(subfolder, fileName, out var safeSubfolder);
        if (directory is null)
        {
            return null;
        }

        return File.Exists(Path.Combine(directory, fileName)) ? $"/Content/{safeSubfolder}/{fileName}" : null;
    }

    public async Task<string?> SaveAsync(string subfolder, string fileName, byte[] data, CancellationToken cancellationToken)
    {
        var directory = ResolveDirectory(subfolder, fileName, out var safeSubfolder);
        if (directory is null)
        {
            return null;
        }

        var url = $"/Content/{safeSubfolder}/{fileName}";
        var fullPath = Path.Combine(directory, fileName);
        if (File.Exists(fullPath))
        {
            return url;
        }

        try
        {
            Directory.CreateDirectory(directory);

            // 先写临时文件再改名，避免并发/中断留下半截图片被前台读到。
            var temp = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temp, data, cancellationToken);
            try
            {
                File.Move(temp, fullPath, overwrite: false);
            }
            catch (IOException)
            {
                File.Delete(temp); // 另一路已写好同名文件
            }

            return url;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string? ResolveDirectory(string subfolder, string fileName, out string safeSubfolder)
    {
        safeSubfolder = AdminAssetPaths.SanitizeSubfolder(subfolder);

        // 文件名只允许字母数字、连字符和单个扩展名点，杜绝路径片段。
        if (fileName.Length == 0 || fileName.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '.')) || fileName.Contains(".."))
        {
            return null;
        }

        // 调用方传入的子目录必须原样通过净化（不接受被静默改写成 newsImg）。
        var requested = subfolder.Trim().Replace('\\', '/').Trim('/');
        if (!string.Equals(requested, safeSubfolder, StringComparison.Ordinal))
        {
            return null;
        }

        return AdminAssetPaths.ResolveDirectory(_legacyAssets.Value.ContentRoot, safeSubfolder);
    }
}
