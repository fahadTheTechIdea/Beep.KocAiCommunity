using Beep.KocAiCommunity.Application.Datasets;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Datasets;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Datasets;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class DatasetEndpoints
{
    public static RouteGroupBuilder MapDatasetEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/datasets", async (CreateDatasetRequest req, IKocCurrentUser me, IDatasetService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse<VisibilityScope>(req.Scope, ignoreCase: true, out var scope))
            {
                return Results.BadRequest(new { error = $"Unknown visibility scope '{req.Scope}'." });
            }

            if (!Enum.TryParse<KocDataClassification>(req.Classification, ignoreCase: true, out var classification))
            {
                classification = KocDataClassification.Internal;
            }

            try
            {
                var dataset = await svc.CreateAsync(me.UserId!, req.Name, req.Description, scope, classification,
                    string.IsNullOrWhiteSpace(req.Domain) ? "upstream" : req.Domain, req.Tags, ct);
                return Results.Ok(ToDto(dataset));
            }
            catch (DatasetException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateDataset")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/datasets", async (IKocCurrentUser me, IDatasetService svc, CancellationToken ct) =>
        {
            var visible = await svc.BrowseVisibleAsync(me.UserId!, ct);
            return Results.Ok(visible.Select(ToDto).ToList());
        })
        .WithName("BrowseDatasets")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/datasets/{id:guid}", async (Guid id, IKocCurrentUser me, IDatasetService svc, CancellationToken ct) =>
        {
            var dataset = await svc.GetVisibleAsync(me.UserId!, id, ct);
            return dataset is null ? Results.NotFound() : Results.Ok(ToDto(dataset));
        })
        .WithName("GetDataset")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // --- Versioned contents: files, schema, profiling, imports, download ---

        group.MapPost("/datasets/{id:guid}/files", async (Guid id, IFormFile file, IKocCurrentUser me, IDatasetContentService svc, CancellationToken ct) =>
        {
            try
            {
                await using var stream = file.OpenReadStream();
                var v = await svc.UploadCsvAsync(me.UserId!, IsAdmin(me), id, stream, file.FileName, file.ContentType ?? "text/csv", ct);
                return Results.Ok(ToVersionDto(v));
            }
            catch (DatasetException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("UploadDatasetFile")
        .RequireAuthorization(KocPolicies.RequireEmployee)
        .DisableAntiforgery();

        group.MapPost("/datasets/{id:guid}/imports", async (Guid id, ImportUrlRequest req, IKocCurrentUser me, IDatasetContentService svc, CancellationToken ct) =>
        {
            try
            {
                var v = await svc.ImportFromUrlAsync(me.UserId!, IsAdmin(me), id, req.Url, ct);
                return Results.Ok(ToVersionDto(v));
            }
            catch (DatasetException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ImportDatasetFromUrl")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/datasets/{id:guid}/versions", async (Guid id, IKocCurrentUser me, IDatasetContentService svc, CancellationToken ct) =>
        {
            try
            {
                var versions = await svc.ListVersionsAsync(me.UserId!, id, ct);
                return Results.Ok(versions.Select(ToVersionDto).ToList());
            }
            catch (DatasetException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ListDatasetVersions")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/datasets/{id:guid}/versions/{n:int}", async (Guid id, int n, IKocCurrentUser me, IDatasetContentService svc, CancellationToken ct) =>
        {
            var detail = await svc.GetVersionAsync(me.UserId!, id, n, ct);
            if (detail is null)
            {
                return Results.NotFound();
            }

            var v = detail.Version;
            var profile = detail.Profile is null
                ? null
                : new DatasetProfileDto(detail.Profile.SampledRows, detail.Profile.TotalRows, detail.Profile.GeneratedUtc,
                    [.. detail.ProfileColumns.Select(c => new DatasetProfileColumnDto(c.ColumnName, c.NullCount, c.DistinctCount, c.Min, c.Max, c.Mean))]);

            return Results.Ok(new DatasetVersionDetailDto(
                ToVersionDto(v),
                [.. detail.Files.Select(f => new DatasetFileDto(f.Id, f.LogicalPath, f.ContentType, f.SizeBytes, f.RowCount))],
                [.. detail.Schema.Select(s => new DatasetSchemaColumnDto(s.Ordinal, s.ColumnName, s.DataType, s.Nullable))],
                profile));
        })
        .WithName("GetDatasetVersion")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/datasets/{id:guid}/versions/{n:int}/publish", (Guid id, int n, IKocCurrentUser me, IDatasetContentService svc, CancellationToken ct) =>
            VersionAction(() => svc.PublishVersionAsync(me.UserId!, IsAdmin(me), id, n, ct)))
        .WithName("PublishDatasetVersion").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/datasets/{id:guid}/versions/{n:int}/archive", (Guid id, int n, IKocCurrentUser me, IDatasetContentService svc, CancellationToken ct) =>
            VersionAction(() => svc.ArchiveVersionAsync(me.UserId!, IsAdmin(me), id, n, ct)))
        .WithName("ArchiveDatasetVersion").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/datasets/files/{fileId:guid}/download", async (Guid fileId, IKocCurrentUser me, IDatasetContentService svc, CancellationToken ct) =>
        {
            try
            {
                var d = await svc.DownloadFileAsync(me.UserId!, IsAdmin(me), fileId, ct);
                return Results.File(d.Content, d.ContentType, d.FileName);
            }
            catch (DatasetException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("DownloadDatasetFile")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }

    private static bool IsAdmin(IKocCurrentUser me) => me.IsInRole(KocRoles.PlatformAdmin);

    private static async Task<IResult> VersionAction(Func<Task<Domain.Datasets.DatasetVersion>> action)
    {
        try
        {
            var v = await action();
            return Results.Ok(ToVersionDto(v));
        }
        catch (DatasetException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static DatasetVersionDto ToVersionDto(Domain.Datasets.DatasetVersion v) =>
        new(v.VersionNumber, v.Status, v.TotalSizeBytes, v.Sha256, v.Notes, v.PublishedUtc, v.CreatedUtc);

    private static DatasetDto ToDto(Dataset d) =>
        new(d.Id, d.Name, d.Description, d.VisibilityScope.ToString(), d.Classification.ToString(), d.Domain, d.OwnerUserId, d.FileArtifactId is not null);
}
