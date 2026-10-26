namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// A 合作伙伴 card on the About page. Order is display order; the first two partners also appear in the
/// phone layout with their description hidden, the rest appear in a second logo-only phone row. The
/// "合作案例" section on About is an empty, hidden placeholder and is intentionally NOT data-driven.
/// Table is additive — readers must tolerate it not existing yet.
/// </summary>
public sealed class Partner : ISortableRecord
{
    public int Id { get; set; }

    /// <summary>Display name; a line break (newline) renders as &lt;br /&gt;.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Site-relative logo URL under /Content.</summary>
    public string? LogoPath { get; set; }

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    /// <summary>Optional external link (http/https). When set, logo and name link to it.</summary>
    public string? Link { get; set; }

    /// <summary>简介 (plain text; newlines collapse like the old hand-written markup).</summary>
    public string? Description { get; set; }

    public int Sort { get; set; }

    public bool IsVisible { get; set; } = true;
}
