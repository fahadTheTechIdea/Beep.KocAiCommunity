using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Studio;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// What is left of Studio on the server: reading the run history the desktop writes.
/// <para>
/// This used to prove that uploading a CSV trained a model here. It does not any more — the training
/// half moved to the desktop and the route is gone, asserted in <see cref="NoServerTrainingTests"/>.
/// The read stays covered, because it is the half that survived.
/// </para>
/// </summary>
public class StudioEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Run_history_reads_back_empty_for_somebody_who_has_trained_nothing()
    {
        var client = _factory.CreateClientAs("ml-reader", "Employee");

        var runs = await client.GetFromJsonAsync<List<ModelRunDto>>("/api/v1/studio/runs");

        runs.Should().NotBeNull("the history is readable even when there is none of it")
            .And.BeEmpty();
    }

    [Fact]
    public async Task Run_history_needs_an_account()
    {
        (await _factory.CreateClientAs(sub: null).GetAsync("/api/v1/studio/runs"))
            .StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}
