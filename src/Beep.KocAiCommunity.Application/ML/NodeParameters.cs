using System.Globalization;

namespace Beep.KocAiCommunity.Application.ML;

/// <summary>
/// One strongly-typed parameter field on a node's parameter class — the common base every field type shares.
/// It carries its own metadata (name, label, value <see cref="Type"/>, default, lookup options, bounds, help)
/// and its current <see cref="Value"/>, and knows how to read/write itself from a node's string config. The
/// property panel renders and get/sets any field through this one shape, while each node still declares its
/// own distinct set of fields via its own parameter class.
/// </summary>
public abstract class ParamField
{
    protected ParamField(
        string name, string displayName, NodeParameterType type,
        bool required = false, string? @default = null,
        IReadOnlyList<LookupOption>? options = null,
        double? min = null, double? max = null, string? help = null)
    {
        Name = name;
        DisplayName = displayName;
        Type = type;
        Required = required;
        Default = @default;
        Options = options;
        Min = min;
        Max = max;
        Help = help;
    }

    public string Name { get; }
    public string DisplayName { get; }
    public NodeParameterType Type { get; }
    public bool Required { get; }
    public string? Default { get; }
    public IReadOnlyList<LookupOption>? Options { get; }
    public double? Min { get; }
    public double? Max { get; }
    public string? Help { get; }

    /// <summary>The current raw value (as persisted in the node's string config). Null/blank means unset.</summary>
    public string? Value { get; set; }

    /// <summary>The value if set, else the default.</summary>
    public string? Effective => string.IsNullOrWhiteSpace(Value) ? Default : Value;

    /// <summary>Project this field to the wire/catalog parameter shape the panel and API consume.</summary>
    public NodeParameter ToDescriptor() => new(Name, DisplayName, Type, Required, Default, Options, Min, Max, Help);

    internal static string? Num(double? v) => v?.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Free-text field.</summary>
public sealed class TextParam(string name, string displayName, bool required = false, string? @default = null, string? help = null)
    : ParamField(name, displayName, NodeParameterType.Text, required, @default, help: help);

/// <summary>A comma-separated list of column names.</summary>
public sealed class ColumnsParam(string name, string displayName, bool required = false, string? help = null)
    : ParamField(name, displayName, NodeParameterType.Columns, required, help: help);

/// <summary>A single column name.</summary>
public sealed class ColumnParam(string name, string displayName, bool required = false, string? help = null)
    : ParamField(name, displayName, NodeParameterType.Column, required, help: help);

/// <summary>A dataset chosen from the caller's visible datasets (value is the dataset id).</summary>
public sealed class DatasetParam(string name, string displayName, bool required = false, string? help = null)
    : ParamField(name, displayName, NodeParameterType.Dataset, required, help: help);

/// <summary>A whole-number field with optional bounds.</summary>
public sealed class IntParam(string name, string displayName, int? @default = null, int? min = null, int? max = null, bool required = false, string? help = null)
    : ParamField(name, displayName, NodeParameterType.Integer, required, @default?.ToString(CultureInfo.InvariantCulture), min: min, max: max, help: help)
{
    public int? Get() => int.TryParse(Effective, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
}

/// <summary>A decimal-number field with optional bounds.</summary>
public sealed class NumberParam(string name, string displayName, double? @default = null, double? min = null, double? max = null, bool required = false, string? help = null)
    : ParamField(name, displayName, NodeParameterType.Number, required, Num(@default), min: min, max: max, help: help)
{
    public double? Get() => double.TryParse(Effective, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
}

/// <summary>A true/false toggle.</summary>
public sealed class BoolParam(string name, string displayName, bool @default = false, string? help = null)
    : ParamField(name, displayName, NodeParameterType.Boolean, false, @default ? "true" : "false", help: help)
{
    public bool Get() => bool.TryParse(Effective, out var v) && v;
}

/// <summary>A calendar-date field.</summary>
public sealed class DateParam(string name, string displayName, bool required = false, string? help = null)
    : ParamField(name, displayName, NodeParameterType.Date, required, help: help);

/// <summary>A lookup — one value chosen from a list carried as objects (value + label).</summary>
public sealed class LookupParam(string name, string displayName, string @default, IReadOnlyList<LookupOption> options, string? help = null)
    : ParamField(name, displayName, NodeParameterType.Select, false, @default, options, help: help);

/// <summary>
/// The base a node's parameter class derives from. Each concrete class (e.g. <c>TrainParameters</c>) declares
/// its own strongly-typed fields and lists them in <see cref="Declare"/>. The base turns them into the panel/
/// API descriptor and bridges the node's string config to and from the typed fields — so every node keeps its
/// own distinct parameters while the panel and persistence path stay uniform.
/// </summary>
public abstract class NodeParameters
{
    private IReadOnlyList<ParamField>? _fields;

    /// <summary>The node's fields, in display order.</summary>
    protected abstract IReadOnlyList<ParamField> Declare();

    public IReadOnlyList<ParamField> Fields => _fields ??= Declare();

    /// <summary>The catalog/panel parameter list for this node.</summary>
    public IReadOnlyList<NodeParameter> Describe() => [.. Fields.Select(f => f.ToDescriptor())];

    /// <summary>Load field values from a node's persisted config.</summary>
    public void Load(IReadOnlyDictionary<string, string>? config)
    {
        foreach (var f in Fields)
        {
            f.Value = config is not null && config.TryGetValue(f.Name, out var v) ? v : null;
        }
    }

    /// <summary>Write the set field values back to a config dictionary (blank/unset keys are omitted).</summary>
    public IDictionary<string, string> Save()
    {
        var config = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)))
        {
            config[f.Name] = f.Value!.Trim();
        }

        return config;
    }
}
