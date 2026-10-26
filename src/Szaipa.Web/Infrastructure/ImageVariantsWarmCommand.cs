using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Szaipa.Web.Configuration;
using Szaipa.Web.Services;

namespace Szaipa.Web.Infrastructure;

/// <summary>
/// T8 存量图片预热：遍历 ContentRoot 下所有 JPG/PNG（≥40KB，跳过 _preview），预先生成 WebP 变体写入缓存目录，
/// 上线后访客不必等首次生成。只读原图、只写缓存目录；可重复执行（已是最新的自动跳过），可随时中断。
/// 用法：<c>dotnet Szaipa.Web.dll image-variants-warm [--widths 480,960,1600] [--only images/man]</c>
/// </summary>
public static class ImageVariantsWarmCommand
{
    public static async Task<int> RunAsync(IConfiguration configuration, string environmentName, string contentRootPath, string[] args, TextWriter? output = null)
    {
        output ??= Console.Out;
        var assets = new LegacyAssetsOptions();
        configuration.GetSection(LegacyAssetsOptions.SectionName).Bind(assets);
        var options = new ImageVariantOptions();
        configuration.GetSection(ImageVariantOptions.SectionName).Bind(options);
        var variants = new ImageVariants(Options.Create(assets), Options.Create(options),
            new CommandEnvironment(environmentName, contentRootPath), NullLogger<ImageVariants>.Instance);
        if (!variants.Enabled)
        {
            output.WriteLine("图片变体未启用：请配置 LegacyAssets:ContentRoot 与 ImageVariants:CacheRoot（绝对路径）。");
            return 2;
        }

        var widths = ParseWidths(Option(args, "--widths")) ?? new[] { 480, 960, 1600 };
        var root = Path.GetFullPath(assets.ContentRoot!);
        var only = Option(args, "--only");
        var start = string.IsNullOrWhiteSpace(only) ? root : Path.GetFullPath(Path.Combine(root, only));
        if (!start.StartsWith(root, StringComparison.Ordinal) || !Directory.Exists(start))
        {
            output.WriteLine($"目录不存在：{only}");
            return 2;
        }

        var files = Directory.EnumerateFiles(start, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .Where(f => !Path.GetRelativePath(root, f).Replace('\\', '/').StartsWith("_preview/", StringComparison.OrdinalIgnoreCase))
            .Where(f => new FileInfo(f).Length >= 40_000)
            .ToList();
        output.WriteLine($"待处理 {files.Count} 张原图，宽度 {string.Join('/', widths)}。");

        long sourceBytes = 0, failed = 0, done = 0;
        var generated = widths.ToDictionary(w => w, _ => 0L);
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            sourceBytes += new FileInfo(file).Length;
            foreach (var width in widths)
            {
                var target = await variants.GetOrCreateAsync(width, relative, CancellationToken.None);
                if (target is null)
                {
                    failed++;
                    continue;
                }

                generated[width] += new FileInfo(target).Length;
            }

            if (++done % 200 == 0)
            {
                output.WriteLine($"  已处理 {done}/{files.Count}");
            }
        }

        output.WriteLine($"完成：原图合计 {sourceBytes / 1048576.0:F1} MB；" +
            string.Join("；", widths.Select(w => $"{w} 宽合计 {generated[w] / 1048576.0:F1} MB")) +
            $"；失败 {failed} 次（失败的图片页面会自动回退原图）。");
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int[]? ParseWidths(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : text.Split(',').Select(t => int.TryParse(t, out var w) ? w : 0).Where(ImageVariants.Widths.Contains).Distinct().ToArray();

    private sealed class CommandEnvironment(string name, string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Szaipa.Web";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
