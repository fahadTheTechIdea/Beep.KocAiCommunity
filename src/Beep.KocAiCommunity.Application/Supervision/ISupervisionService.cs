using Beep.KocAiCommunity.Contracts.Supervision;

namespace Beep.KocAiCommunity.Application.Supervision;

/// <summary>
/// Read-only participation rollups for supervisors, scoped to the org subtree they lead
/// (Team Leader → Team, Manager → Group, DCEO → Directorate, CEO → Company).
/// </summary>
public interface ISupervisionService
{
    Task<SupervisionRollupDto> GetRollupAsync(string userId, CancellationToken ct = default);
}
