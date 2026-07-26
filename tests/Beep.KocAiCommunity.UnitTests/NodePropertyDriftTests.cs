using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Beep.KocAiCommunity.ML.Nodes;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Anti-drift guard for the node property panel (Phase 21a). The <c>NodeDescriptor.Parameters</c> list is the
/// single source of truth for three things at once — the designer property inspector, the API node catalog
/// (<c>GET /api/v1/ml/nodes</c>), and parameter validation. The model executor, however, reads hyperparameters
/// straight from the raw node <c>Config</c> (<see cref="MlModelOps"/>). If a knob is read but never declared,
/// it is invisible in the UI and users are silently locked to its hardcoded default — exactly the defect that
/// left <c>trees</c>/<c>leaves</c>/<c>learningRate</c>/<c>l2</c> untunable. This test scans the executor source
/// for every <c>Config</c> read and fails if the model-node descriptors don't declare it, so a hidden knob can
/// never reappear: any future setting added to the executor must also be declared to keep this green.
/// </summary>
public class NodePropertyDriftTests
{
    private static readonly PluginNodeRegistry Registry = new(
        typeof(PluginNodeExecutor).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IPipelineNodeHandler).IsAssignableFrom(t))
            .Select(t => (IPipelineNodeHandler)Activator.CreateInstance(t)!));

    // Both model nodes delegate training to MlModelOps, so every Config key it reads must be declared on both.
    [Theory]
    [InlineData("train")]
    [InlineData("cross-validate")]
    public void Model_node_descriptor_declares_every_config_key_the_executor_reads(string kind)
    {
        var read = ConfigKeysReadBy("MlModelOps.cs");
        read.Should().NotBeEmpty("the guard is only meaningful if it actually found the executor's Config reads");

        var declared = Registry.Find(kind)!.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var hidden = read.Where(k => !declared.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

        hidden.Should().BeEmpty(
            $"'{kind}' reads these config keys via MlModelOps but its descriptor doesn't declare them, so the " +
            "property panel can't render them and every user is locked to the hardcoded default");
    }

    // Extract the node Config keys the executor reads: Cfg/HpInt/HpFloat(node, "key"), plus Algo(node) which
    // internally reads Cfg(node, "algorithm").
    private static HashSet<string> ConfigKeysReadBy(string sourceFile)
    {
        var text = File.ReadAllText(MlNodesSourcePath(sourceFile));
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(text, @"\b(?:Cfg|HpInt|HpFloat)\(node,\s*""([^""]+)"""))
        {
            keys.Add(m.Groups[1].Value);
        }

        if (Regex.IsMatch(text, @"\bAlgo\(node\)"))
        {
            keys.Add("algorithm");
        }

        return keys;
    }

    private static string MlNodesSourcePath(string fileName, [CallerFilePath] string thisFile = "")
    {
        // tests/Beep.KocAiCommunity.UnitTests/<this file> → up two → repo root → src/…/ML/Nodes/<file>.
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "Beep.KocAiCommunity.ML", "Nodes", fileName);
    }
}
