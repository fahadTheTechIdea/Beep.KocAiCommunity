using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The leaderboard rank recompute reads every entry and rewrites ranks, so two submissions to the same
/// competition can race. These lock in that a <see cref="LeaderboardEntry"/> update is optimistic-
/// concurrency-guarded (the RowVersion token is stamped by <see cref="KocDbContext"/> and enforced on
/// both providers), so a lost update surfaces as a conflict instead of silently overwriting.
/// </summary>
public class LeaderboardConcurrencyTests
{
    [Fact]
    public async Task Concurrent_update_of_the_same_entry_conflicts()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<KocDbContext>().UseSqlite(connection).Options;

        var entryId = await SeedEntryAsync(options);

        // Two contexts load the same entry. The first write wins; the second holds a stale RowVersion.
        using var first = new KocDbContext(options);
        using var second = new KocDbContext(options);
        var a = await first.Set<LeaderboardEntry>().FirstAsync(e => e.Id == entryId);
        var b = await second.Set<LeaderboardEntry>().FirstAsync(e => e.Id == entryId);

        a.Score = 2;
        await first.SaveChangesAsync();

        b.Score = 3;
        await second.Invoking(x => x.SaveChangesAsync())
            .Should().ThrowAsync<DbUpdateConcurrencyException>("the stale write must be detected, not silently lost");
    }

    [Fact]
    public async Task Stamped_row_version_changes_on_every_update()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<KocDbContext>().UseSqlite(connection).Options;

        var entryId = await SeedEntryAsync(options);

        using var db = new KocDbContext(options);
        var entry = await db.Set<LeaderboardEntry>().FirstAsync(e => e.Id == entryId);
        var before = entry.RowVersion!;

        entry.Score = 9;
        await db.SaveChangesAsync();

        entry.RowVersion.Should().NotBeNull();
        entry.RowVersion.Should().NotEqual(before, "each save stamps a fresh token");
    }

    private static async Task<Guid> SeedEntryAsync(DbContextOptions<KocDbContext> options)
    {
        using var db = new KocDbContext(options);
        db.Database.EnsureCreated();

        var competition = new Competition { Title = "T", Description = "D", ScorerCode = "accuracy", CreatedUtc = DateTime.UtcNow };
        db.Set<Competition>().Add(competition);
        var entry = new LeaderboardEntry
        {
            CompetitionId = competition.Id,
            SubmitterUserId = "u1",
            Score = 1,
            Rank = 1,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<LeaderboardEntry>().Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }
}
