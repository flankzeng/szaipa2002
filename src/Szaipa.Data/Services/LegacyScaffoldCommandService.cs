using Szaipa.Data.Abstractions;
using Szaipa.Data.Configuration;
using Szaipa.Data.Scaffolding;

namespace Szaipa.Data.Services;

public sealed class LegacyScaffoldCommandService : ILegacyScaffoldCommandService
{
    private readonly ILegacyConnectionPolicy _legacyConnectionPolicy;

    public LegacyScaffoldCommandService(ILegacyConnectionPolicy legacyConnectionPolicy)
    {
        _legacyConnectionPolicy = legacyConnectionPolicy;
    }

    public IReadOnlyList<LegacyScaffoldCommandSuggestion> GetSuggestions()
    {
        var configuredSources = _legacyConnectionPolicy.GetConfiguredSources();
        var suggestions = new List<LegacyScaffoldCommandSuggestion>();

        foreach (var plan in LegacyScaffoldManifest.Plans)
        {
            var sourceOptions = configuredSources[plan.Source];
            var envVarName = GetEnvironmentVariableName(plan.Source);

            suggestions.Add(new LegacyScaffoldCommandSuggestion
            {
                Source = plan.Source,
                ContextName = plan.NewContextName,
                AccessMode = sourceOptions.AccessMode.ToString(),
                IsReady = sourceOptions.AccessMode != LegacyAccessMode.Disabled
                    && !string.IsNullOrWhiteSpace(sourceOptions.ConnectionString),
                EnvironmentVariableName = envVarName,
                Command =
                    $"dotnet ef dbcontext scaffold \"${envVarName}\" Microsoft.EntityFrameworkCore.SqlServer " +
                    $"--project src/Szaipa.Data/Szaipa.Data.csproj --startup-project src/Szaipa.Web/Szaipa.Web.csproj " +
                    $"--context {plan.NewContextName} --output-dir {plan.OutputDirectory} --force --no-onconfiguring",
                Notes = plan.Notes
            });
        }

        return suggestions;
    }

    private static string GetEnvironmentVariableName(LegacyDataSource source)
    {
        return source switch
        {
            LegacyDataSource.Szaipa => "SZAIPA_READONLY_CONNECTION",
            LegacyDataSource.Tongou => "TONGOU_READONLY_CONNECTION",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown legacy data source.")
        };
    }
}
