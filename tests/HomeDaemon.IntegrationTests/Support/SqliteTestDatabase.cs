using HomeDaemon.DataAccess.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.IntegrationTests.Support;

/// <summary>
/// A real SQLite database held in memory for the lifetime of the test. The connection stays
/// open so the shared in-memory database is not discarded, and the schema is created by
/// running the production migrations.
/// </summary>
public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDatabase(SqliteConnection connection, HomeDaemonDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    public HomeDaemonDbContext Context { get; }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HomeDaemonDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new HomeDaemonDbContext(options);
        await context.Database.MigrateAsync();

        return new SqliteTestDatabase(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
