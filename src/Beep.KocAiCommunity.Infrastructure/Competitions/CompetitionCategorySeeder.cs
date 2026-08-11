using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Competitions;

/// <summary>
/// Seeds the starting competition categories — the KOC operational domains, so a challenge's category
/// says whose problem it is rather than which algorithm it happens to use.
/// <para>
/// Idempotent <b>per category</b>: a missing one is added even when others already exist, so a later
/// release can introduce a category without a database reset. Existing rows are never overwritten —
/// once an administrator has renamed or disabled a category, that is their decision to keep.
/// </para>
/// </summary>
public static class CompetitionCategorySeeder
{
    /// <summary>The starting set. Order is the order they appear in the arena's filter row.</summary>
    private static readonly (string Code, string Name, string Description, string Icon)[] Starters =
    [
        ("subsurface", "Subsurface", "Reservoir characterisation, geology, petrophysics, and well logs.", "Layers"),
        ("drilling-wells", "Drilling & Wells", "Rate of penetration, non-productive time, and well integrity.", "Construction"),
        ("production", "Production", "Rates, decline, artificial lift, and production optimisation.", "OilBarrel"),
        ("facilities", "Facilities & Plant", "Process performance, energy use, and plant throughput.", "Factory"),
        ("hse", "HSE", "Incidents, near-misses, and abnormal sensor behaviour.", "HealthAndSafety"),
        ("maintenance", "Maintenance & Reliability", "Failure prediction, remaining useful life, and spares.", "Build"),
        ("medical", "Medical & Health", "Ahmadi Hospital: laboratory testing, diagnostics, patient flow, and occupational health.", "MedicalServices"),
        ("training", "Training & Development", "T&CD: course demand, attendance, completion, and time to competency.", "School"),
        ("people", "People & HR", "Workforce records, payroll integrity, resourcing, and absence planning.", "Groups"),
    ];

    public static async Task SeedAsync(KocDbContext db, CancellationToken ct = default)
    {
        var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var existing = await db.CompetitionCategories.Select(c => c.Code).ToListAsync(ct);

        for (var i = 0; i < Starters.Length; i++)
        {
            var (code, name, description, icon) = Starters[i];
            if (existing.Contains(code, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            db.CompetitionCategories.Add(new CompetitionCategory
            {
                Code = code,
                Name = name,
                Description = description,
                Icon = icon,
                IsEnabled = true,
                OrderNo = i,
                CreatedUtc = stamp,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
