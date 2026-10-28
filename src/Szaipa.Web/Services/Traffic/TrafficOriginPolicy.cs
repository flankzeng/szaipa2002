namespace Szaipa.Web.Services.Traffic;

public static class TrafficOriginPolicy
{
    public static bool IsSameOrigin(string? originValue, string? fetchSite, string requestScheme, string requestHost, int? requestPort)
    {
        if (!string.IsNullOrWhiteSpace(fetchSite) && !string.Equals(fetchSite, "same-origin", StringComparison.OrdinalIgnoreCase)) return false;
        if (!Uri.TryCreate(originValue, UriKind.Absolute, out var origin)) return false;
        var expectedPort = requestPort ?? DefaultPort(requestScheme);
        return string.Equals(origin.Scheme, requestScheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(origin.Host, requestHost, StringComparison.OrdinalIgnoreCase)
            && origin.Port == expectedPort;
    }

    private static int DefaultPort(string scheme) => scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;
}
