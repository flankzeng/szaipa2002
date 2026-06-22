using Szaipa.Web.Models;

namespace Szaipa.Web.Services;

public static class MigrationStubFactory
{
    public static MigrationStubPageViewModel Create(MigrationStubDefinition definition)
    {
        return new MigrationStubPageViewModel
        {
            Title = definition.Title,
            LegacyRoute = definition.LegacyRoute,
            TargetRepository = definition.TargetRepository,
            TargetBatch = definition.TargetBatch,
            NextSteps = definition.NextSteps
        };
    }
}
