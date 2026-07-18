using System.Net;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class HealthEndpointTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Health_returns_200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Api_v1_ping_returns_200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/ping");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
