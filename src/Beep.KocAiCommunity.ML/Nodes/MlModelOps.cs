using System.Globalization;
using Beep.KocAiCommunity.Application.Common;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Microsoft.ML;
using Microsoft.ML.Trainers;
using Microsoft.ML.Trainers.FastTree;

namespace Beep.KocAiCommunity.ML.Nodes;

/// <summary>
/// Model fitting, trainer selection, and prediction reading — moved verbatim from the monolithic
/// executor so the migrated node handlers behave identically.
/// </summary>
internal static class MlModelOps
{
    public static (ITransformer Model, string Algorithm, ITransformer? LabelMap) FitModel(
        MLContext ml, MlTaskType task, WorkflowNode node, string label, IDataView train, string[] features)
    {
        if (task == MlTaskType.AnomalyDetection)
        {
            // Unsupervised: RandomizedPCA learns the "normal" subspace and flags rows with a large
            // reconstruction error (a high Score). Rank must stay below the feature count or the
            // reconstruction is perfect and every score collapses to zero. Seeded for reproducibility.
            var rank = Math.Clamp(features.Length - 1, 1, 20);
            var pca = ml.Transforms.Concatenate("Features", features)
                .Append(ml.AnomalyDetection.Trainers.RandomizedPca(new RandomizedPcaTrainer.Options
                {
                    FeatureColumnName = "Features",
                    Rank = rank,
                    EnsureZeroMean = true,
                    Seed = 1,
                }));
            return (pca.Fit(train), $"RandomizedPca(rank {rank})", null);
        }

        if (task == MlTaskType.MulticlassClassification)
        {
            var (trainer, name) = MulticlassTrainer(ml, node);
            var labelMap = ml.Transforms.Conversion.MapValueToKey("Label", label).Fit(train);
            var keyed = labelMap.Transform(train);
            var model = ml.Transforms.Concatenate("Features", features)
                .Append(trainer)
                .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabelValue", "PredictedLabel"))
                .Fit(keyed);
            return (model, name, labelMap);
        }

        var (t, n) = Trainer(ml, task, node, label);
        var p = ml.Transforms.Concatenate("Features", features).Append(t);
        return (p.Fit(train), n, null);
    }

