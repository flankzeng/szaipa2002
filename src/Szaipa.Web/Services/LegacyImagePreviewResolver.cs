using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Szaipa.Web.Configuration;

namespace Szaipa.Web.Services;

/// <summary>
/// Resolves legacy image URLs to the pre-generated q30w1200 preview tree without generating files or
/// exposing physical paths. The preview generator sometimes converted images such as PNG files to JPEG;
/// its output has mixed path casing, which is retained in the public URL through a case-insensitive index.
/// </summary>
public sealed class LegacyImagePreviewResolver : ILegacyImagePreviewResolver
{
    private const string ContentPrefix = "/Content/";
    private const string PreviewUrlPrefix = "/Content/_preview/q30w1200/Content/";

    private readonly string? _previewRoot;
    private readonly Lazy<PreviewIndex> _index;
    private readonly ConcurrentDictionary<string, PreviewResolution> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public LegacyImagePreviewResolver(IOptions<LegacyAssetsOptions> legacyAssets)
    {
        ArgumentNullException.ThrowIfNull(legacyAssets);

        var contentRoot = legacyAssets.Value.ContentRoot;
        if (string.IsNullOrWhiteSpace(contentRoot))
        {
            _index = new(() => PreviewIndex.Empty);
            return;
        }

        try
        {
            _previewRoot = Path.GetFullPath(Path.Combine(
                contentRoot,
                "_preview",
                "q30w1200",
                "Content"));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            // Invalid machine-local configuration is a safe fallback condition, not a request failure.
        }

        _index = new(BuildIndex, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string Resolve(string originalUrl)
    {
        if (string.IsNullOrWhiteSpace(originalUrl) || _previewRoot is null)
        {
            return originalUrl;
        }

        var suffixIndex = FindSuffixIndex(originalUrl);
        var urlPath = suffixIndex < 0 ? originalUrl : originalUrl[..suffixIndex];
        var suffix = suffixIndex < 0 ? string.Empty : originalUrl[suffixIndex..];

        if (!urlPath.StartsWith(ContentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return originalUrl;
        }

        var encodedRelativePath = urlPath[ContentPrefix.Length..];
        if (!TryNormalizeRelativePath(encodedRelativePath, out var relativePath))
        {
            return originalUrl;
        }

        var extension = Path.GetExtension(relativePath);
        if (!IsSupportedSourceExtension(extension))
        {
            return originalUrl;
        }

        var resolution = _cache.GetOrAdd(relativePath, ResolvePreview);
        return resolution.Found ? resolution.Url + suffix : originalUrl;
    }

    private PreviewResolution ResolvePreview(string relativePath)
    {
        foreach (var candidate in GetCandidates(relativePath))
        {
            if (_index.Value.TryResolve(candidate, out var actualPath))
            {
                return new PreviewResolution(true, PreviewUrlPrefix + EncodeUrlPath(actualPath));
            }
        }

        return PreviewResolution.NotFound;
    }

    private PreviewIndex BuildIndex()
    {
        if (_previewRoot is null || !TryGetAttributes(_previewRoot, out var rootAttributes)
            || rootAttributes.HasFlag(FileAttributes.ReparsePoint)
            || !rootAttributes.HasFlag(FileAttributes.Directory))
        {
            return PreviewIndex.Empty;
        }

        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ambiguousPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = new Stack<string>();
        directories.Push(_previewRoot);

        while (directories.TryPop(out var directory))
        {
            string[] childDirectories;
            string[] files;
            try
            {
                childDirectories = Directory.GetDirectories(directory);
                files = Directory.GetFiles(directory);
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                if (TryGetAttributes(childDirectory, out var attributes)
                    && !attributes.HasFlag(FileAttributes.ReparsePoint)
                    && attributes.HasFlag(FileAttributes.Directory))
                {
                    directories.Push(childDirectory);
                }
            }

            foreach (var file in files)
            {
                if (!TryGetAttributes(file, out var attributes)
                    || attributes.HasFlag(FileAttributes.ReparsePoint)
                    || attributes.HasFlag(FileAttributes.Directory))
                {
                    continue;
                }

                string relativePath;
                try
                {
                    relativePath = Path.GetRelativePath(_previewRoot, file)
                        .Replace(Path.DirectorySeparatorChar, '/');
                }
                catch (Exception exception) when (IsFileSystemException(exception))
                {
                    continue;
                }

                if (relativePath == ".." || relativePath.StartsWith("../", StringComparison.Ordinal)
                    || Path.IsPathRooted(relativePath) || ambiguousPaths.Contains(relativePath))
                {
                    continue;
                }

                if (paths.TryGetValue(relativePath, out var existingPath)
                    && !existingPath.Equals(relativePath, StringComparison.Ordinal))
                {
                    paths.Remove(relativePath);
                    ambiguousPaths.Add(relativePath);
                    continue;
                }

                paths[relativePath] = relativePath;
            }
        }

        return new PreviewIndex(paths);
    }

    private static bool TryNormalizeRelativePath(string encodedPath, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(encodedPath))
        {
            return false;
        }

        var encodedSegments = encodedPath.Split('/');
        var decodedSegments = new string[encodedSegments.Length];
        for (var index = 0; index < encodedSegments.Length; index++)
        {
            var segment = encodedSegments[index];
            try
            {
                // Decode a bounded number of layers so %252e%252e cannot become traversal after acceptance.
                for (var pass = 0; pass < 3; pass++)
                {
                    var next = Uri.UnescapeDataString(segment);
                    if (next == segment)
                    {
                        break;
                    }

                    segment = next;
                }
            }
            catch (UriFormatException)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(segment)
                || segment is "." or ".."
                || segment.Contains('/')
                || segment.Contains('\\')
                || segment.Contains('%')
                || segment.Contains('?')
                || segment.Contains('#')
                || segment.IndexOf('\0') >= 0
                || segment.Any(char.IsControl))
            {
                return false;
            }

            decodedSegments[index] = segment;
        }

        normalizedPath = string.Join('/', decodedSegments);
        if (normalizedPath.StartsWith("_preview/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static IEnumerable<string> GetCandidates(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        var jpegCandidate = relativePath[..^extension.Length] + ".jpg";
        yield return jpegCandidate;

        if (!extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
        {
            // Retained for future preview generators; the audited current preview tree contains JPEG only.
            yield return relativePath;
        }
    }

    private static bool IsSupportedSourceExtension(string extension) =>
        extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".png", StringComparison.OrdinalIgnoreCase);

    private static string EncodeUrlPath(string relativePath) =>
        string.Join('/', relativePath.Split('/').Select(Uri.EscapeDataString));

    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            attributes = default;
            return false;
        }
    }

    private static bool IsFileSystemException(Exception exception) =>
        exception is ArgumentException or IOException or NotSupportedException
            or UnauthorizedAccessException or System.Security.SecurityException;

    private static int FindSuffixIndex(string url)
    {
        var queryIndex = url.IndexOf('?');
        var fragmentIndex = url.IndexOf('#');

        if (queryIndex < 0)
        {
            return fragmentIndex;
        }

        if (fragmentIndex < 0)
        {
            return queryIndex;
        }

        return Math.Min(queryIndex, fragmentIndex);
    }

    private readonly record struct PreviewResolution(bool Found, string Url)
    {
        public static PreviewResolution NotFound { get; } = new(false, string.Empty);
    }

    private sealed class PreviewIndex
    {
        public static PreviewIndex Empty { get; } = new(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        private readonly IReadOnlyDictionary<string, string> _paths;

        public PreviewIndex(IReadOnlyDictionary<string, string> paths)
        {
            _paths = paths;
        }

        public bool TryResolve(string candidate, out string actualPath) =>
            _paths.TryGetValue(candidate, out actualPath!);
    }
}
