namespace Szaipa.Data.Models.Home;

/// <summary>One person card in the homepage 核心成员 / 顾问团队 blocks. <see cref="ArtistId"/> null = unlinked card.</summary>
public sealed record OrgMemberModel(
    string Name,
    string? Role,
    string? Title,
    int? ArtistId,
    string? AvatarPath,
    int? ImageWidth,
    int? ImageHeight)
{
    /// <summary>数据库行 Id；回退到内置默认内容时为 null（此时不可现场编辑）。</summary>
    public int? Id { get; init; }
}

/// <summary>One slide in the homepage 在售艺术品 carousel. <see cref="ImagePath"/> is the original image URL.</summary>
public sealed record SaleWorkModel(
    int? ArtistId,
    string? ArtistName,
    string Title,
    string? Size,
    string? Medium,
    string? ImagePath,
    int? ImageWidth,
    int? ImageHeight)
{
    /// <summary>数据库行 Id；回退到内置默认内容时为 null（此时不可现场编辑）。</summary>
    public int? Id { get; init; }
}

/// <summary>One 合作伙伴 card on the About page.</summary>
public sealed record PartnerModel(
    string Name,
    string? LogoPath,
    int? ImageWidth,
    int? ImageHeight,
    string? Link,
    string? Description)
{
    /// <summary>数据库行 Id；回退到内置默认内容时为 null（此时不可现场编辑）。</summary>
    public int? Id { get; init; }
}

/// <summary>
/// The data-driven static blocks of the homepage, already filtered to visible rows and ordered. When the
/// backing tables are missing or empty the read repository returns the built-in
/// <see cref="Composition.HomeBlocksDefaults"/> (the previously hard-coded content) instead.
/// </summary>
public sealed class HomeStaticBlocksModel
{
    public required IReadOnlyList<OrgMemberModel> CoreMembers { get; init; }

    public required IReadOnlyList<OrgMemberModel> AdvisoryMembers { get; init; }

    public required IReadOnlyList<SaleWorkModel> SaleWorks { get; init; }
}
