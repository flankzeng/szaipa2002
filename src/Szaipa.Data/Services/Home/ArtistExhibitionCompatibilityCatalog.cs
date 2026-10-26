using Szaipa.Data.Models.Home;
using Szaipa.Data.Services.Admin;

namespace Szaipa.Data.Services.Home;

/// <summary>Production Publication may still be empty before the slug migration runs; these three existing Art slots remain selectable.</summary>
public static class ArtistExhibitionCompatibilityCatalog
{
    public const int SpringOneId = 92003;
    public const int SpringTwoId = 92007;
    public const int TonggouTwoId = 99999;

    private static readonly ArtistExhibitionItemModel[] Items =
    [
        new() { Id=SpringOneId, Title="“春语”——当代艺术名家邀请展", CoverUrl="/Content/images/zhongyi/10000.jpg", Location="深圳市动漫园·盛世艺术空间", StartDate="2022.03.29", Link="/Home/Publication/92003" },
        new() { Id=SpringTwoId, Title="“春语”第二季——国际视觉艺术邀请展", CoverUrl="/Content/images/IMG_9282.jpg", Location="深圳动漫园·同构艺术空间", StartDate="2023.04.21", EndDate="2023.05.28", Link="/Home/Publication/92007" },
        new() { Id=TonggouTwoId, Title="同构《第二季》·当代艺术展", CoverUrl="/Content/images/_DSF7252.jpg", Location="深圳市罗湖美术馆", StartDate="2022.12.29", EndDate="2023.01.28", Link="/Publication/tonggou2" }
    ];

    public static IReadOnlyList<PublicationOption> GetOptions()
    {
        var named = Items
            .Select(item => new PublicationOption(item.Id, item.Title, Parse(item.StartDate), item.Location))
            .ToDictionary(option => option.Id);
        foreach (var id in CompatibilityIds)
        {
            named.TryAdd(id, new PublicationOption(id, $"兼容展览 #{id}", null, null));
        }

        return named.Values.ToArray();
    }

    public static IReadOnlySet<int> CompatibilityIds => Enumerable.Range(92001, 15)
        .Where(id => id != 92004)
        .Append(TonggouTwoId)
        .ToHashSet();

    public static ArtistExhibitionItemModel? Find(int id) => Items.FirstOrDefault(item => item.Id == id)
        ?? (CompatibilityIds.Contains(id)
            ? new ArtistExhibitionItemModel
            {
                Id = id,
                Title = $"兼容展览 #{id}",
                Link = id == TonggouTwoId ? "/Publication/tonggou2" : $"/Home/Publication/{id}"
            }
            : null);

    private static DateTime? Parse(string? value) => DateTime.TryParseExact(value, "yyyy.MM.dd", null, System.Globalization.DateTimeStyles.None, out var date) ? date : null;
}
