namespace Beep.KocAiCommunity.Contracts.Studio;

public sealed record ModelRunDto(
    Guid Id,
    string DatasetName,
    string LabelColumn,
    string Task,
    string Algorithm,
    string PrimaryMetric,
    double PrimaryValue,
    string SecondaryMetric,
    double SecondaryValue,
    long RowCount,
    DateTime CompletedUtc);
