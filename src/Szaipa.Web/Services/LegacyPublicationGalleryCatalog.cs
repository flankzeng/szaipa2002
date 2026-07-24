using Szaipa.Data.Models.Home;

namespace Szaipa.Web.Services;

/// <summary>
/// Read-only compatibility data used by the fourteen retired hand-written publication pages.
/// The external legacy Content tree remains untouched; these paths bridge its historical
/// naming schemes, while database rows take precedence whenever they become available.
/// </summary>
public static class LegacyPublicationGalleryCatalog
{
    private static readonly IReadOnlyDictionary<int, IReadOnlyList<string>> ImagesByPublicationId =
        new Dictionary<int, IReadOnlyList<string>>
        {
            [92001] = Range("tonggouEurope", 100000, 30),
            [92002] = Names(
                "shuimai",
                "10001.jpg", "10002.jpg", "10003.jpg", "10004.jpg", "10005.jpg", "10006.jpg",
                "10008.jpg", "10011.jpg", "10012.jpg", "10013.jpg", "10014.jpg", "10015.jpg",
                "10016.jpg", "10017.jpg", "10018.jpg", "10020.png", "10021.jpg", "10022.jpg",
                "10023.jpg", "10024.jpg", "10025.jpg", "10026.jpg", "10027.jpg", "10028.jpg",
                "10029.jpg", "10030.png", "10031.png", "10032.png", "10033.png", "10034.jpg"),
            [92003] = Names(
                "chunyu",
                "01.jpg", "02.jpg", "03.jpg", "04.jpg", "05.jpg", "06.jpg",
                "07.jpg", "08.jpg", "09.jpg", "010.jpg", "011.jpg", "012.png"),
            [92005] = Range("zhongyi", 10000, 24),
            [92006] = Range("tonggou", 10001, 23),
            [92007] = Combine(
                Range("chunyu2", 10001, 9),
                Range("chunyu2", 100010, 8)),
            [92008] = Combine(
                Range("trio", 10001, 9),
                Range("trio", 100010, 9)),
            [92009] = Range("man", 10001, 25),
            [92010] = Range("yijia", 10001, 70),
            [92011] = Range("zhongri", 10001, 57),
            [92012] = Range("shuyuyi", 100000, 131),
            [92013] = Range("chunyu4", 100000, 96),
            [92014] = Combine(
                Range("zhongfa", 100000, 16),
                Combine(
                    Names("zhongfa", "1000016.jpg"),
                    Range("zhongfa", 100017, 28))),
            [92015] = Range("tangqishan", 100000, 26)
        };

