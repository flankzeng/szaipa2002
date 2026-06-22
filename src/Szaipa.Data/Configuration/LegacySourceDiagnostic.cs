namespace Szaipa.Data.Configuration;

public sealed class LegacySourceDiagnostic
{
    public required LegacyAccessMode AccessMode { get; init; }

    public required bool HasConnectionString { get; init; }

    public required string ConnectionStringSource { get; init; }
}
