using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Application.Jobs;
using Beep.KocAiCommunity.Contracts.Jobs;
using Beep.KocAiCommunity.Contracts.Workflow;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// The website does not train.
/// <para>
/// These replace the suites that used to prove the opposite — StudioDatasetTrainingTests, and the
/// training halves of StudioEndpointsTests, WorkflowEndpointsTests and CompetitionEndpointsTests. A
/// removal with no test behind it is an invitation to add the route back the next time somebody wants
/// a quick server-side train, so the absence is asserted rather than assumed.
/// </para>
/// <para>
/// Training happens in KOC Studio on the desktop, on that machine's cores. The platform keeps the
/// record — registering a model, serving predictions from one already registered — and neither of
/// those is training.
/// </para>
/// </summary>
public class NoServerTrainingTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    public static TheoryData<string> RemovedRoutes =>
    [
        "/api/v1/studio/train",
        "/api/v1/studio/train/dataset",
        "/api/v1/studio/workflows/execute",
        "/api/v1/studio/workflows/run",
        "/api/v1/studio/workflows/execute/dataset",
    ];

    [Theory]
    [MemberData(nameof(RemovedRoutes))]
    public async Task No_route_on_this_platform_runs_a_training_pass(string route)
    {
        var client = _factory.CreateClientAs("no-train", "Employee");

        // 404 or 405: sibling routes still live under /studio, so routing matches the path and rejects
        // the method rather than the address. Either answer means there is no such endpoint; what would
        // fail this test is a 200, or a 400 from a handler that ran and merely disliked the input.
        (await client.PostAsync(route, null)).StatusCode
            .Should().BeOneOf([HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed],
                "{0} used to train on the server and is gone", route);
    }

    [Fact]
    public async Task A_competition_cannot_be_entered_with_a_pipeline_for_the_server_to_run()
    {
        // Running a competitor's graph is training. The desktop still submits a graph — it executes it
        // itself and sends the result — so what reaches the platform is a scored submission.
        var client = _factory.CreateClientAs("no-pipeline", "Employee");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/competitions/{Guid.NewGuid()}/submit-pipeline",
            new WorkflowDefinition { Name = "anything", Nodes = [] });

        response.StatusCode.Should().BeOneOf([HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed]);
    }

    [Fact]
    public async Task A_training_job_is_refused_at_the_queue_rather_than_left_to_fail_in_a_worker()
    {
        // The queue takes any job type, so this is the door. Accepting it and letting a worker with no
        // handler fail on it would read as a broken queue rather than a closed door.
        var client = _factory.CreateClientAs("no-train-job", "Employee");

        var response = await client.PostAsJsonAsync("/api/v1/runs",
            new CreateRunRequest(JobTypes.ModelTrain, "sneak a train past the door", "{}", 0));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("does not train");
    }

    [Fact]
    public async Task Other_job_types_still_enqueue()
    {
        // The refusal is one job type, not the queue. A blanket block would have taken the rest with it.
        var client = _factory.CreateClientAs("other-job", "Employee");

        (await client.PostAsJsonAsync("/api/v1/runs",
            new CreateRunRequest("report.generate", "something that is not training", "{}", 0)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
