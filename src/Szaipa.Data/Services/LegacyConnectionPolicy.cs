using Microsoft.Extensions.Options;
using Szaipa.Data.Abstractions;
using Szaipa.Data.Configuration;

namespace Szaipa.Data.Services;

public sealed class LegacyConnectionPolicy : ILegacyConnectionPolicy
{
    private readonly LegacyDataOptions _options;

    public LegacyConnectionPolicy(IOptions<LegacyDataOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyDictionary<LegacyDataSource, LegacyDatabaseOptions> GetConfiguredSources()
    {
        return new Dictionary<LegacyDataSource, LegacyDatabaseOptions>
        {
            [LegacyDataSource.Szaipa] = _options.Szaipa,
            [LegacyDataSource.Tongou] = _options.Tongou
        };
    }

    public IReadOnlyDictionary<LegacyDataSource, LegacySourceDiagnostic> GetSourceDiagnostics()
    {
        return new Dictionary<LegacyDataSource, LegacySourceDiagnostic>
        {
            [LegacyDataSource.Szaipa] = CreateDiagnostic(_options.Szaipa),
            [LegacyDataSource.Tongou] = CreateDiagnostic(_options.Tongou)
        };
    }

    public string GetConnectionString(LegacyDataSource source, bool requireWrite = false)
    {
        var selected = source switch
        {
            LegacyDataSource.Szaipa => _options.Szaipa,
            LegacyDataSource.Tongou => _options.Tongou,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown legacy data source.")
        };

        if (selected.AccessMode == LegacyAccessMode.Disabled)
        {
            throw new InvalidOperationException(
                $"Legacy data source '{source}' is disabled. Enable it explicitly before use.");
        }

        if (requireWrite && selected.AccessMode != LegacyAccessMode.ReadWrite)
        {
            throw new InvalidOperationException(
                $"Legacy data source '{source}' is not configured for write access.");
        }

        if (string.IsNullOrWhiteSpace(selected.ConnectionString))
        {
            throw new InvalidOperationException(
                $"Legacy data source '{source}' does not have a configured connection string.");
        }

        return selected.ConnectionString;
    }

    private static LegacySourceDiagnostic CreateDiagnostic(LegacyDatabaseOptions options)
    {
        return new LegacySourceDiagnostic
        {
            AccessMode = options.AccessMode,
            HasConnectionString = !string.IsNullOrWhiteSpace(options.ConnectionString),
            ConnectionStringSource = options.ConnectionStringSource
        };
    }
}
