namespace Szaipa.Web.Models;

public sealed class LegacySourceStateModel
{
    public required string AccessMode { get; init; }

    public required string ConnectionStringSource { get; init; }

    public required bool HasConnectionString { get; init; }

    public required bool ScaffoldReady { get; init; }
}
