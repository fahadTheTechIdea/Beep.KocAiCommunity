using System.Globalization;
using Beep.KocAiCommunity.Application.Common;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Microsoft.ML;

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

    public static (IEstimator<ITransformer> Trainer, string Name) MulticlassTrainer(MLContext ml, WorkflowNode node)
    {
        const string label = "Label";
        const string features = "Features";
        var l2 = PipelineContext.HpFloat(node, "l2");
        return PipelineContext.Algo(node) switch
        {
            "lbfgs" => (ml.MulticlassClassification.Trainers.LbfgsMaximumEntropy(label, features, l2Regularization: l2 ?? 1f), "LbfgsMaximumEntropy"),
            "naivebayes" => (ml.MulticlassClassification.Trainers.NaiveBayes(label, features), "NaiveBayes"),
            _ => (ml.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: label, featureColumnName: features, l2Regularization: l2), "SdcaMaximumEntropy"),
        };
    }

    public static (IEstimator<ITransformer> Trainer, string Name) Trainer(MLContext ml, MlTaskType task, WorkflowNode node, string label)
    {
        const string features = "Features";
        const int minLeaf = 10;
        var algo = PipelineContext.Algo(node);
        var trees = PipelineContext.HpInt(node, "trees", 100);
        var leaves = PipelineContext.HpInt(node, "leaves", 20);
        var learningRate = PipelineContext.ReadDouble(PipelineContext.Cfg(node, "learningRate"), 0.2);
        var l2 = PipelineContext.HpFloat(node, "l2");

        if (task == MlTaskType.Regression)
        {
            return algo switch
            {
                "fasttree" => (ml.Regression.Trainers.FastTree(label, features, numberOfLeaves: leaves, numberOfTrees: trees, minimumExampleCountPerLeaf: minLeaf, learningRate: learningRate), "FastTreeRegression"),
                "fastforest" => (ml.Regression.Trainers.FastForest(label, features, numberOfLeaves: leaves, numberOfTrees: trees, minimumExampleCountPerLeaf: minLeaf), "FastForestRegression"),
                "lbfgs" => (ml.Regression.Trainers.LbfgsPoissonRegression(label, features, l2Regularization: l2 ?? 1f), "LbfgsPoissonRegression"),
                _ => (ml.Regression.Trainers.Sdca(labelColumnName: label, featureColumnName: features, l2Regularization: l2), "SdcaRegression"),
            };
        }

        return algo switch
        {
            "fasttree" => (ml.BinaryClassification.Trainers.FastTree(label, features, numberOfLeaves: leaves, numberOfTrees: trees, minimumExampleCountPerLeaf: minLeaf, learningRate: learningRate), "FastTreeBinary"),
            "fastforest" => (ml.BinaryClassification.Trainers.FastForest(label, features, numberOfLeaves: leaves, numberOfTrees: trees, minimumExampleCountPerLeaf: minLeaf), "FastForestBinary"),
            "lbfgs" => (ml.BinaryClassification.Trainers.LbfgsLogisticRegression(label, features, l2Regularization: l2 ?? 1f), "LbfgsLogisticRegression"),
            "perceptron" => (ml.BinaryClassification.Trainers.AveragedPerceptron(label, features), "AveragedPerceptron"),
            _ => (ml.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: label, featureColumnName: features, l2Regularization: l2), "SdcaLogisticRegression"),
        };
    }

    public static List<string> ReadPredictions(IDataView scored, MlTaskType task)
    {
        var list = new List<string>();
        if (task == MlTaskType.Regression)
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
            var col = scored.Schema["PredictedLabel"];
            using var cursor = scored.GetRowCursor([col]);
            var getter = cursor.GetGetter<bool>(col);
            bool value = false;
            while (cursor.MoveNext())
            {
                getter(ref value);
                list.Add(value ? "true" : "false");
            }
        }
        return list;
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
