using Beep.KocAiCommunity.Infrastructure.Jobs;

namespace Beep.KocAiCommunity.Worker;

/// <summary>Tunable worker limits. SQLite is single-writer, so concurrency defaults to 1.</summary>
public sealed class JobExecutionOptions
{
    public const string SectionName = "Jobs";

    /// <summary>How many jobs this worker runs at once. SQLite = 1; SQL Server can raise this.</summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>How long to wait before polling again when the queue is empty.</summary>
    public int IdlePollSeconds { get; set; } = 3;
}

/// <summary>
/// The Worker's run loop. Runs <c>MaxConcurrency</c> independent lease-and-execute loops; each idles
/// when the queue is empty and drains its in-flight job on shutdown. Every job runs in its own DI
/// scope so its DbContext and handlers are isolated.
/// </summary>
public sealed class JobExecutionService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<JobExecutionService> logger) : BackgroundService
{
    // A stable-per-process worker id; distinguishes lease owners across worker instances.
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = ReadOptions();
        logger.LogInformation("Worker {WorkerId} started: max concurrency {Max}.", _workerId, options.MaxConcurrency);

        var loops = Enumerable.Range(0, options.MaxConcurrency)
            .Select(_ => RunLoopAsync(options, stoppingToken))
            .ToArray();

        await Task.WhenAll(loops);
        logger.LogInformation("Worker {WorkerId} stopped.", _workerId);
    }

    private async Task RunLoopAsync(JobExecutionOptions options, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            bool didWork;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<JobProcessor>();
                didWork = await processor.ProcessOneAsync(_workerId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in worker loop.");
                didWork = false;
            }

            if (!didWork)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(options.IdlePollSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private JobExecutionOptions ReadOptions()
    {
        var options = new JobExecutionOptions();
        configuration.GetSection(JobExecutionOptions.SectionName).Bind(options);
        if (options.MaxConcurrency < 1)
        {
            options.MaxConcurrency = 1;
        }

        return options;
    }
}
