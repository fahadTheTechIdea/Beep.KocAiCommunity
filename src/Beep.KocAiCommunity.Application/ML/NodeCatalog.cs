namespace Beep.KocAiCommunity.Application.ML;

/// <summary>The kind of value flowing along a node port.</summary>
public enum PortKind { None, Table, Model, Metrics }

/// <summary>
/// The value type of a node parameter — the single switch the property panel renders on. Every editor
/// (text box, numeric field, date picker, checkbox, lookup dropdown, column picker, dataset picker) maps to
/// one of these, so the panel treats every parameter of every node identically.
/// </summary>
public enum NodeParameterType
{
    /// <summary>Free text.</summary>
    Text,

    /// <summary>A decimal number.</summary>
    Number,

    /// <summary>A whole number.</summary>
    Integer,

    /// <summary>A calendar date.</summary>
    Date,

    /// <summary>A true/false toggle.</summary>
    Boolean,

    /// <summary>A lookup — one value chosen from <see cref="NodeParameter.Options"/>.</summary>
    Select,

    /// <summary>A comma-separated list of column names.</summary>
    Columns,

    /// <summary>A single column name.</summary>
    Column,

    /// <summary>A dataset chosen from the caller's visible datasets (value is the dataset id).</summary>
    Dataset,
}

/// <summary>
/// One choice for a <see cref="NodeParameterType.Select"/> lookup, carried as an object (stored
/// <see cref="Value"/> + human <see cref="Label"/>) rather than a bare string, so the panel can show a
/// friendly label while persisting a stable value. <see cref="AppliesTo"/> optionally tags the contexts the
/// option is valid in (e.g. the ML task); when set, the panel shows the option only when the active context
/// matches — the one generic mechanism that replaces per-field filtering logic.
/// </summary>
public sealed record LookupOption(string Value, string Label, IReadOnlyList<string>? AppliesTo = null);

/// <summary>
/// A typed, declared parameter of a node — the single contract the property panel binds to for get/set,
/// regardless of node kind. It carries everything the editor needs: the value <see cref="Type"/>, the
/// <see cref="Default"/>, whether it is <see cref="Required"/>, the lookup <see cref="Options"/> (as objects)
/// for a <see cref="NodeParameterType.Select"/>, numeric <see cref="Min"/>/<see cref="Max"/> bounds, and
/// optional <see cref="Help"/> text.
/// </summary>
public sealed record NodeParameter(
    string Name,
    string DisplayName,
    NodeParameterType Type,
    bool Required = false,
    string? Default = null,
    IReadOnlyList<LookupOption>? Options = null,
    double? Min = null,
    double? Max = null,
    string? Help = null);

/// <summary>
/// A code-first description of one pipeline node kind: its category, ports, and typed parameters.
/// The <see cref="Kind"/> matches the workflow compiler's known kinds so a graph built from these nodes
/// validates and executes.
/// </summary>
public sealed record NodeDescriptor(
    string Kind, string Category, string DisplayName, string Description,
    PortKind Input, PortKind Output, IReadOnlyList<NodeParameter> Parameters);

/// <summary>The outcome of validating a node's parameters.</summary>
public sealed record ParameterValidation(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>The backend node catalog: descriptors, lookup, and parameter validation.</summary>
public interface INodeRegistry
{
    IReadOnlyList<NodeDescriptor> All { get; }
    IReadOnlyList<string> Categories { get; }
    NodeDescriptor? Find(string kind);
    ParameterValidation ValidateParameters(string kind, IReadOnlyDictionary<string, string> config);
}
