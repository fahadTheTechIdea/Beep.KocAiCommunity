using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.ML;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// One trained model, shared by every test that needs <em>a</em> model rather than a particular one.
/// <para>
/// The registry, the bundle format and the prediction service all need a real ML.NET model — their
/// behaviour is read out of its schema, so a fake byte array would not exercise it. Training one each
/// would cost three AutoML runs, and AutoML runs are the slowest and most contention-sensitive thing in
/// this suite.
/// </para>
/// </summary>
public sealed class TrainedModelFixture : IAsyncLifetime
{
    /// <summary>Binary classification on <c>x1</c>, <c>x2</c> → <c>label</c>, cleanly separable.</summary>
    public byte[] ModelBytes { get; private set; } = [];

    public const string TargetColumn = "label";

    public async Task InitializeAsync()
    {
        var csv = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            csv.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            csv.Append($"{i % 3},{(i / 3) % 3},false\n");
        }

        var captured = await new AutoMlTrainer().TrainAndCaptureAsync(
            MlTaskType.BinaryClassification,
            new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString())),
            TargetColumn,
            maxSeconds: 5);

        ModelBytes = captured.ModelBytes;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// Serializes the AutoML-training test classes. Each runs a full ML.NET AutoML experiment (many
/// trainers, multiple threads); running several concurrently starves CPU/memory and makes AutoML
/// throw intermittently. Sharing this non-parallel collection keeps them one-at-a-time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MlTrainingCollection : ICollectionFixture<TrainedModelFixture>
{
    public const string Name = "ML training (serialized)";
}
