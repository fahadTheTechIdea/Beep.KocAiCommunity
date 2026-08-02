using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Contracts.Datasets;
using Beep.KocAiCommunity.Contracts.ML;
using Beep.KocAiCommunity.Contracts.Workflow;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>
/// The desktop's <see cref="IKocApiClient"/>: the Studio surface (node catalog, datasets, pipeline
/// runs, workflow registry) runs in-process and offline; everything else (competitions, /me, …)
/// is inherited from <see cref="RemoteFallbackKocApiClient"/> and flows to the HTTP API.
/// </summary>
public sealed class LocalKocApiClient(
    IKocApiClient remote,
    INodeRegistry registry,
    IPipelineExecutor executor,
    LocalDatasetStore datasets,
    LocalWorkflowStore workflows,
    LocalCompetitionCache cache,
    SubmissionOutbox outbox,
    ConnectionState connection) : RemoteFallbackKocApiClient(remote)
{
    // ---- Competitions: cached for reading, queued for writing ----

    /// <summary>
    /// The competition list, from the network when it is there and from the cache when it is not.
    /// <para>
    /// The age comes back through <see cref="LastCompetitionsFetchUtc"/> rather than the return value,
    /// because this implements a shared interface the Web also uses. The desktop's own pages read it
    /// and say how old the list is — a cached leaderboard shown as current is worse than none.
    /// </para>
    /// </summary>
    public override async Task<IReadOnlyList<CompetitionDto>> GetCompetitionsAsync(CancellationToken ct = default)
    {
        var cached = await cache.ThroughAsync<IReadOnlyList<CompetitionDto>>(
            LocalCompetitionCache.CompetitionsKey, async c => await Remote.GetCompetitionsAsync(c), ct);

        LastCompetitionsFetchUtc = cached?.FetchedUtc;
        return cached?.Value ?? [];
    }

    /// <summary>When the competition list currently on screen was actually fetched.</summary>
    public DateTime? LastCompetitionsFetchUtc { get; private set; }

    public override async Task<CompetitionDto?> GetCompetitionAsync(Guid competitionId, CancellationToken ct = default)
    {
        var cached = await cache.ThroughAsync<CompetitionDto>(
            LocalCompetitionCache.CompetitionKey(competitionId),
            async c => await Remote.GetCompetitionAsync(competitionId, c), ct);

        return cached?.Value;
    }

    /// <summary>
    /// Submits, or queues it when that is not possible.
    /// <para>
    /// The work is already done by this point — the pipeline ran and the score is known — so failing
    /// because the network happened to be absent at that moment throws it away. The queued entry's id
    /// is its idempotency key, which is what makes the replay safe against the daily quota.
    /// </para>
    /// </summary>
    public override async Task<SubmissionResultDto?> SubmitAsync(
        Guid competitionId, Stream csv, string fileName, string? idempotencyKey = null, CancellationToken ct = default)
    {
        var key = idempotencyKey ?? Guid.NewGuid().ToString();

        // Buffer first: the stream is read either by the API or by the outbox, and on a failed send it
        // has already been consumed.
        using var buffer = new MemoryStream();
        await csv.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        try
        {
            var result = await Remote.SubmitAsync(competitionId, buffer, fileName, key, ct);
            connection.Status = Connectivity.Online;
            return result;
        }
        catch (Exception)
        {
            connection.Status = Connectivity.Offline;
        }

        buffer.Position = 0;
        var competition = (await cache.ReadAsync<CompetitionDto>(
            LocalCompetitionCache.CompetitionKey(competitionId), ct))?.Value;

        await outbox.EnqueueAsync(new QueuedSubmission
        {
            Id = key,
            CompetitionId = competitionId,
            CompetitionTitle = competition?.Title ?? "this competition",
            QueuedUtc = DateTime.UtcNow,
            CompetitionStatusWhenQueued = competition?.Status,
        }, buffer, ct);

        connection.Queued = outbox.Pending().Count;

        // No submission id and no score, because there is no submission yet. Returning a fabricated one
        // would have the UI report a result that does not exist.
        return null;
    }

    // ---- Node catalog + tasks (local) ----

    public override Task<IReadOnlyList<NodeDescriptorDto>> GetMlNodesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<NodeDescriptorDto>>([.. registry.All.Select(ToDto)]);

    public override Task<IReadOnlyList<MlTaskDto>> GetMlTasksAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MlTaskDto>>([.. MlTaskCatalog.All.Select(t =>
            new MlTaskDto(t.Key, t.Task?.ToString(), t.DisplayName, t.PrimaryMetric, t.SecondaryMetric, t.Supported, t.OgExample))]);

    // ---- Datasets (local CSV files) ----

    public override Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(CancellationToken ct = default) =>
        Task.FromResult(datasets.List());

    // ---- Pipeline execution (in-process) ----

    public override async Task<(PipelineExecutionResult? Result, string? Error)> ExecuteWorkflowFromDatasetAsync(
        WorkflowDefinition definition, Guid datasetId, string labelColumn, string task, CancellationToken ct = default)
    {
        var path = datasets.PathFor(datasetId);
        if (path is null)
        {
            return (null, "That dataset is not in your local workspace.");
        }

        // Running against it is what "recently used" means, and it keeps the file being worked on at
        // the top of the list instead of alphabetised away from its owner.
        datasets.MarkUsed(datasetId);

        await using var csv = File.OpenRead(path);
        return await RunLocalAsync(definition, csv, labelColumn, task, datasetId, ct);
    }

    public override async Task<PipelineExecutionResult?> ExecuteWorkflowAsync(
        WorkflowDefinition definition, Stream csv, string fileName, string labelColumn, string task, CancellationToken ct = default)
    {
        var (result, _) = await RunLocalAsync(definition, csv, labelColumn, task, primaryDatasetId: null, ct);
        return result;
    }

    private async Task<(PipelineExecutionResult?, string?)> RunLocalAsync(
        WorkflowDefinition definition, Stream csv, string labelColumn, string task, Guid? primaryDatasetId, CancellationToken ct)
    {
        var mlTask = Enum.TryParse<MlTaskType>(task, ignoreCase: true, out var parsed) ? parsed : MlTaskType.BinaryClassification;
        var secondary = new Dictionary<Guid, Stream>();
        try
        {
            // Join/union nodes reference other local datasets by id — open each from the workspace.
            foreach (var id in WorkflowDatasetScanner.ReferencedDatasetIds(definition))
            {
                if ((primaryDatasetId is { } pid && id == pid) || secondary.ContainsKey(id))
                {
                    continue;
                }

                if (datasets.PathFor(id) is { } p)
                {
                    secondary[id] = File.OpenRead(p);
                }
            }

            var result = await executor.ExecuteAsync(
                definition, labelColumn, mlTask, csv, maxSeconds: 60, secondary.Count > 0 ? secondary : null, ct);
            return (result, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
        finally
        {
            foreach (var s in secondary.Values)
            {
                await s.DisposeAsync();
            }
        }
    }

    // ---- Workflow registry (local JSON store) ----

    public override Task<IReadOnlyList<WorkflowSummaryDto>> GetWorkflowsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<WorkflowSummaryDto>>([.. workflows.All().Select(ToSummary)]);

    public override Task<(WorkflowSummaryDto? Workflow, string? Error)> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default) =>
        Task.FromResult<(WorkflowSummaryDto?, string?)>((ToSummary(workflows.Create(request.Name, request.Description, request.Classification, request.CompetitionId)), null));

    public override Task<WorkflowDetailDto?> GetWorkflowDetailAsync(Guid id, CancellationToken ct = default)
    {
        var wf = workflows.Get(id);
        return Task.FromResult(wf is null ? null : new WorkflowDetailDto(
            wf.Id, wf.Name, wf.Description, wf.OwnerUserId, wf.Classification, wf.Latest.VersionNumber,
            [.. wf.Versions.OrderBy(v => v.VersionNumber).Select(ToVersionDto)], wf.CompetitionId));
    }

    public override Task<WorkflowVersionDetailDto?> GetWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default)
    {
        var v = workflows.Get(id)?.Versions.FirstOrDefault(x => x.VersionNumber == versionNumber);
        return Task.FromResult(v is null ? null : new WorkflowVersionDetailDto(
            v.VersionNumber, v.Status, v.SchemaVersion, v.DefinitionJson, v.SnapshotHash, v.Notes));
    }

    public override Task<(WorkflowVersionDto? Version, string? Error)> SaveWorkflowDraftAsync(Guid id, SaveDraftRequest request, CancellationToken ct = default)
    {
        try
        {
            return Task.FromResult<(WorkflowVersionDto?, string?)>((ToVersionDto(workflows.SaveDraft(id, request.DefinitionJson, request.Notes)), null));
        }
        catch (Exception ex)
        {
            return Task.FromResult<(WorkflowVersionDto?, string?)>((null, ex.Message));
        }
    }

    public override Task<string?> PublishWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default) =>
        Task.FromResult(workflows.SetStatus(id, versionNumber, "published"));

    public override Task<string?> ArchiveWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default) =>
        Task.FromResult(workflows.SetStatus(id, versionNumber, "archived"));

    public override Task<string?> DeleteWorkflowAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(workflows.Delete(id) ? null : "Workflow not found.");

    public override Task<WorkflowExportDto?> ExportWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default)
    {
        var v = workflows.Get(id)?.Versions.FirstOrDefault(x => x.VersionNumber == versionNumber);
        return Task.FromResult(v is null ? null : new WorkflowExportDto(v.DefinitionJson));
    }

    public override Task<(WorkflowSummaryDto? Workflow, string? Error)> ImportWorkflowAsync(ImportWorkflowRequest request, CancellationToken ct = default) =>
        Task.FromResult<(WorkflowSummaryDto?, string?)>((ToSummary(workflows.Import(request.Name, request.EnvelopeJson)), null));

    // No local templates / queued runs — those are server features. Keep them offline-safe.
    public override Task<IReadOnlyList<WorkflowTemplateDto>> GetWorkflowTemplatesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<WorkflowTemplateDto>>([]);

    public override Task<(WorkflowSummaryDto? Workflow, string? Error)> InstantiateTemplateAsync(string code, InstantiateTemplateRequest request, CancellationToken ct = default) =>
        Task.FromResult<(WorkflowSummaryDto?, string?)>((null, "Templates are available online only."));

    public override Task<(Guid? RunId, string? Error)> RunWorkflowVersionAsync(Guid id, int versionNumber, RunWorkflowVersionRequest request, CancellationToken ct = default) =>
        Task.FromResult<(Guid?, string?)>((null, "Queued runs need the server — use the Run panel in the designer to run locally."));

    // ---- mappers ----

    private static WorkflowSummaryDto ToSummary(StoredWorkflow w) =>
        new(w.Id, w.Name, w.Description, w.OwnerUserId, w.Classification, w.Latest.VersionNumber, w.Versions.Count, w.Latest.Status, w.CreatedUtc, w.CompetitionId);

    private static WorkflowVersionDto ToVersionDto(StoredVersion v) =>
        new(v.VersionNumber, v.Status, v.SchemaVersion, v.SnapshotHash, v.Notes, v.PublishedUtc, v.CreatedUtc);

    private static NodeDescriptorDto ToDto(NodeDescriptor n) =>
        new(n.Kind, n.Category, n.DisplayName, n.Description, n.Input.ToString(), n.Output.ToString(),
            [.. n.Parameters.Select(ToDto)]);

    private static NodeParameterDto ToDto(NodeParameter p) =>
        new(p.Name, p.DisplayName, p.Type.ToString(), p.Required, p.Default,
            p.Options is null ? null : [.. p.Options.Select(o => new LookupOptionDto(o.Value, o.Label, o.AppliesTo))],
            p.Min, p.Max, p.Help,
            p.VisibleWhen is null ? null : new VisibleWhenDto(p.VisibleWhen.Param, p.VisibleWhen.Values));
}
