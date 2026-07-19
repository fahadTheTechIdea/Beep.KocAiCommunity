using Beep.KocAiCommunity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Applies the entire SQLite migration chain (every phase's migration, in order) to a fresh database
/// and confirms the resulting schema is queryable. Catches ordering/consistency breaks between
/// migrations that EnsureCreated (used elsewhere) would silently paper over.
/// </summary>
public class MigrationChainTests
{
    [Fact]
    public void All_sqlite_migrations_apply_cleanly()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<KocDbContext>().UseSqlite(connection).Options;

        using var db = new KocDbContext(options);
        db.Database.Migrate();

        // No pending migrations remain, and the newest-phase tables are present and queryable.
        db.Database.GetPendingMigrations().Should().BeEmpty();
        db.Set<Beep.KocAiCommunity.Domain.Datasets.DatasetVersion>().Count().Should().Be(0);
        db.Set<Beep.KocAiCommunity.Domain.Admin.FeatureFlag>().Count().Should().Be(0);
        db.Set<Beep.KocAiCommunity.Domain.Studio.WorkflowVersion>().Count().Should().Be(0);
    }
}
