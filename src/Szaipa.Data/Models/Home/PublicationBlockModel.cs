namespace Szaipa.Data.Models.Home;

/// <summary>专题页的一块积木（读侧）：类型 + 已规范化的 JSON 内容，由视图层解析渲染。</summary>
public sealed class PublicationBlockModel
{
    public int Id { get; init; }

    public string BlockType { get; init; } = string.Empty;

    /// <summary>JSON object; shape per <see cref="Contexts.Szaipa.PublicationBlockTypes"/>.</summary>
    public string Content { get; init; } = "{}";
}
