namespace Beep.KocAiCommunity.Application.ML;

/// <summary>
/// The single source of truth for the trainer lookup — the list the <c>algorithm</c> parameter hands the
/// property panel as objects (stored value + friendly label + the tasks it supports). Both the node
/// descriptors and any task-aware UI read from here, so there is exactly one place that knows which
/// trainers exist and where each applies.
/// </summary>
public static class MlAlgorithms
{
    /// <summary>The ML task ids an algorithm can be tagged for (match the workflow's task values).</summary>
    public const string Binary = "BinaryClassification";
    public const string Multiclass = "MulticlassClassification";
    public const string Regression = "Regression";

    /// <summary>Every trainer, as lookup options tagged with the tasks it supports.</summary>
    public static readonly IReadOnlyList<LookupOption> All =
    [
        new("sdca", "SDCA (linear)", [Binary, Multiclass, Regression]),
        new("lbfgs", "L-BFGS (linear)", [Binary, Multiclass, Regression]),
        new("fasttree", "FastTree (boosted trees)", [Binary, Regression]),
        new("fastforest", "FastForest (bagged trees)", [Binary, Regression]),
        new("perceptron", "Averaged Perceptron", [Binary]),
        new("naivebayes", "Naive Bayes", [Multiclass]),
    ];
}
