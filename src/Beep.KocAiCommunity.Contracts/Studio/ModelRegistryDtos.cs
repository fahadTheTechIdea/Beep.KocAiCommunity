namespace Beep.KocAiCommunity.Contracts.Studio;

public sealed record RegisterModelRequest(string ModelName, Guid SourceRunId);

public sealed record ModelVersionDto(
    Guid Id, string SemVer, string Status, string MetricName, double MetricValue, int Approvals, string RegisteredByUserId);

public sealed record RegisteredModelDto(
    Guid Id, string Name, string OwnerUserId, IReadOnlyList<ModelVersionDto> Versions);

public sealed record DeploymentDto(
    Guid Id, Guid ModelVersionId, string ModelName, string SemVer, string Environment,
    string Status, string MetricName, double MetricValue, DateTime DeployedUtc, string DeployedByUserId);
