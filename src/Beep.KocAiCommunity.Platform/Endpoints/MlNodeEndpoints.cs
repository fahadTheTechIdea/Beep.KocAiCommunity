using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.ML;
using Beep.KocAiCommunity.Contracts.Workflow;

namespace Beep.KocAiCommunity.Platform.Endpoints;

/// <summary>The ML node catalog: descriptors, parameter validation, task catalog, and the split-before-fit check.</summary>
public static class MlNodeEndpoints
{
    public static RouteGroupBuilder MapMlNodeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/ml/nodes", (INodeRegistry registry) =>
            Results.Ok(registry.All.Select(ToDto).ToList()))
        .WithName("ListMlNodes").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/ml/nodes/{kind}", (string kind, INodeRegistry registry) =>
        {
            var descriptor = registry.Find(kind);
            return descriptor is null ? Results.NotFound() : Results.Ok(ToDto(descriptor));
        })
        .WithName("GetMlNode").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/ml/nodes/{kind}/validate", (string kind, Dictionary<string, string>? config, INodeRegistry registry) =>
        {
            var result = registry.ValidateParameters(kind, config ?? []);
            return Results.Ok(new ParameterValidationDto(result.IsValid, result.Errors));
        })
        .WithName("ValidateMlNode").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/ml/tasks", () =>
            Results.Ok(MlTaskCatalog.All.Select(t => new MlTaskDto(
                t.Key, t.Task?.ToString(), t.DisplayName, t.PrimaryMetric, t.SecondaryMetric, t.Supported, t.OgExample)).ToList()))
        .WithName("ListMlTasks").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/ml/workflows/featurization-check", (WorkflowDefinition definition) =>
        {
            var violations = FeaturizationGuard.Check(definition);
            return Results.Ok(new FeaturizationCheckDto(violations.Count == 0, violations));
        })
        .WithName("FeaturizationCheck").RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }

    private static NodeDescriptorDto ToDto(NodeDescriptor n) =>
        new(n.Kind, n.Category, n.DisplayName, n.Description, n.Input.ToString(), n.Output.ToString(),
            [.. n.Parameters.Select(ToDto)]);

    private static NodeParameterDto ToDto(NodeParameter p) =>
        new(p.Name, p.DisplayName, p.Type.ToString(), p.Required, p.Default,
            p.Options is null ? null : [.. p.Options.Select(o => new LookupOptionDto(o.Value, o.Label, o.AppliesTo))],
            p.Min, p.Max, p.Help,
            p.VisibleWhen is null ? null : new VisibleWhenDto(p.VisibleWhen.Param, p.VisibleWhen.Values));
}
