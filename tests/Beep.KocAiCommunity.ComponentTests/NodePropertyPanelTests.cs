using Beep.KocAiCommunity.Contracts.Datasets;
using Beep.KocAiCommunity.Contracts.ML;
using Beep.KocAiCommunity.Ui.Shared.Components;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

/// <summary>
/// The one property panel that edits every node. These render it with real parameter shapes and confirm it
/// draws each value type and gets/sets the node's config — proof the nodes ↔ panel wiring actually works.
/// </summary>
public class NodePropertyPanelTests : TestContext
{
    private static NodeParameterDto Param(string name, string type, bool required = false, string? def = null,
        IReadOnlyList<LookupOptionDto>? options = null, double? min = null, double? max = null, string? help = null)
        => new(name, name, type, required, def, options, min, max, help);

    private static NodeDescriptorDto Descriptor(params NodeParameterDto[] p)
        => new("k", "Cat", "Node", "desc", "Table", "Table", p);

    private static NodeDescriptorDto DescriptorOfKind(string kind, params NodeParameterDto[] p)
        => new(kind, "Data", "Node", "desc", "Table", "Table", p);

    private IRenderedComponent<NodePropertyPanel> Render(
        NodeDescriptorDto? descriptor, IDictionary<string, string> config,
        string task = "BinaryClassification", IReadOnlyList<DatasetDto>? datasets = null, Action? onChanged = null,
        IReadOnlyList<string>? lockedKeys = null)
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        return RenderComponent<NodePropertyPanel>(ps => ps
            .Add(x => x.Descriptor, descriptor)
            .Add(x => x.Config, config)
            .Add(x => x.Task, task)
            .Add(x => x.Datasets, datasets ?? [])
            .Add(x => x.LockedKeys, lockedKeys ?? [])
            .Add(x => x.OnChanged, Microsoft.AspNetCore.Components.EventCallback.Factory.Create(this, onChanged ?? (() => { }))));
    }

    [Fact]
    public void No_parameters_shows_the_no_options_message()
    {
        Render(Descriptor(), new Dictionary<string, string>())
            .Markup.Should().Contain("No options");

        Render(null, new Dictionary<string, string>())
            .Markup.Should().Contain("No options");
    }

    [Fact]
    public void Renders_every_value_type_without_error()
    {
        // One parameter of each type — the panel must draw them all (a type it can't render would throw here).
        var descriptor = Descriptor(
            Param("txt", "Text"),
            Param("num", "Number", min: 0, max: 1, def: "0.5"),
            Param("whole", "Integer", min: 2, max: 255, def: "10"),
            Param("flag", "Boolean"),
            Param("day", "Date"),
            Param("algo", "Select", def: "sdca", options: [new("sdca", "SDCA", null), new("fasttree", "FastTree", null)]),
            Param("cols", "Columns"),
            Param("col", "Column"),
            Param("ds", "Dataset"));

        var cut = Render(descriptor, new Dictionary<string, string>());

        // Each field's label is rendered.
        foreach (var label in new[] { "txt", "num", "whole", "flag", "day", "algo", "cols", "col", "ds" })
        {
            cut.Markup.Should().Contain(label);
        }
    }

    [Fact]
    public void Editing_a_text_field_writes_the_value_to_config_and_raises_onchanged()
    {
        var config = new Dictionary<string, string>();
        var changed = 0;
        var cut = Render(Descriptor(Param("column", "Text")), config, onChanged: () => changed++);

        cut.Find("input").Change("pressure");

        config.Should().ContainKey("column").WhoseValue.Should().Be("pressure");
        changed.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Clearing_a_field_removes_it_from_config()
    {
        var config = new Dictionary<string, string> { ["column"] = "pressure" };
        var cut = Render(Descriptor(Param("column", "Text")), config);

        cut.Find("input").Change("");

        config.Should().NotContainKey("column");
    }

    [Fact]
    public void Editing_a_numeric_field_writes_the_number_to_config()
    {
        var config = new Dictionary<string, string>();
        var cut = Render(Descriptor(Param("bins", "Integer", min: 2, max: 255, def: "10")), config);

        cut.Find("input").Change("50");

        config.Should().ContainKey("bins").WhoseValue.Should().Be("50");
    }

    [Fact]
    public void Column_fields_become_pickers_when_the_columns_are_known()
    {
        var descriptor = Descriptor(Param("column", "Column"), Param("columns", "Columns"));
        var cols = new[] { "pclass", "sex", "age", "fare" };

        var cut = Render(descriptor, new Dictionary<string, string>());
        cut.SetParametersAndRender(ps => ps.Add(x => x.AvailableColumns, cols));

        // Both Column and Columns render as MudSelect pickers (not text boxes), each offering the real columns.
        var selects = cut.FindComponents<MudSelect<string>>();
        selects.Should().HaveCountGreaterThanOrEqualTo(2);
        cut.FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value)
            .Should().Contain(cols);
    }

    [Fact]
    public void Column_field_falls_back_to_a_text_box_when_no_columns_are_known()
    {
        var cut = Render(Descriptor(Param("column", "Column")), new Dictionary<string, string>());

        // No known columns → a text field, no select picker.
        cut.FindComponents<MudSelect<string>>().Should().BeEmpty();
        cut.FindComponents<MudTextField<string>>().Should().NotBeEmpty();
    }

    [Fact]
    public void Algorithm_options_follow_the_nodes_own_task_field()
    {
        // The Train node carries its own `task`; the algorithm dropdown must filter by it (not the fallback).
        var task = Param("task", "Select", def: "BinaryClassification", options:
            [new("BinaryClassification", "Binary", null), new("Regression", "Reg", null)]);
        var algo = Param("algorithm", "Select", def: "sdca", options:
            [new("sdca", "SDCA", ["BinaryClassification", "Regression"]), new("perceptron", "Perceptron", ["BinaryClassification"])]);

        // Fallback Task is Binary, but the node's own task is Regression → perceptron (binary-only) must vanish.
        var cut = Render(Descriptor(task, algo), new Dictionary<string, string> { ["task"] = "Regression" }, task: "BinaryClassification");

        var values = cut.FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value).ToList();
        values.Should().Contain("sdca");
        values.Should().NotContain("perceptron");
    }

    [Fact]
    public void Locked_keys_render_the_field_read_only()
    {
        var algo = Param("algorithm", "Select", def: "sdca", options: [new("sdca", "SDCA", null)]);

        var unlocked = Render(Descriptor(algo), new Dictionary<string, string>());
        unlocked.FindComponents<MudSelect<string>>().Should().NotContain(s => s.Instance.ReadOnly);

        var locked = Render(Descriptor(algo), new Dictionary<string, string>(), lockedKeys: ["algorithm"]);
        locked.FindComponents<MudSelect<string>>().Should().Contain(s => s.Instance.ReadOnly);
    }

    [Fact]
    public void Sql_nodes_get_a_visual_builder_when_columns_are_known()
    {
        var cols = new[] { "pressure", "zone" };

        var groupBy = Render(DescriptorOfKind("group-by", Param("groupBy", "Columns"), Param("aggregations", "Text")), new Dictionary<string, string>());
        groupBy.SetParametersAndRender(ps => ps.Add(x => x.AvailableColumns, cols));
        groupBy.FindComponents<AggregateBuilder>().Should().NotBeEmpty("group-by gets an aggregate builder");

        var sort = Render(DescriptorOfKind("sort", Param("orderBy", "Text")), new Dictionary<string, string>());
        sort.SetParametersAndRender(ps => ps.Add(x => x.AvailableColumns, cols));
        sort.FindComponents<SortKeyBuilder>().Should().NotBeEmpty("sort gets a sort-key builder");

        var filter = Render(DescriptorOfKind("sql-filter", Param("where", "Text")), new Dictionary<string, string>());
        filter.SetParametersAndRender(ps => ps.Add(x => x.AvailableColumns, cols));
        filter.FindComponents<SqlConditionBuilder>().Should().NotBeEmpty("sql-filter gets a condition builder");

        // A non-SQL node gets none of the builders.
        var binning = Render(DescriptorOfKind("binning", Param("bins", "Integer")), new Dictionary<string, string>());
        binning.SetParametersAndRender(ps => ps.Add(x => x.AvailableColumns, cols));
        binning.FindComponents<AggregateBuilder>().Should().BeEmpty();
        binning.FindComponents<SortKeyBuilder>().Should().BeEmpty();
        binning.FindComponents<SqlConditionBuilder>().Should().BeEmpty();
    }

    [Fact]
    public void Lookup_shows_only_options_that_apply_to_the_active_task()
    {
        // perceptron applies to binary only, naivebayes to multiclass only.
        var algo = Param("algorithm", "Select", def: "sdca", options:
        [
            new("sdca", "SDCA", ["BinaryClassification", "Regression", "MulticlassClassification"]),
            new("perceptron", "Averaged Perceptron", ["BinaryClassification"]),
            new("naivebayes", "Naive Bayes", ["MulticlassClassification"]),
        ]);

        var regression = Render(Descriptor(algo), new Dictionary<string, string>(), task: "Regression");
        regression.Markup.Should().Contain("SDCA");
        regression.Markup.Should().NotContain("Averaged Perceptron");
        regression.Markup.Should().NotContain("Naive Bayes");
    }
}
