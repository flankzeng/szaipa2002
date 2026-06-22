using Szaipa.Web.Models;

namespace Szaipa.Web.Services;

public static class PageSkeletonFactory
{
    public static PageSkeletonViewModel Create(PageSkeletonDefinition definition)
    {
        return new PageSkeletonViewModel
        {
            Title = definition.Title,
            Eyebrow = definition.Eyebrow,
            Lead = definition.Lead,
            LegacyRoute = definition.LegacyRoute,
            TargetRepository = definition.TargetRepository,
            TargetBatch = definition.TargetBatch,
            Sections = definition.Sections,
            AssetDependencies = definition.AssetDependencies,
            QueryParameters = definition.QueryParameters,
            Guardrails = definition.Guardrails,
            TargetReadModels = definition.TargetReadModels,
            RemovedWriteBehaviors = definition.RemovedWriteBehaviors,
            Blueprint = definition.Blueprint,
            NextSteps = definition.NextSteps
        };
    }
}
