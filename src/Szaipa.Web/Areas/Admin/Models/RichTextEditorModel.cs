namespace Szaipa.Web.Areas.Admin.Models;

/// <summary>
/// Drives the <c>_RichTextEditor</c> partial (TipTap). The editor keeps the hidden input named
/// <see cref="Name"/> in sync with the HTML, so a normal form POST submits the content like any field.
/// </summary>
public sealed class RichTextEditorModel
{
    /// <summary>Hidden input name = the form field the content posts as (e.g. "Content").</summary>
    public required string Name { get; init; }

    /// <summary>Existing HTML to load into the editor (edit pages); empty for new records.</summary>
    public string? InitialHtml { get; init; }

    /// <summary>Content subfolder for inline image uploads (e.g. "newsImg").</summary>
    public string UploadFolder { get; init; } = "newsImg";

    public string? Placeholder { get; init; }

    /// <summary>Stable DOM id linking the mount container to its hidden input.</summary>
    public string FieldId => "rte_" + Name.Replace('.', '_').Replace('[', '_').Replace(']', '_');
}
