namespace Szaipa.Data.Models.Home;

public sealed class PublicationDetailSnapshotModel
{
    public required PublicationDetailModel Publication { get; init; }

    public required IReadOnlyList<PublicationSummaryModel> RelatedPublications { get; init; }
}
