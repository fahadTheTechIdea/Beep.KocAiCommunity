using Beep.KocAiCommunity.Domain.Engagement;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Engagement;

/// <summary>Seeds the badge catalog from <see cref="BadgeCatalog"/> (idempotent, per-badge).</summary>
public static class EngagementSeeder
{
    public static async Task SeedBadgesAsync(KocDbContext db, CancellationToken ct = default)
    {
        var existing = await db.Badges.Select(b => b.Code).ToListAsync(ct);
        var have = existing.ToHashSet(StringComparer.Ordinal);

        var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var added = false;

        foreach (var def in BadgeCatalog.All)
        {
            if (have.Contains(def.Code))
            {
                continue;
            }

            db.Badges.Add(new Badge
            {
                Code = def.Code,
                Name = def.Name,
                Description = def.Description,
                IconFile = def.IconFile,
                Tier = def.Tier,
                CreatedUtc = stamp,
            });
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
