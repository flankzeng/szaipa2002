using System.Collections.Concurrent;

namespace Szaipa.Data.Services.Home;

/// <summary>
/// Remembers, per additive table (OrgMember / SaleWork / Partner), that a read just failed with "table/column
/// does not exist", so the public pages skip that query for <see cref="RetryAfter"/> instead of hitting the
/// database (and logging an EF error) on every request until the operator runs the SQL script. Registered as a
/// singleton; tests construct their own instance, so nothing leaks between tests.
/// </summary>
public sealed class HomeBlocksSchemaState
{
    /// <summary>How long a table stays marked missing before the next probe.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _missingUntil = new(StringComparer.Ordinal);
    private readonly Func<DateTimeOffset> _now;

    public HomeBlocksSchemaState()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    public HomeBlocksSchemaState(Func<DateTimeOffset> now)
    {
        _now = now;
    }

    public bool ShouldSkip(string table) =>
        _missingUntil.TryGetValue(table, out var until) && _now() < until;

    public void MarkMissing(string table) => _missingUntil[table] = _now() + RetryAfter;
}