    // Optional hyperparameters read from the node config. HpInt/HpFloat treat 0/blank as "unset".
    private static int MinLeaf(WorkflowNode node) => PipelineContext.HpInt(node, "minLeaf", 10);
    private static int? IntOrNull(WorkflowNode node, string key) =>
        int.TryParse(PipelineContext.Cfg(node, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;

    public static (IEstimator<ITransformer> Trainer, string Name) MulticlassTrainer(MLContext ml, WorkflowNode node)
    {
        const string label = "Label";
        const string features = "Features";
        var l1 = PipelineContext.HpFloat(node, "l1");
        var l2 = PipelineContext.HpFloat(node, "l2");
        var maxIter = IntOrNull(node, "maxIterations");
        return PipelineContext.Algo(node) switch
        {
            "lbfgs" => (ml.MulticlassClassification.Trainers.LbfgsMaximumEntropy(new LbfgsMaximumEntropyMulticlassTrainer.Options
            {
                LabelColumnName = label,
                FeatureColumnName = features,
                L1Regularization = l1 ?? 1f,
                L2Regularization = l2 ?? 1f,
                HistorySize = PipelineContext.HpInt(node, "historySize", 20),
            }), "LbfgsMaximumEntropy"),
            "naivebayes" => (ml.MulticlassClassification.Trainers.NaiveBayes(label, features), "NaiveBayes"),
            // One-vs-all lets a binary tree learner power multiclass (FastTree isn't natively multiclass).
            "ova-fasttree" => (ml.MulticlassClassification.Trainers.OneVersusAll(
                ml.BinaryClassification.Trainers.FastTree(new FastTreeBinaryTrainer.Options
                {
                    LabelColumnName = label,
                    FeatureColumnName = features,
                    NumberOfLeaves = PipelineContext.HpInt(node, "leaves", 20),
                    NumberOfTrees = PipelineContext.HpInt(node, "trees", 100),
                    MinimumExampleCountPerLeaf = MinLeaf(node),
                    LearningRate = PipelineContext.ReadDouble(PipelineContext.Cfg(node, "learningRate"), 0.2),
                    NumberOfThreads = 1,
                }), labelColumnName: label), "OneVersusAllFastTree"),
            _ => (ml.MulticlassClassification.Trainers.SdcaMaximumEntropy(new SdcaMaximumEntropyMulticlassTrainer.Options
            {
                LabelColumnName = label,
                FeatureColumnName = features,
                L1Regularization = l1,
                L2Regularization = l2,
                MaximumNumberOfIterations = maxIter,
            }), "SdcaMaximumEntropy"),
        };
    }

    public static (IEstimator<ITransformer> Trainer, string Name) Trainer(MLContext ml, MlTaskType task, WorkflowNode node, string label)
    {
        const string features = "Features";
        var algo = PipelineContext.Algo(node);
        var trees = PipelineContext.HpInt(node, "trees", 100);
        var leaves = PipelineContext.HpInt(node, "leaves", 20);
        var minLeaf = MinLeaf(node);
        var learningRate = PipelineContext.ReadDouble(PipelineContext.Cfg(node, "learningRate"), 0.2);
        var l1 = PipelineContext.HpFloat(node, "l1");
        var l2 = PipelineContext.HpFloat(node, "l2");
        var maxIter = IntOrNull(node, "maxIterations");
        var historySize = PipelineContext.HpInt(node, "historySize", 20);

        if (task == MlTaskType.Regression)
        {
            return algo switch
            {
                // NumberOfThreads = 1: the FastTree/FastForest native builders are otherwise multi-threaded
                // and their floating-point reductions vary run-to-run; pinning one thread makes the fitted
                // tree reproducible (the rest of the engine is already seeded).
                "fasttree" => (ml.Regression.Trainers.FastTree(new FastTreeRegressionTrainer.Options
                {
                    LabelColumnName = label,
                    FeatureColumnName = features,
                    NumberOfLeaves = leaves,
                    NumberOfTrees = trees,
                    MinimumExampleCountPerLeaf = minLeaf,
                    LearningRate = learningRate,
                    NumberOfThreads = 1,
                }), "FastTreeRegression"),
                "fastforest" => (ml.Regression.Trainers.FastForest(new FastForestRegressionTrainer.Options
                {
                    LabelColumnName = label,
                    FeatureColumnName = features,
                    NumberOfLeaves = leaves,
                    NumberOfTrees = trees,
                    MinimumExampleCountPerLeaf = minLeaf,
                    NumberOfThreads = 1,
                }), "FastForestRegression"),
                "gam" => (ml.Regression.Trainers.Gam(new GamRegressionTrainer.Options
                {
                    LabelColumnName = label,
                    FeatureColumnName = features,
                    NumberOfIterations = PipelineContext.HpInt(node, "iterations", 9500),
                    LearningRate = learningRate,
                    NumberOfThreads = 1,
                }), "GamRegression"),
                "ogd" => (ml.Regression.Trainers.OnlineGradientDescent(new OnlineGradientDescentTrainer.Options
                {
                    LabelColumnName = label,
                    FeatureColumnName = features,
                    NumberOfIterations = PipelineContext.HpInt(node, "iterations", 1),
                    LearningRate = (float)learningRate,
                }), "OnlineGradientDescent"),
                "lbfgs" => (ml.Regression.Trainers.LbfgsPoissonRegression(new LbfgsPoissonRegressionTrainer.Options
                {
                    LabelColumnName = label,
                    FeatureColumnName = features,
                    L1Regularization = l1 ?? 1f,
                    L2Regularization = l2 ?? 1f,
                    HistorySize = historySize,
                }), "LbfgsPoissonRegression"),
                _ => (ml.Regression.Trainers.Sdca(new SdcaRegressionTrainer.Options
                {
                    LabelColumnName = label,
                    FeatureColumnName = features,
                    L1Regularization = l1,
                    L2Regularization = l2,
                    MaximumNumberOfIterations = maxIter,
                }), "SdcaRegression"),
            };
        }

        return algo switch
        {
            "fasttree" => (ml.BinaryClassification.Trainers.FastTree(new FastTreeBinaryTrainer.Options
            {
                LabelColumnName = label,
                FeatureColumnName = features,
                NumberOfLeaves = leaves,
                NumberOfTrees = trees,
                MinimumExampleCountPerLeaf = minLeaf,
                LearningRate = learningRate,
                NumberOfThreads = 1,
            }), "FastTreeBinary"),
            "fastforest" => (ml.BinaryClassification.Trainers.FastForest(new FastForestBinaryTrainer.Options
            {
                LabelColumnName = label,
                FeatureColumnName = features,
                NumberOfLeaves = leaves,
                NumberOfTrees = trees,
                MinimumExampleCountPerLeaf = minLeaf,
                NumberOfThreads = 1,
            }), "FastForestBinary"),
            "gam" => (ml.BinaryClassification.Trainers.Gam(new GamBinaryTrainer.Options
            {
                LabelColumnName = label,
                FeatureColumnName = features,
                NumberOfIterations = PipelineContext.HpInt(node, "iterations", 9500),
                LearningRate = learningRate,
                NumberOfThreads = 1,
            }), "GamBinary"),
            "sgd" => (ml.BinaryClassification.Trainers.SgdCalibrated(new SgdCalibratedTrainer.Options
            {
                LabelColumnName = label,
                FeatureColumnName = features,
                NumberOfIterations = PipelineContext.HpInt(node, "iterations", 20),
                LearningRate = PipelineContext.ReadDouble(PipelineContext.Cfg(node, "learningRate"), 0.01),
                L2Regularization = l2 ?? 1e-6f,
            }), "SgdCalibrated"),
            "lbfgs" => (ml.BinaryClassification.Trainers.LbfgsLogisticRegression(new LbfgsLogisticRegressionBinaryTrainer.Options
            {
                LabelColumnName = label,
                FeatureColumnName = features,
                L1Regularization = l1 ?? 1f,
                L2Regularization = l2 ?? 1f,
                HistorySize = historySize,
            }), "LbfgsLogisticRegression"),
            "perceptron" => (ml.BinaryClassification.Trainers.AveragedPerceptron(new AveragedPerceptronTrainer.Options
            {
                LabelColumnName = label,
                FeatureColumnName = features,
                LearningRate = (float)PipelineContext.ReadDouble(PipelineContext.Cfg(node, "learningRate"), 1.0),
                NumberOfIterations = PipelineContext.HpInt(node, "iterations", 10),
                L2Regularization = l2 ?? 0f,
            }), "AveragedPerceptron"),
            _ => (ml.BinaryClassification.Trainers.SdcaLogisticRegression(new SdcaLogisticRegressionBinaryTrainer.Options
            {
                LabelColumnName = label,
                FeatureColumnName = features,
                L1Regularization = l1,
                L2Regularization = l2,
                MaximumNumberOfIterations = maxIter,
            }), "SdcaLogisticRegression"),
        };
    }

    public static List<string> ReadPredictions(IDataView scored, MlTaskType task, (string True, string False)? binaryTokens = null)
    {
        var list = new List<string>();
        // Regression emits a numeric Score; anomaly detection emits a continuous anomaly Score (higher =
        // more anomalous) that the AUC scorer ranks against the rare positive class.
        if (task is MlTaskType.Regression or MlTaskType.AnomalyDetection)
        {
            var col = scored.Schema["Score"];
            using var cursor = scored.GetRowCursor([col]);
            var getter = cursor.GetGetter<float>(col);
            float value = 0;
            while (cursor.MoveNext())
            {
                getter(ref value);
                list.Add(value.ToString(CultureInfo.InvariantCulture));
            }
        }
        else if (task == MlTaskType.MulticlassClassification)
        {
            var col = scored.Schema["PredictedLabelValue"];
            using var cursor = scored.GetRowCursor([col]);
            var getter = cursor.GetGetter<ReadOnlyMemory<char>>(col);
            ReadOnlyMemory<char> value = default;
            while (cursor.MoveNext())
            {
                getter(ref value);
                list.Add(value.ToString());
            }
        }
        else
        {
            var (t, f) = binaryTokens ?? ("true", "false");
            var col = scored.Schema["PredictedLabel"];
            using var cursor = scored.GetRowCursor([col]);
            var getter = cursor.GetGetter<bool>(col);
            bool value = false;
            while (cursor.MoveNext())
            {
                getter(ref value);
                list.Add(value ? t : f);
            }
        }
        return list;
    }

    /// <summary>
    /// The exact tokens the training label used for the true/false classes (e.g. <c>1</c>/<c>0</c> or
    /// <c>Yes</c>/<c>No</c>), so a binary submission echoes the competition's own convention instead of a
    /// hardcoded <c>true</c>/<c>false</c>. Returns null when the labels aren't a recognisable boolean pair
    /// (both classes not present, or non-standard tokens) — callers then fall back to <c>true</c>/<c>false</c>.
    /// </summary>
    public static (string True, string False)? BinaryLabelTokens(string trainingCsvPath, string labelColumn)
    {
        string? trueToken = null;
        string? falseToken = null;
        foreach (var raw in ReadColumn(trainingCsvPath, labelColumn))
        {
            var token = raw.Trim();
            switch (token.ToLowerInvariant())
            {
                case "true" or "1" or "yes" or "y" or "t":
                    trueToken ??= token;
                    break;
                case "false" or "0" or "no" or "n" or "f":
                    falseToken ??= token;
                    break;
                default:
                    return null; // a class token outside the boolean families — don't guess a convention
            }

            if (trueToken is not null && falseToken is not null)
            {
                break;
            }
        }

        return trueToken is not null && falseToken is not null ? (trueToken, falseToken) : null;
    }

    /// <summary>
    /// Reads the anomaly <c>Score</c> paired with the ground-truth label (numeric 0/1, positive = anomaly)
    /// from a scored view, for computing AUC in the evaluate node.
    /// </summary>
    public static List<(double Score, bool Positive)> ReadScoreAndLabel(IDataView scored, string labelColumn)
    {
        var rows = new List<(double, bool)>();
        var scoreCol = scored.Schema["Score"];
        var labelCol = scored.Schema[labelColumn];
        using var cursor = scored.GetRowCursor([scoreCol, labelCol]);
        var scoreGetter = cursor.GetGetter<float>(scoreCol);

        // A 0/1 label is inferred as Boolean by the loader but as Single when the schema is carried, so read
        // whichever type the column actually is (positive = anomaly).
        float score = 0;
        if (labelCol.Type.RawType == typeof(bool))
        {
            var boolGetter = cursor.GetGetter<bool>(labelCol);
            bool label = false;
            while (cursor.MoveNext())
            {
                scoreGetter(ref score);
                boolGetter(ref label);
                rows.Add((score, label));
            }
        }
        else
        {
            var floatGetter = cursor.GetGetter<float>(labelCol);
            float label = 0;
            while (cursor.MoveNext())
            {
                scoreGetter(ref score);
                floatGetter(ref label);
                rows.Add((score, label >= 0.5f));
            }
        }

        return rows;
    }

    public static List<string> ReadColumn(string path, string columnName)
    {
        var values = new List<string>();
        using var reader = new StreamReader(path);
        string[]? header = null;
        var index = 0;
        foreach (var record in KocCsv.ParseRecords(reader))
        {
            if (header is null)
            {
                header = record;
                index = Array.FindIndex(header, c => c.Trim().Equals(columnName, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    index = 0;
                }

                continue;
            }

            values.Add(index < record.Length ? record[index].Trim() : string.Empty);
        }

        return values;
    }
}
