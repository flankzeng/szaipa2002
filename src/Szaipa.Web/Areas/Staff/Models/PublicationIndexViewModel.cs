using Szaipa.Data.Composition;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;

namespace Szaipa.Web.Areas.Staff.Models;

/// <summary>Exhibition list page: one page of rows, the active lifecycle filter and per-state counts for the tabs.</summary>
public sealed class PublicationIndexViewModel
{
    public required PagedResult<Publication> Paged { get; init; }

    /// <summary>Active filter (<c>null</c> = all).</summary>
    public PublicationLifecycle? Filter { get; init; }

    public required IReadOnlyDictionary<PublicationLifecycle, int> Counts { get; init; }

    public DateTime Today { get; init; } = DateTime.Today;
}
