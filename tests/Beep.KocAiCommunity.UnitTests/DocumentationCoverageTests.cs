using Beep.KocAiCommunity.Application.Help;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.ML.Nodes;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The reference claims to cover <em>every</em> node and algorithm. That claim decays the moment
/// someone adds one, and nothing about a missing page is visible at runtime — the node simply has no
/// explanation. These turn that silent gap into a failing build.
/// </summary>
public class DocumentationCoverageTests
{
    private static readonly PluginNodeRegistry Registry = new(
        typeof(PluginNodeExecutor).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IPipelineNodeHandler).IsAssignableFrom(t))
            .Select(t => (IPipelineNodeHandler)System.Activator.CreateInstance(t)!));

    private static string AllHelpText =>
        string.Join('\n', HelpCatalog.All.Select(a => a.BodyMarkdown));

    [Fact]
    public void Every_node_kind_is_documented_somewhere()
    {
        var undocumented = Registry.Kinds
            .Where(kind => !AllHelpText.Contains($"`{kind}`", StringComparison.Ordinal))
            .OrderBy(kind => kind, StringComparer.Ordinal)
            .ToList();

        undocumented.Should().BeEmpty(
            "every node in the palette needs a reference entry — add it to the article for its category");
    }

    [Fact]
    public void Every_node_category_has_a_reference_article()
    {
        foreach (var category in Registry.Categories)
        {
            var slug = $"nodes-{category.ToLowerInvariant()}";
            HelpCatalog.All.Should().Contain(
                a => a.Slug == slug,
                "the property panel links to /help/{0} for {1} nodes", slug, category);
        }
    }

    [Fact]
    public void Every_algorithm_is_in_the_algorithm_reference()
    {
        var article = HelpCatalog.All.Single(a => a.Slug == "algorithms");

        foreach (var algorithm in MlAlgorithms.All)
        {
            article.BodyMarkdown.Should().Contain($"`{algorithm.Value}`",
                "an algorithm the Train node offers must say what it is for");
        }
    }

    [Fact]
    public void Every_ml_task_is_reachable_from_the_reference()
    {
        // A task with no algorithm tagged for it would render an empty dropdown in the property panel.
        foreach (var task in MlTaskCatalog.All.Where(t => t.Task is not null))
        {
            MlAlgorithms.All.Should().Contain(
                a => a.AppliesTo == null || a.AppliesTo.Contains(task.Task!.ToString()!),
                "task '{0}' needs at least one algorithm", task.DisplayName);
        }
    }

    [Fact]
    public void Reference_articles_are_findable()
    {
        var reference = HelpCatalog.All.Where(a => a.Category == "Reference").ToList();

        reference.Should().HaveCountGreaterThanOrEqualTo(9);
        reference.Should().OnlyContain(a =>
            !string.IsNullOrWhiteSpace(a.Title)
            && !string.IsNullOrWhiteSpace(a.Summary)
            && a.Tags.Count > 0,
            "the help page searches on title, summary and tags");
    }
}
