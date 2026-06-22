using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.Tongou;

namespace Szaipa.Data.Tests;

/// <summary>
/// Builds the read-only contexts over a fresh SQLite in-memory database (same table/column mapping as the
/// legacy schema). Seeding uses raw SQL because the contexts block <c>SaveChanges</c>.
/// </summary>
internal static class TestDb
{
    public static ContextFixture<SzaipaLegacyReadContext> Szaipa() =>
        Open<SzaipaLegacyReadContext>(options => new SzaipaLegacyReadContext(options));

    public static ContextFixture<TongouLegacyReadContext> Tongou() =>
        Open<TongouLegacyReadContext>(options => new TongouLegacyReadContext(options));

    private static ContextFixture<TContext> Open<TContext>(
        Func<DbContextOptions<TContext>, TContext> factory)
        where TContext : DbContext
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(connection)
            .Options;

        var context = factory(options);
        context.Database.EnsureCreated();
        return new ContextFixture<TContext>(context, connection);
    }
}

internal sealed class ContextFixture<TContext> : IAsyncDisposable
    where TContext : DbContext
{
    private readonly SqliteConnection _connection;

    public ContextFixture(TContext context, SqliteConnection connection)
    {
        Context = context;
        _connection = connection;
    }

    public TContext Context { get; }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        _connection.Dispose();
    }
}
