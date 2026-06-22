using Szaipa.Data.Configuration;

namespace Szaipa.Data.Abstractions;

public interface ILegacyConnectionPolicy
{
    IReadOnlyDictionary<LegacyDataSource, LegacyDatabaseOptions> GetConfiguredSources();

    IReadOnlyDictionary<LegacyDataSource, LegacySourceDiagnostic> GetSourceDiagnostics();

    string GetConnectionString(LegacyDataSource source, bool requireWrite = false);
}
