namespace Beep.KocAiCommunity.Application.Admin;

/// <summary>What demo content is currently present.</summary>
public sealed record DemoDataStatus(bool Seeded, int Users, int Submissions, int Discussions, int Datasets);

/// <summary>
/// Seeds and removes a self-contained demo of the platform — colleagues with engagement, their
/// submissions and standings, discussions, and datasets — so a fresh install can be explored
/// immediately.
/// <para>
/// It adds people and what people do, never platform content: the competitions they enter, the tracks
/// they follow and the badges they earn ship with the product and are seeded when the database is
/// migrated. The demo enters those competitions rather than inventing its own.
/// </para>
/// <para>
/// Every demo record is owned by a <c>demo-*</c> user (or lives under the <c>/demo</c> org path), which
/// is exactly what unseed removes, so real KOC data is never touched.
/// </para>
/// </summary>
public interface IDemoDataService
{
    Task<DemoDataStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Creates the demo content. No-op (returns current status) when already seeded.</summary>
    Task<DemoDataStatus> SeedAsync(string actorUserId, CancellationToken ct = default);

    /// <summary>Removes every demo record. Safe to run when nothing is seeded.</summary>
    Task<DemoDataStatus> UnseedAsync(string actorUserId, CancellationToken ct = default);
}
