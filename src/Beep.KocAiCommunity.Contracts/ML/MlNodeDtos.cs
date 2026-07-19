namespace Beep.KocAiCommunity.Contracts.ML;

public sealed record NodeParameterDto(string Name, string DisplayName, string Type, bool Required, string? Default, IReadOnlyList<string>? Options);

public sealed record NodeDescriptorDto(
    string Kind, string Category, string DisplayName, string Description, string Input, string Output, IReadOnlyList<NodeParameterDto> Parameters);

public sealed record ParameterValidationDto(bool IsValid, IReadOnlyList<string> Errors);

public sealed record MlTaskDto(string Key, string? Task, string DisplayName, string PrimaryMetric, string SecondaryMetric, bool Supported, string OgExample);

public sealed record FeaturizationCheckDto(bool Ok, IReadOnlyList<string> Violations);
