using System.Text.Json;
using Beep.KocAiCommunity.Desktop.Local;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The line protocol between the desktop and its training child.
/// <para>
/// It is the only channel between the two processes, and both halves are read a line at a time from a
/// stream that other things also write to. What matters is that noise is ignored and that a child which
/// cannot even start still manages to say why — an exit code alone leaves the run history reporting
/// "failed" with nothing after the colon.
/// </para>
/// </summary>
public sealed class TrainingHostTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "koc-host-" + Guid.NewGuid().ToString("N"));

    public TrainingHostTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Noise_on_the_stream_is_ignored()
    {
        // ML.NET and the .NET runtime both write to stdout uninvited.
        TrainingHost.Parse(null).Should().BeNull();
        TrainingHost.Parse("").Should().BeNull();
        TrainingHost.Parse("Info: loading dataset").Should().BeNull();
        TrainingHost.Parse("{ this is not json").Should().BeNull();
    }

    [Fact]
    public void A_trial_line_round_trips()
    {
        var line = JsonSerializer.Serialize(
            new TrainingMessage
            {
                Type = "trial",
                TrialNumber = 4,
                TrainerName = "LightGbm",
                MetricName = "Accuracy",
                MetricValue = 0.93,
                RuntimeSeconds = 2.4,
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var parsed = TrainingHost.Parse(line);

        parsed.Should().NotBeNull();
        parsed!.TrialNumber.Should().Be(4);
        parsed.TrainerName.Should().Be("LightGbm");
        parsed.MetricValue.Should().Be(0.93);
    }

    [Fact]
    public async Task An_unreadable_job_is_reported_on_the_stream_not_just_in_the_exit_code()
    {
        var output = new StringWriter();

        var exitCode = await TrainingHost.RunAsync(Path.Combine(_root, "no-such-job.json"), output);

        exitCode.Should().NotBe(0);
        TrainingHost.Parse(output.ToString().Trim()).Should()
            .NotBeNull().And.Match<TrainingMessage>(m => m.Type == "error" && !string.IsNullOrEmpty(m.Message));
    }

    [Fact]
    public async Task A_job_pointing_at_a_missing_csv_fails_with_a_message()
    {
        var jobPath = Path.Combine(_root, "job.json");
        await File.WriteAllTextAsync(jobPath, JsonSerializer.Serialize(new TrainingJob
        {
            CsvPath = Path.Combine(_root, "gone.csv"),
            TargetColumn = "failed",
            Task = "BinaryClassification",
            MaxSeconds = 5,
            OutputDirectory = _root,
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var output = new StringWriter();

        var exitCode = await TrainingHost.RunAsync(jobPath, output);

        exitCode.Should().Be(1);
        TrainingHost.Parse(output.ToString().Trim())!.Type.Should().Be("error");
        File.Exists(Path.Combine(_root, "model.zip")).Should().BeFalse("no model was produced");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
    }
}
