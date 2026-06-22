using Szaipa.Data.Configuration;

namespace Szaipa.Data.Scaffolding;

public static class LegacyScaffoldManifest
{
    public static IReadOnlyList<LegacyContextPlan> Plans { get; } =
    [
        new LegacyContextPlan
        {
            Source = LegacyDataSource.Szaipa,
            LegacyContextName = "SzaipaEntities",
            NewContextName = "SzaipaLegacyReadContext",
            OutputDirectory = "Contexts/Szaipa",
            Notes = "Public site domain data from Model1.edmx. Start with read-only tables used by Home, Artist, Publication, Search, and Staff flows."
        },
        new LegacyContextPlan
        {
            Source = LegacyDataSource.Tongou,
            LegacyContextName = "TongouEntities",
            NewContextName = "TongouLegacyReadContext",
            OutputDirectory = "Contexts/Tongou",
            Notes = "Project and Tongou data from Model2.edmx. Keep isolated from the public site context during the first migration phase."
        }
    ];
}
