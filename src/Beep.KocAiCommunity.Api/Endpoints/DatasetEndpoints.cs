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

        return group;
    }

    private static DatasetDto ToDto(Dataset d) =>
        new(d.Id, d.Name, d.Description, d.VisibilityScope.ToString(), d.Classification.ToString(), d.Domain, d.OwnerUserId, d.FileArtifactId is not null);
}
