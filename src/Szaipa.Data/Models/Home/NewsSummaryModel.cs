namespace Szaipa.Data.Models.Home;

public sealed class NewsSummaryModel
{
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public DateTime? Date { get; init; }

    public string CoverPath { get; init; } = string.Empty;

    /// <summary>
    /// Raw legacy <c>News.Important</c> (nullable). The two public surfaces treat null differently, so the
    /// raw value is preserved here and the rule is applied per surface:
    /// <list type="bullet">
    ///   <item>landing newIndex (via legacy <c>newslist</c>): null and true are featured (<c>!= false</c>);</item>
    ///   <item>news list newnews (raw entity, <c>Important == true</c>): only explicit true is featured (null = normal).</item>
    /// </list>
    /// </summary>
    public bool? Important { get; init; }
}
