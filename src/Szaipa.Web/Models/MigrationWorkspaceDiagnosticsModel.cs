namespace Szaipa.Web.Models;

public sealed class MigrationWorkspaceDiagnosticsModel
{
    public required bool HasReadWriteLegacySource { get; init; }

    public required IReadOnlyDictionary<string, LegacySourceStateModel> LegacySources { get; init; }

    public required IReadOnlyDictionary<string, bool> ReadModelFlags { get; init; }
}
