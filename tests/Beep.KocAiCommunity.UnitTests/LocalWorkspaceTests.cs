using System.Text;
using Beep.KocAiCommunity.Desktop.Local;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The launch integrity check. Before it existed, a workspace in a bad state produced failures one
/// operation at a time, well away from the cause.
/// <para>
/// The rule these pin: <b>repair what is ours, never delete what is the user's.</b> Folders and the
/// dataset index are ours. A workflow file is not.
/// </para>
/// </summary>
public sealed class LocalWorkspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "koc-ws-" + Guid.NewGuid().ToString("N"));
    private readonly LocalWorkspace _workspace;

    public LocalWorkspaceTests()
    {
        _workspace = new LocalWorkspace { RootPath = _root };
        _workspace.EnsureCreated();
    }

    [Fact]
    public void A_healthy_workspace_reports_nothing()
    {
        var report = _workspace.Verify();

        report.IsClean.Should().BeTrue();
        report.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void A_missing_folder_is_recreated_and_reported()
    {
        Directory.Delete(_workspace.DatasetsPath, recursive: true);

        var report = _workspace.Verify();

        Directory.Exists(_workspace.DatasetsPath).Should().BeTrue();
        report.Findings.Should().ContainSingle(f =>
            f.Level == WorkspaceFindingLevel.Repaired && f.Message.Contains("datasets"));
    }

    [Fact]
    public void An_unreadable_workflow_is_reported_but_left_alone()
    {
        // The user's work. Reporting it is right; tidying it away is not.
        var path = Path.Combine(_workspace.WorkflowsPath, "broken.json");
        File.WriteAllText(path, "{ this is not json");

        var report = _workspace.Verify();

        File.Exists(path).Should().BeTrue("a workflow file is the user's, not ours to delete");
        report.Findings.Should().Contain(f =>
            f.Level == WorkspaceFindingLevel.Warning && f.Message.Contains("broken.json"));
    }

    [Fact]
    public void A_valid_workflow_is_not_flagged()
    {
        File.WriteAllText(Path.Combine(_workspace.WorkflowsPath, "fine.json"), """{ "nodes": [] }""");

        _workspace.Verify().Findings.Should().NotContain(f => f.Message.Contains("fine.json"));
    }

    [Fact]
    public void Stale_scratch_is_swept_and_recent_scratch_is_kept()
    {
        var stale = Path.Combine(_workspace.TempPath, "stale.tmp");
        var fresh = Path.Combine(_workspace.TempPath, "fresh.tmp");
        File.WriteAllText(stale, "x");
        File.WriteAllText(fresh, "x");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-4));

        var report = _workspace.Verify();

        File.Exists(stale).Should().BeFalse("it is older than the two-hour cutoff");
        File.Exists(fresh).Should().BeTrue("a run in flight owns its scratch");
        report.Findings.Should().Contain(f => f.Message.Contains("scratch"));
    }

    [Fact]
    public void A_workspace_that_cannot_be_written_is_blocked_rather_than_thrown()
    {
        // A path under a file cannot be a directory — the cheapest portable way to make creation fail.
        var blocker = Path.Combine(_root, "blocker");
        File.WriteAllText(blocker, "x");

        var report = new LocalWorkspace { RootPath = Path.Combine(blocker, "workspace") }.Verify();

        report.IsBlocked.Should().BeTrue();
        report.BlockedReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Logs_folder_is_created_so_a_crash_at_startup_has_somewhere_to_go()
    {
        Directory.Exists(_workspace.LogsPath).Should().BeTrue();
    }

    [Fact]
    public async Task A_corrupt_dataset_index_is_preserved_and_rebuilt()
    {
        // The ids in the index are the only link between a saved workflow and its dataset. Losing them
        // silently would break every workflow with no clue why, so the bad file is kept.
        var store = new LocalDatasetStore(_workspace);
        await store.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes("a\n1\n")), "kept.csv");

        var indexPath = Path.Combine(_workspace.DatasetsPath, ".index.json");
        File.WriteAllText(indexPath, "{ not json at all");

        var reopened = new LocalDatasetStore(_workspace);
        var listed = reopened.List();

        listed.Should().ContainSingle(d => d.Name == "kept", "the file is still on disk");
        reopened.IndexWasRebuilt.Should().BeTrue();
        Directory.GetFiles(_workspace.DatasetsPath, ".index.json.corrupt-*")
            .Should().NotBeEmpty("the unreadable index is kept in case the ids can be salvaged");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A locked temp file must not fail the run.
        }
    }
}
