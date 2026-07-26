namespace Beep.KocAiCommunity.Contracts.ML;

/// <summary>One lookup choice, carried as an object: stored <paramref name="Value"/> + human
/// <paramref name="Label"/>, optionally tagged with the contexts (e.g. ML task) it applies to.</summary>
public sealed record LookupOptionDto(string Value, string Label, IReadOnlyList<string>? AppliesTo);

/// <summary>The wire shape of one node parameter — the single contract the property panel binds to.</summary>
public sealed record NodeParameterDto(
    string Name, string DisplayName, string Type, bool Required, string? Default,
    IReadOnlyList<LookupOptionDto>? Options, double? Min, double? Max, string? Help);

public sealed record NodeDescriptorDto(
    string Kind, string Category, string DisplayName, string Description, string Input, string Output, IReadOnlyList<NodeParameterDto> Parameters);

public sealed record ParameterValidationDto(bool IsValid, IReadOnlyList<string> Errors);

public sealed record MlTaskDto(string Key, string? Task, string DisplayName, string PrimaryMetric, string SecondaryMetric, bool Supported, string OgExample);

public sealed record FeaturizationCheckDto(bool Ok, IReadOnlyList<string> Violations);
