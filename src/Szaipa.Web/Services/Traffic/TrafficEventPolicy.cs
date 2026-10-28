namespace Szaipa.Web.Services.Traffic;

public static class TrafficEventPolicy
{
    public const int MaxPathLength = 512;
    private static readonly string[] ExcludedPrefixes = ["/admin", "/staff", "/api"];

    public static bool TryNormalizePath(string? value, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxPathLength || value[0] != '/' || value.StartsWith("//", StringComparison.Ordinal))
            return false;

        var query = value.IndexOfAny(['?', '#']);
        var candidate = query >= 0 ? value[..query] : value;
        candidate = Uri.UnescapeDataString(candidate);
        if (candidate.Contains('%') || candidate.Contains('\\') || candidate.StartsWith("//", StringComparison.Ordinal))
            return false;
        if (candidate.Length == 0 || candidate.Any(c => char.IsControl(c) || c == '\\'))
            return false;
        if (candidate.Split('/').Any(segment => segment is "." or "..")) return false;

        if (ExcludedPrefixes.Any(prefix => candidate.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)))
            return false;

        path = candidate;
        return true;
    }

    public static bool IsValidKind(string? kind) =>
        string.Equals(kind, "view", StringComparison.Ordinal) || string.Equals(kind, "click", StringComparison.Ordinal);
}
