namespace Szaipa.Web.Infrastructure;

/// <summary>
/// Resolves the Data Protection key directory without touching the file system. Production-like
/// environments fail closed when keys would be missing, relative, or tied to an immutable release.
/// </summary>
public static class DataProtectionKeyPathResolver
{
    private static readonly string DevelopmentFallback = Path.Combine(
        "App_Data",
        "DataProtection-Keys");

    public static string Resolve(
        string contentRoot,
        string? configuredKeysPath,
        bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(contentRoot))
        {
            throw new ArgumentException("The application content root is required.", nameof(contentRoot));
        }

        var normalizedContentRoot = Path.GetFullPath(contentRoot);
        if (string.IsNullOrWhiteSpace(configuredKeysPath))
        {
            if (isDevelopment)
            {
                return Path.GetFullPath(Path.Combine(normalizedContentRoot, DevelopmentFallback));
            }

            throw new InvalidOperationException(
                "DataProtection:KeysPath is required outside Development and must point to a persistent "
                + "absolute directory outside the application release root.");
        }

        var expandedKeysPath = Environment.ExpandEnvironmentVariables(configuredKeysPath.Trim());
        if (!Path.IsPathRooted(expandedKeysPath))
        {
            if (isDevelopment)
            {
                return Path.GetFullPath(Path.Combine(normalizedContentRoot, expandedKeysPath));
            }

            throw new InvalidOperationException(
                "DataProtection:KeysPath must be an absolute path outside Development.");
        }

        var normalizedKeysPath = Path.GetFullPath(expandedKeysPath);
        if (!isDevelopment && IsWithin(normalizedKeysPath, normalizedContentRoot))
        {
            throw new InvalidOperationException(
                "DataProtection:KeysPath must be outside the application release root so keys survive "
                + "atomic release switches.");
        }

        return normalizedKeysPath;
    }

    private static bool IsWithin(string candidatePath, string rootPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, candidatePath);
        if (relativePath == ".")
        {
            return true;
        }

        if (Path.IsPathRooted(relativePath) || relativePath == "..")
        {
            return false;
        }

        return !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
