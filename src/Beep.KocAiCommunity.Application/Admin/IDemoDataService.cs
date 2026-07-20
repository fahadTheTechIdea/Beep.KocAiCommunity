namespace Beep.KocAiCommunity.Application.Admin;

/// <summary>What demo content is currently present.</summary>
public sealed record DemoDataStatus(bool Seeded, int Users, int Competitions, int Discussions, int Datasets);

/// <summary>
/// Seeds and removes a self-contained demo of the platform — people, engagement, a competition, a
/// discussion, and a dataset — so a fresh install can be explored immediately.
/// Every demo record is owned by a <c>demo-*</c> user (or lives under the <c>/demo</c> org path), which
/// is exactly what unseed removes, so real KOC data is never touched.
/// </summary>
public interface IDemoDataService
{
    Task<DemoDataStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Creates the demo content. No-op (returns current status) when already seeded.</summary>
    Task<DemoDataStatus> SeedAsync(string actorUserId, CancellationToken ct = default);

    /// <summary>Removes every demo record. Safe to run when nothing is seeded.</summary>
    Task<DemoDataStatus> UnseedAsync(string actorUserId, CancellationToken ct = default);
}
