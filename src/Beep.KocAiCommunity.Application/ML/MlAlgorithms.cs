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
    public const string Anomaly = "AnomalyDetection";

    /// <summary>Every trainer, as lookup options tagged with the tasks it supports.</summary>
    public static readonly IReadOnlyList<LookupOption> All =
    [
        new("sdca", "SDCA (linear)", [Binary, Multiclass, Regression]),
        new("lbfgs", "L-BFGS (linear)", [Binary, Multiclass, Regression]),
        new("fasttree", "FastTree (boosted trees)", [Binary, Regression]),
        new("fastforest", "FastForest (bagged trees)", [Binary, Regression]),
        new("gam", "GAM (interpretable)", [Binary, Regression]),
        new("perceptron", "Averaged Perceptron", [Binary]),
        new("sgd", "SGD (calibrated)", [Binary]),
        new("ogd", "Online gradient descent", [Regression]),
        new("naivebayes", "Naive Bayes", [Multiclass]),
        new("ova-fasttree", "One-vs-all (FastTree)", [Multiclass]),
        // Anomaly detection is unsupervised: the only trainer is RandomizedPCA, which learns the "normal"
        // subspace and scores a row by its reconstruction error. Tagged so it is the sole option the panel
        // offers once the task is Anomaly detection — and so the supervised hyperparameters hide themselves.
        new("randomized-pca", "Randomized PCA (reconstruction error)", [Anomaly]),
    ];
}
