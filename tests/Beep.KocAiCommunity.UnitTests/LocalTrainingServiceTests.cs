using System.Diagnostics;
using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Desktop.Local;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The desktop's training loop, driven by a stand-in child process.
/// <para>
/// The point of these is the endings, not the maths. A run that stops, or fails, or is killed by the
/// memory watchdog still has to be recorded and still has to say <em>which</em> of those happened — a
/// failure the history reports as "stopped" is worse than no history.
/// </para>
/// <para>
/// The stand-in is a real process, because the behaviour being tested is process behaviour: it has to
/// be killable, and its memory has to be readable from out here. It is <c>cmd</c> echoing the same JSON
/// lines a training child would print.
/// </para>
/// </summary>
public sealed class LocalTrainingServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "koc-train-" + Guid.NewGuid().ToString("N"));
    private readonly LocalWorkspace _workspace;
    private readonly LocalDatasetStore _datasets;
    private readonly LocalRunStore _runs;

    public LocalTrainingServiceTests()
    {
        _workspace = new LocalWorkspace { RootPath = _root };
        _workspace.EnsureCreated();
        _datasets = new LocalDatasetStore(_workspace);
        _runs = new LocalRunStore(_workspace);
    }

    private LocalTrainingService Service(LocalTrainingLimits? limits = null) =>
        new(_workspace, _datasets, _runs, limits ?? new LocalTrainingLimits());

    private async Task<Guid> SeedDatasetAsync()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("id,pressure,failed\n1,120,0\n2,340,1\n"));
        return (await _datasets.ImportAsync(stream, "esp-readings.csv")).Id;
    }

    /// <summary>
    /// A child that prints the given stdout lines and exits. The lines go through a file rather than
    /// the command line — JSON is most of the way to unquotable in <c>cmd</c>.
    /// </summary>
    private Func<string, Process?> ChildPrinting(params string[] lines)
    {
        var scratch = Path.Combine(_root, $"child-{Guid.NewGuid():N}.txt");
        File.WriteAllLines(scratch, lines);

        return _ => Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c type \"{scratch}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    /// <summary>A child that sits there until something kills it.</summary>
    private static Func<string, Process?> ChildThatHangs() => _ => Process.Start(new ProcessStartInfo
    {
        FileName = "cmd.exe",
        Arguments = "/c ping -n 120 127.0.0.1",
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    });

    private const string Trial1 =
        """{"type":"trial","trialNumber":1,"trainerName":"FastTree","metricName":"Accuracy","metricValue":0.81,"runtimeSeconds":1.2}""";

    private const string Trial2 =
        """{"type":"trial","trialNumber":2,"trainerName":"LightGbm","metricName":"Accuracy","metricValue":0.93,"runtimeSeconds":2.4}""";

    private const string Result =
        """{"type":"result","algorithm":"LightGbm","primaryMetric":"Accuracy","primaryValue":0.93,"secondaryMetric":"AUC","secondaryValue":0.97,"rowCount":400}""";

    [Fact]
    public async Task A_completed_run_records_its_winner_and_its_trials()
    {
        var id = await SeedDatasetAsync();
        var service = Service();
        service.StartChild = ChildPrinting(Trial1, Trial2, Result);

        var run = await service.TrainAsync(id, "failed", MlTaskType.BinaryClassification);

        run.Outcome.Should().Be(LocalRunOutcome.Completed);
        run.Algorithm.Should().Be("LightGbm");
        run.PrimaryValue.Should().Be(0.93);
        run.TrialsCompleted.Should().Be(2, "both trials were printed before the child exited");
        run.RowCount.Should().Be(400);

        _runs.Get(run.Id).Should().NotBeNull();
        _runs.ReadTrialLog(run.Id).Should().Contain("LightGbm");
    }

    [Fact]
    public async Task A_child_that_reports_an_error_is_recorded_as_failed_with_that_error()
    {
        // The run that fails is exactly the one someone comes back to look at. Dropping it from the
        // history would leave a gap where the explanation should be.
        var id = await SeedDatasetAsync();
        var service = Service();
        service.StartChild = ChildPrinting(
            """{"type":"error","message":"Column 'failed' has one value."}""");

        var run = await service.TrainAsync(id, "failed", MlTaskType.BinaryClassification);

        run.Outcome.Should().Be(LocalRunOutcome.Failed);
        run.Error.Should().Contain("one value");
        _runs.Get(run.Id).Should().NotBeNull();
    }

    [Fact]
    public async Task A_child_that_says_nothing_at_all_is_a_failure_not_a_success()
    {
        // Exit code 0 with no result line: something swallowed the run. Recording it as completed with
        // no metrics would put a row in the history that looks like a model and is not one.
        var id = await SeedDatasetAsync();
        var service = Service();
        service.StartChild = ChildPrinting("nothing useful");

        var run = await service.TrainAsync(id, "failed", MlTaskType.BinaryClassification);

        run.Outcome.Should().Be(LocalRunOutcome.Failed);
        run.PrimaryValue.Should().BeNull();
    }

    [Fact]
    public async Task Stopping_kills_the_child_and_is_recorded_as_stopped()
    {
        // This is the whole reason training is out of process: the child can be killed, and killing it
        // is what makes Stop mean something.
        var id = await SeedDatasetAsync();
        var service = Service();
        service.StartChild = ChildThatHangs();

        var training = service.TrainAsync(id, "failed", MlTaskType.BinaryClassification);
        await WaitUntil(() => service.IsRunning);
        service.Stop();

        var run = await training;

        run.Outcome.Should().Be(LocalRunOutcome.Stopped);
        run.Error.Should().NotContain("memory", "the user stopped this, the watchdog did not");
        _runs.Get(run.Id).Should().NotBeNull("a stopped run is still part of the history");
    }

    [Fact]
    public async Task The_memory_watchdog_kills_the_child_and_says_that_is_why()
    {
        var id = await SeedDatasetAsync();
        var service = Service(new LocalTrainingLimits
        {
            MaxMemoryMb = 600,
            MemoryCheckInterval = TimeSpan.FromMilliseconds(50),
        });
        service.StartChild = ChildThatHangs();

        // Over the ceiling from the first sample, so the trip is deterministic rather than dependent on
        // what a `cmd` process happens to weigh.
        service.ReadWorkingSetMb = _ => 4096;

        var run = await service.TrainAsync(id, "failed", MlTaskType.BinaryClassification);

        run.Outcome.Should().Be(LocalRunOutcome.OutOfBudget);
        run.Error.Should().Contain("memory");
        run.LimitMemoryMb.Should().Be(600);
    }

    [Fact]
    public async Task A_run_that_stays_under_the_ceiling_is_left_alone()
    {
        // The other half of the watchdog: it must not end a run that is behaving.
        var id = await SeedDatasetAsync();
        var service = Service(new LocalTrainingLimits
        {
            MaxMemoryMb = 2048,
            MemoryCheckInterval = TimeSpan.FromMilliseconds(50),
        });
        service.StartChild = ChildPrinting(Trial1, Result);
        service.ReadWorkingSetMb = _ => 900;

        var run = await service.TrainAsync(id, "failed", MlTaskType.BinaryClassification);

        run.Outcome.Should().Be(LocalRunOutcome.Completed);
    }

    [Fact]
    public async Task A_stopped_run_keeps_no_model_file()
    {
        // A killed child leaves a partly written model behind. Keeping it would put a file in the
        // history that claims to be a model and is not.
        var id = await SeedDatasetAsync();
        var service = Service();
        service.StartChild = job =>
        {
            var directory = Path.Combine(_root, "runs");
            foreach (var dir in Directory.GetDirectories(directory))
            {
                File.WriteAllBytes(Path.Combine(dir, "model.zip"), [1, 2, 3]);
            }

            return ChildThatHangs()(job);
        };

        var training = service.TrainAsync(id, "failed", MlTaskType.BinaryClassification);
        await WaitUntil(() => service.IsRunning);
        service.Stop();
        var run = await training;

        run.Outcome.Should().Be(LocalRunOutcome.Stopped);
        _runs.Get(run.Id)!.HasModel.Should().BeFalse();
    }

    [Fact]
    public async Task A_run_records_the_dataset_hash_it_trained_on()
    {
        var id = await SeedDatasetAsync();
        var service = Service();
        service.StartChild = ChildPrinting(Result);

        var run = await service.TrainAsync(id, "failed", MlTaskType.BinaryClassification);

        run.DatasetHash.Should().NotBeNullOrEmpty();
        run.DatasetHash.Should().Be(await LocalRunStore.HashFileAsync(_datasets.PathFor(id)));
    }

    [Fact]
    public async Task Training_against_a_dataset_that_is_gone_fails_before_a_run_is_recorded()
    {
        // Nothing was attempted, so there is nothing to record — an empty run in the history would
        // just be noise to scroll past.
        var service = Service();
        service.StartChild = ChildPrinting(Result);

        var act = () => service.TrainAsync(Guid.NewGuid(), "failed", MlTaskType.BinaryClassification);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _runs.List().Should().BeEmpty();
    }

    [Fact]
    public async Task A_child_that_cannot_be_started_is_reported_rather_than_swallowed()
    {
        var id = await SeedDatasetAsync();
        var service = Service();
        service.StartChild = _ => null;

        var run = await service.TrainAsync(id, "failed", MlTaskType.BinaryClassification);

        run.Outcome.Should().Be(LocalRunOutcome.Failed);
        run.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Progress_reaches_a_listener_and_ends_in_Done()
    {
        var id = await SeedDatasetAsync();
        var service = Service();
        service.StartChild = ChildPrinting(Trial1, Trial2, Result);

        var seen = new List<TrainingProgress>();
        service.Progress += p => { lock (seen) { seen.Add(p); } };

        await service.TrainAsync(id, "failed", MlTaskType.BinaryClassification);

        lock (seen)
        {
            seen.Should().NotBeEmpty();
            seen[^1].Phase.Should().Be(TrainingPhase.Done);
            seen.Last(p => p.Best is not null).Best!.TrainerName
                .Should().Be("LightGbm", "the highest score wins, not the last one");
        }
    }

    [Fact]
    public void Limits_are_clamped_so_a_hand_edited_settings_file_cannot_disable_them()
    {
        new LocalTrainingLimits { MaxMemoryMb = 0, MaxSecondsPerExperiment = 0 }.Clamped()
            .Should().BeEquivalentTo(new { MaxMemoryMb = 512, MaxSecondsPerExperiment = 10 });

        new LocalTrainingLimits { MaxMemoryMb = 999_999, MaxSecondsPerExperiment = 999_999 }.Clamped()
            .Should().BeEquivalentTo(new { MaxMemoryMb = 16384, MaxSecondsPerExperiment = 3600 });
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(25);
        }

        condition().Should().BeTrue("the run should have started within five seconds");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
    }
}
