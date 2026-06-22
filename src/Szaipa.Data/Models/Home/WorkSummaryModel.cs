namespace Szaipa.Data.Models.Home;

public sealed class WorkSummaryModel
{
    public int Id { get; init; }

    public int ArtistId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public int Width { get; init; }

    public int Height { get; init; }

    public string Tags { get; init; } = string.Empty;

    /// <summary>Legacy <c>Works.Content</c> — the material/size caption shown above each artwork on newArt.</summary>
    public string Content { get; init; } = string.Empty;
}
