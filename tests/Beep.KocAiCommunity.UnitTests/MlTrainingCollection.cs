using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Serializes the AutoML-training test classes. Each runs a full ML.NET AutoML experiment (many
/// trainers, multiple threads); running several concurrently starves CPU/memory and makes AutoML
/// throw intermittently. Sharing this non-parallel collection keeps them one-at-a-time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MlTrainingCollection
{
    public const string Name = "ML training (serialized)";
}
