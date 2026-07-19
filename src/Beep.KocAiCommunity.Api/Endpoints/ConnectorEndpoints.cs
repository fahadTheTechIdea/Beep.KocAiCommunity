using Beep.KocAiCommunity.Application.Connectors;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Connectors;

namespace Beep.KocAiCommunity.Api.Endpoints;

/// <summary>KOC enterprise connectors: catalog, instances, encrypted credentials, test/schema/health. Admin-only.</summary>
public static class ConnectorEndpoints
{
    public static RouteGroupBuilder MapConnectorEndpoints(this RouteGroupBuilder group)
    {
        var connectors = group.MapGroup("/connectors").RequireAuthorization(KocPolicies.RequirePlatformAdmin);

        connectors.MapGet("", () => Results.Ok(ConnectorCatalog.All.Select(c => new ConnectorDescriptorDto(
            c.Code, c.DisplayName, c.Version, c.Description, c.DefaultClassification.ToString(),
            [.. c.AuthModes.Select(a => a.ToString())],
            new ConnectorCapabilitiesDto(c.Capabilities.Schema, c.Capabilities.Browse, c.Capabilities.Import, c.Capabilities.ReadOnly))).ToList()))
        .WithName("ListConnectors");

        connectors.MapGet("/{code}/instances", async (string code, IConnectorService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListInstancesAsync(code, ct)).Select(ToInstanceDto).ToList()))
        .WithName("ListConnectorInstances");

        connectors.MapPost("/{code}/instances", async (string code, CreateConnectorInstanceRequest req, IKocCurrentUser me, IConnectorService svc, CancellationToken ct) =>
        {
            try
            {
                var instance = await svc.CreateInstanceAsync(me.UserId!, code, req.Name, req.Endpoint, req.AuthMode, req.Classification, ct);
                return Results.Ok(ToInstanceDto(instance));
            }
            catch (ConnectorException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateConnectorInstance");

        connectors.MapGet("/instances/{id:guid}", async (Guid id, IConnectorService svc, CancellationToken ct) =>
        {
            var d = await svc.GetInstanceAsync(id, ct);
            return d is null
                ? Results.NotFound()
                : Results.Ok(new ConnectorInstanceDetailDto(
                    ToInstanceDto(d.Instance),
                    [.. d.Credentials.Select(c => new CredentialInfoDto(c.Key, c.LastRotatedUtc, c.ExpiresUtc))],
                    d.LatestHealth is null ? null : new ConnectorHealthDto(d.LatestHealth.Status, d.LatestHealth.LatencyMs, d.LatestHealth.Detail, d.LatestHealth.MeasuredUtc)));
        })
        .WithName("GetConnectorInstance");

        connectors.MapDelete("/instances/{id:guid}", (Guid id, IKocCurrentUser me, IConnectorService svc, CancellationToken ct) =>
            Guard(async () => { await svc.DeleteInstanceAsync(me.UserId!, id, ct); }))
        .WithName("DeleteConnectorInstance");

        connectors.MapPost("/instances/{id:guid}/credentials", async (Guid id, SetCredentialRequest req, IKocCurrentUser me, IConnectorService svc, CancellationToken ct) =>
        {
            try
            {
                var info = await svc.SetCredentialAsync(me.UserId!, id, req.Key, req.Value, req.ExpiresUtc, ct);
                return Results.Ok(new CredentialInfoDto(info.Key, info.LastRotatedUtc, info.ExpiresUtc));
            }
            catch (ConnectorException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SetConnectorCredential");

        connectors.MapDelete("/instances/{id:guid}/credentials/{key}", (Guid id, string key, IKocCurrentUser me, IConnectorService svc, CancellationToken ct) =>
            Guard(async () => { await svc.DeleteCredentialAsync(me.UserId!, id, key, ct); }))
        .WithName("DeleteConnectorCredential");

        connectors.MapPost("/instances/{id:guid}/test", async (Guid id, IConnectorService svc, CancellationToken ct) =>
        {
            try
            {
                var r = await svc.TestAsync(id, ct);
                return Results.Ok(new ConnectorTestDto(r.Ok, r.Message, r.LatencyMs));
            }
            catch (ConnectorException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("TestConnectorInstance");

        connectors.MapGet("/instances/{id:guid}/schema", async (Guid id, IConnectorService svc, CancellationToken ct) =>
        {
            try
            {
                var s = await svc.GetSchemaAsync(id, ct);
                return Results.Ok(new ConnectorSchemaDto([.. s.Resources.Select(r =>
                    new ConnectorResourceDto(r.Path, r.Name, r.Kind, [.. r.Columns.Select(c => new ConnectorColumnDto(c.Name, c.DataType))]))]));
            }
            catch (ConnectorException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GetConnectorSchema");

        connectors.MapPost("/instances/{id:guid}/health", async (Guid id, IConnectorService svc, CancellationToken ct) =>
        {
            try
            {
                var h = await svc.ProbeHealthAsync(id, ct);
                return Results.Ok(new ConnectorHealthDto(h.Status, h.LatencyMs, h.Detail, h.MeasuredUtc));
            }
            catch (ConnectorException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ProbeConnectorHealth");

        return group;
    }

    private static async Task<IResult> Guard(Func<Task> action)
    {
        try
        {
            await action();
            return Results.NoContent();
        }
        catch (ConnectorException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static ConnectorInstanceDto ToInstanceDto(Domain.Connectors.ConnectorInstance i) =>
        new(i.Id, i.ConnectorCode, i.Name, i.Endpoint, i.AuthMode, i.DefaultClassification.ToString(), i.IsEnabled, i.CreatedUtc);
}
