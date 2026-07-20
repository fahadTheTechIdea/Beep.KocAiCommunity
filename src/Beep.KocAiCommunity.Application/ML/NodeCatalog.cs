namespace Beep.KocAiCommunity.Application.ML;

/// <summary>The kind of value flowing along a node port.</summary>
public enum PortKind { None, Table, Model, Metrics }

/// <summary>The editor type for a node parameter.</summary>
public enum NodeParameterType { Text, Number, Select, Columns }

/// <summary>A typed, declared parameter of a node.</summary>
public sealed record NodeParameter(
    string Name, string DisplayName, NodeParameterType Type, bool Required, string? Default, IReadOnlyList<string>? Options);

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
