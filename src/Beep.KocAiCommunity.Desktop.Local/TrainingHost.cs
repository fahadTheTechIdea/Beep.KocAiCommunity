using System.Text.Json;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.ML;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>What the parent asks a training child process to do.</summary>
public sealed record TrainingJob
{
    public required string CsvPath { get; init; }
    public required string TargetColumn { get; init; }
    public required string Task { get; init; }
    public required int MaxSeconds { get; init; }

    /// <summary>Where to write <c>model.zip</c> if a model is produced. The parent owns this folder.</summary>
    public required string OutputDirectory { get; init; }
}

/// <summary>
/// One line of the child's stdout. Deliberately flat and small: this crosses a process boundary and is
/// the only channel, so it has to survive being read a line at a time.
/// </summary>
public sealed record TrainingMessage
{
    /// <summary>"trial", "result" or "error".</summary>
    public required string Type { get; init; }

    public int TrialNumber { get; init; }
    public string? TrainerName { get; init; }
    public string? MetricName { get; init; }
    public double MetricValue { get; init; }
    public double RuntimeSeconds { get; init; }

    public string? Algorithm { get; init; }
    public string? PrimaryMetric { get; init; }
    public double PrimaryValue { get; init; }
    public string? SecondaryMetric { get; init; }
    public double SecondaryValue { get; init; }
    public long RowCount { get; init; }

    public string? Message { get; init; }
}

/// <summary>
/// The child half of a training run: reads a job, trains, prints progress, exits.
/// <para>
/// Training lives in its own process because that is the only thing in reach that can actually be
/// stopped. ML.NET's AutoML in this version ignores a cancellation token once an experiment is running —
/// measured, not assumed: a thirty-second experiment cancelled after one second still ran the full
/// thirty. In-process, then, Stop cannot stop and a memory ceiling cannot be enforced; both would be
/// buttons that lie. A child process can be killed, and killing it returns its memory to the machine.
/// </para>
/// <para>
/// This is Phase 04's stated fallback ("consider a child process — heavier, but it can be killed"),
/// reached because the lighter option was measured and did not work.
/// </para>
/// </summary>
public static class TrainingHost
{
    /// <summary>The switch that turns the desktop executable into a training child.</summary>
    public const string CommandLineSwitch = "--train";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Reads the job file, trains, and writes one JSON message per line to <paramref name="output"/>.</summary>
    public static async Task<int> RunAsync(string jobPath, TextWriter output, CancellationToken ct = default)
    {
        TrainingJob job;
        try
        {
            job = JsonSerializer.Deserialize<TrainingJob>(await File.ReadAllTextAsync(jobPath, ct), Json)
                  ?? throw new InvalidOperationException("The training job file was empty.");
        }
        catch (Exception ex)
        {
            await WriteAsync(output, new TrainingMessage { Type = "error", Message = ex.Message });
            return 2;
        }

        try
        {
            var task = Enum.TryParse<MlTaskType>(job.Task, ignoreCase: true, out var parsed)
                ? parsed
                : MlTaskType.BinaryClassification;

            // Reported as they complete, so the parent can show progress and record the attempts even
            // when this process is killed a moment later.
            var trials = new Progress<TrialReport>(t => WriteAsync(output, new TrainingMessage
            {
                Type = "trial",
                TrialNumber = t.TrialNumber,
                TrainerName = t.TrainerName,
                MetricName = t.MetricName,
                MetricValue = t.MetricValue,
                RuntimeSeconds = t.RuntimeSeconds,
            }).GetAwaiter().GetResult());

            await using var csv = File.OpenRead(job.CsvPath);
            var captured = await new AutoMlTrainer()
                .TrainAndCaptureAsync(task, csv, job.TargetColumn, job.MaxSeconds, trials, ct);

            Directory.CreateDirectory(job.OutputDirectory);
            await File.WriteAllBytesAsync(
                Path.Combine(job.OutputDirectory, "model.zip"), captured.ModelBytes, ct);

            var r = captured.Result;
            await WriteAsync(output, new TrainingMessage
            {
                Type = "result",
                Algorithm = r.Algorithm,
                PrimaryMetric = r.PrimaryMetric,
                PrimaryValue = r.PrimaryValue,
                SecondaryMetric = r.SecondaryMetric,
                SecondaryValue = r.SecondaryValue,
                RowCount = r.RowCount,
            });
            return 0;
        }
        catch (Exception ex)
        {
            // The parent has no other way to learn why. An exit code alone would leave the run history
            // saying "failed" with nothing after the colon.
            await WriteAsync(output, new TrainingMessage { Type = "error", Message = ex.Message });
            return 1;
        }
    }

    /// <summary>Serializes to a single line — the parent reads this stream one line at a time.</summary>
    private static async Task WriteAsync(TextWriter output, TrainingMessage message)
    {
        await output.WriteLineAsync(JsonSerializer.Serialize(message, Json));
        await output.FlushAsync();
    }

    /// <summary>Parses one line of a child's stdout, or null if it is not one of ours.</summary>
    public static TrainingMessage? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TrainingMessage>(line, Json);
        }
        catch (JsonException)
        {
            // ML.NET and the runtime both write to stdout uninvited. Anything unrecognisable is noise.
            return null;
        }
    }
}
