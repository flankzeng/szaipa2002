namespace Szaipa.Web.Services;

/// <summary>
/// Exact image order used by the fourteen retired hand-written publication pages.
/// The external legacy Content tree remains untouched; these paths bridge its historical
/// naming schemes until each gallery is deliberately normalized through the admin workflow.
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

    public static IReadOnlyList<string> GetImages(int publicationId) =>
        ImagesByPublicationId.TryGetValue(publicationId, out var images)
            ? images
            : Array.Empty<string>();

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