    private static readonly IReadOnlyDictionary<int, PublicationDetailModel> PublicationsById =
        new[]
        {
            Publication(
                92001,
                "同构——欧洲行 当代艺术展",
                "Isomorphism - Contemporary Art Exhibition (European Tour)",
                new DateTime(2023, 10, 9),
                new DateTime(2023, 10, 23),
                "tonggouEurope"),
            Publication(
                92002,
                "2023龙游水脉艺术节",
                "LONGYOU LIQUID THREADS ART FESTIVAL",
                new DateTime(2023, 9, 26),
                new DateTime(2023, 12, 26),
                "shuimai"),
            Publication(
                92003,
                "春语·当代艺术名家邀请展",
                "Contemporary Art masters Invitational exhibition",
                new DateTime(2022, 3, 29),
                new DateTime(2022, 4, 29),
                "chunyu"),
            Publication(
                92005,
                "中意艺术名画展",
                "Famous Chinese and Italian Art Paintings Exhibition",
                new DateTime(2021, 9, 24),
                new DateTime(2021, 10, 24),
                "zhongyi"),
            Publication(
                92006,
                "同构——当代艺术作品邀请展",
                "\"isomorphism\" Contemporary Art Exhibition of China",
                new DateTime(2021, 1, 26),
                new DateTime(2021, 2, 26),
                "tonggou"),
            Publication(
                92007,
                "春语第二季——国际视觉艺术邀请展",
                "\"SOUND OF SPRING II\" international Visual Art Invitation Exhibition",
                null,
                new DateTime(2023, 5, 8),
                "chunyu2"),
            Publication(
                92008,
                "三人行——鸥洋/雷双/张岚芊艺术展",
                "\"TRIO\" —— Ou Yang / Lei Shuang / Zhang LanQian Art Exhibition",
                new DateTime(2023, 6, 3),
                new DateTime(2023, 7, 2),
                "trio"),
            Publication(
                92009,
                "漫MAN-艺术时尚先锋展",
                "PIONEER ART AND FASHION EXHIBITION",
                new DateTime(2024, 5, 22),
                new DateTime(2024, 5, 30),
                "man"),
            Publication(
                92010,
                "艺+科技新潮流展",
                "ART PLUS - ART FASHION & TECHNOLOGY",
                new DateTime(2024, 5, 24),
                new DateTime(2024, 5, 31),
                "yijia"),
            Publication(
                92011,
                "同构——中日艺术交流展",
                "ISOMORPHISM - CHINA & JAPAN ART COMMUNICATION EXHIBITION",
                new DateTime(2024, 6, 28),
                null,
                "zhongri"),
            Publication(
                92012,
                "\"数\"与\"艺\"——新文艺群体创作成果展",
                "DIGITAL AND ART - NEW ARTISTIC GROUP CREATION RESULTS EXHIBITION",
                new DateTime(2024, 9, 20),
                null,
                "shuyuyi"),
            Publication(
                92013,
                "春语第四季——当代艺术作品邀请展",
                "Sound of Spring season 4 —— Contemporary Art Invitational Exhibition",
                null,
                new DateTime(2025, 5, 25),
                "chunyu4"),
            Publication(
                92014,
                "深圳-法国国际当代艺术展2025",
                "Shenzhen - Exposition internationale d'art contemporain français",
                null,
                new DateTime(2025, 8, 30),
                "zhongfa"),
            Publication(
                92015,
                "入骨相知——唐岐山当代艺术展",
                "Soul-Deep Resonance —— Tang Qishan: A Contemporary Art Exhibition",
                null,
                new DateTime(2025, 9, 30),
                "tangqishan")
        }.ToDictionary(publication => publication.Id);

    public static IReadOnlyList<string> GetImages(int publicationId) =>
        ImagesByPublicationId.TryGetValue(publicationId, out var images)
            ? images
            : Array.Empty<string>();

    public static PublicationDetailSnapshotModel? GetFallbackPublication(int publicationId) =>
        PublicationsById.TryGetValue(publicationId, out var publication)
            ? new PublicationDetailSnapshotModel
            {
                Publication = publication,
                RelatedPublications = Array.Empty<PublicationSummaryModel>()
            }
            : null;

    private static PublicationDetailModel Publication(
        int id,
        string titleCn,
        string titleEn,
        DateTime? startDate,
        DateTime? endDate,
        string folderName) =>
        new()
        {
            Id = id,
            TitleCn = titleCn,
            TitleEn = titleEn,
            StartDate = startDate,
            EndDate = endDate,
            FolderName = folderName,
            MaxImg = ImagesByPublicationId[id].Count - 1,
            Type = 0
        };

    private static IReadOnlyList<string> Range(string folder, int start, int count) =>
        Enumerable.Range(start, count)
            .Select(number => $"/Content/images/{folder}/{number}.jpg")
            .ToArray();

    private static IReadOnlyList<string> Names(string folder, params string[] names) =>
        names.Select(name => $"/Content/images/{folder}/{name}").ToArray();

    private static IReadOnlyList<string> Combine(
        IReadOnlyList<string> first,
        IReadOnlyList<string> second) =>
        first.Concat(second).ToArray();
}
