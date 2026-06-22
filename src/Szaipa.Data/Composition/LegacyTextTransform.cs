namespace Szaipa.Data.Composition;

public static class LegacyTextTransform
{
    public static string TrimForLandingPage(string? subtitle)
    {
        if (string.IsNullOrWhiteSpace(subtitle))
        {
            return string.Empty;
        }

        return subtitle.Length > LegacyDisplayRules.LandingPageNewsSubtitleMaxLength
            ? subtitle[..LegacyDisplayRules.LandingPageNewsSubtitleMaxLength] + "..."
            : subtitle;
    }
}
