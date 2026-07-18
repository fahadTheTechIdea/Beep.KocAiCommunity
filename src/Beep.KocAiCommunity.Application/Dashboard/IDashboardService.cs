using Beep.KocAiCommunity.Contracts.Dashboard;

namespace Beep.KocAiCommunity.Application.Dashboard;

public interface IDashboardService
{
    Task<PersonalDashboardDto> GetPersonalAsync(string userId, CancellationToken ct = default);
}
