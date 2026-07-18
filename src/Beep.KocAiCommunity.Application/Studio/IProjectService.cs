using Beep.KocAiCommunity.Domain.Studio;

namespace Beep.KocAiCommunity.Application.Studio;

public sealed class ProjectException(string message) : Exception(message);

public interface IProjectService
{
    Task<Project> CreateAsync(string userId, string name, Guid? competitionId, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListMineAsync(string userId, CancellationToken ct = default);
    Task<Project?> GetAsync(string userId, Guid projectId, CancellationToken ct = default);
    Task SaveDefinitionAsync(string userId, Guid projectId, string definitionJson, string labelColumn, string taskType, CancellationToken ct = default);
    Task DeleteAsync(string userId, Guid projectId, CancellationToken ct = default);
}
