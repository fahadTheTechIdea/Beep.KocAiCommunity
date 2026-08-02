using System.Text;
using Beep.KocAiCommunity.Desktop.Local;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The desktop Studio's own data. Until import existed, the only way to get a CSV in was to know about
/// <c>%LOCALAPPDATA%\KocStudio\datasets</c> and copy files there by hand — so the designer opened with an
/// empty dataset picker and no way to say why.
/// </summary>
public sealed class LocalDatasetStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "koc-store-" + Guid.NewGuid().ToString("N"));
    private readonly LocalDatasetStore _store;

    public LocalDatasetStoreTests()
    {
        var workspace = new LocalWorkspace { RootPath = _root };
        workspace.EnsureCreated();
        _store = new LocalDatasetStore(workspace);
    }

    private static Stream Csv(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task An_imported_file_becomes_a_listed_dataset()
    {
        var dataset = await _store.ImportAsync(Csv("id,pressure,failed\n1,220,0\n2,190,1\n"), "wells.csv");

        dataset.Name.Should().Be("wells");
        dataset.HasFile.Should().BeTrue();

        _store.List().Should().ContainSingle(d => d.Id == dataset.Id);
        File.Exists(_store.PathFor(dataset.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task A_second_file_of_the_same_name_does_not_overwrite_the_first()
    {
        // Two people's "data.csv" are not the same file, and losing one silently would be worse than
        // an awkward name.
        var first = await _store.ImportAsync(Csv("a\n1\n"), "data.csv");
        var second = await _store.ImportAsync(Csv("b\n2\n"), "data.csv");

        second.Id.Should().NotBe(first.Id);
        _store.List().Should().HaveCount(2);

        (await File.ReadAllTextAsync(_store.PathFor(first.Id)!)).Should().StartWith("a");
        (await File.ReadAllTextAsync(_store.PathFor(second.Id)!)).Should().StartWith("b");
    }

    [Fact]
    public async Task An_id_survives_a_restart()
    {
        // A saved workflow references a dataset by id. If ids were minted per session, every workflow
        // would break on the next launch.
        var imported = await _store.ImportAsync(Csv("x\n1\n"), "stable.csv");

        var reopened = new LocalDatasetStore(new LocalWorkspace { RootPath = _root });

        reopened.List().Should().ContainSingle(d => d.Id == imported.Id);
    }

    [Fact]
    public async Task A_hostile_file_name_cannot_escape_the_workspace()
    {
        var dataset = await _store.ImportAsync(Csv("x\n1\n"), "../../evil.csv");

        var path = _store.PathFor(dataset.Id);
        path.Should().NotBeNull();
        Path.GetFullPath(path!).Should().StartWith(Path.GetFullPath(_root),
            "an imported name must never place a file outside the workspace");
    }

    [Fact]
    public async Task Deleting_removes_the_file_and_the_entry()
    {
        var dataset = await _store.ImportAsync(Csv("x\n1\n"), "gone.csv");
        var path = _store.PathFor(dataset.Id)!;

        _store.Delete(dataset.Id).Should().BeTrue();

        File.Exists(path).Should().BeFalse();
        _store.List().Should().BeEmpty();
        _store.PathFor(dataset.Id).Should().BeNull();
        _store.Delete(dataset.Id).Should().BeFalse("it is already gone");
    }

    [Fact]
    public async Task Preview_reads_the_header_and_a_few_rows()
    {
        var dataset = await _store.ImportAsync(
            Csv("id,pressure,failed\n1,220,0\n2,190,1\n3,205,0\n4,180,1\n"), "peek.csv");

        var (header, rows, _) = await _store.PeekAsync(dataset.Id, rows: 2);

        header.Should().BeEquivalentTo(["id", "pressure", "failed"], o => o.WithStrictOrdering());
        rows.Should().HaveCount(2);
        rows[0].Should().BeEquivalentTo(["1", "220", "0"], o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Preview_respects_quoted_commas()
    {
        var dataset = await _store.ImportAsync(Csv("id,note\n1,\"Al-Rawdatain, north\"\n"), "quoted.csv");

        var (header, rows, _) = await _store.PeekAsync(dataset.Id);

        header.Should().HaveCount(2);
        rows[0][1].Should().Be("Al-Rawdatain, north", "a comma inside quotes is not a column break");
    }

    [Fact]
    public async Task An_empty_file_previews_as_empty_rather_than_throwing()
    {
        var dataset = await _store.ImportAsync(Csv(""), "empty.csv");

        var (header, rows, _) = await _store.PeekAsync(dataset.Id);

        header.Should().BeEmpty();
        rows.Should().BeEmpty();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A locked temp file must not fail the test run.
        }
    }
}
