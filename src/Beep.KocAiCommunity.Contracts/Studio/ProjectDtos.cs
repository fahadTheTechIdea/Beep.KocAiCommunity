namespace Beep.KocAiCommunity.Contracts.Studio;

public sealed record CreateProjectRequest(string Name, Guid? CompetitionId);

public sealed record SaveProjectRequest(string DefinitionJson, string LabelColumn, string TaskType);

public sealed record ProjectDto(
    Guid Id,
    string Name,
    Guid? CompetitionId,
    string? CompetitionTitle,
    string TaskType,
    string LabelColumn,
    DateTime UpdatedUtc);

public sealed record ProjectDetailDto(
    Guid Id,
    string Name,
    Guid? CompetitionId,
    string? CompetitionTitle,
    string TaskType,
    string LabelColumn,
    string DefinitionJson);
