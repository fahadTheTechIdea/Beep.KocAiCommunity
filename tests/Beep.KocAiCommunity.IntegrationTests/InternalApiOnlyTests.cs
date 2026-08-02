using System.Net;
using Beep.KocAiCommunity.Platform;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// The API surface is for this machine only.
/// <para>
/// It was a public website until the merge on 2026-08-02. Now the website carries it in-process and
/// KOC Studio reads the database directly, so nothing off this machine calls it — and an API left
/// reachable for a caller that no longer exists is attack surface maintained for nobody.
/// </para>
/// <para>
/// In a real deployment the surface is mapped on a loopback-only listener, so it is not routable from
/// the public port at all. A test server has no ports, so these exercise the second guard: the check
/// on where the caller came from.
/// </para>
/// </summary>
public class InternalApiOnlyTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    /// <summary>
    /// Drives the pipeline directly so the caller's address can be set — an ordinary HttpClient through
    /// the test server leaves it null, which the guard reads as in-process.
    /// </summary>
    private async Task<int> StatusFromAsync(IPAddress? caller, string path)
    {
        var context = await _factory.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Path = path;
            c.Connection.RemoteIpAddress = caller;
            c.Connection.LocalIpAddress = IPAddress.Loopback;
        });

        return context.Response.StatusCode;
    }

    [Fact]
    public async Task A_call_from_this_machine_reaches_the_surface()
    {
        // The website calls its own surface over loopback; closing that would close the website.
        var status = await StatusFromAsync(IPAddress.Loopback, "/api/v1/ping");

        status.Should().NotBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task An_in_process_call_reaches_the_surface()
    {
        // No socket, no address — an in-process host. As local as a call gets.
        var status = await StatusFromAsync(null, "/api/v1/ping");

        status.Should().NotBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task A_call_from_elsewhere_on_the_network_is_refused()
    {
        var status = await StatusFromAsync(IPAddress.Parse("10.20.30.40"), "/api/v1/ping");

        // 404 rather than 403: a refusal advertises that there is something here to find.
        status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task The_leaderboard_hub_stays_reachable_from_a_workstation()
    {
        // The one part of the surface with a real remote caller: KOC Studio subscribes to live
        // standings from an engineer's machine. Closing this would take that with it.
        var status = await StatusFromAsync(IPAddress.Parse("10.20.30.40"), PlatformApi.HubPath);

        status.Should().NotBe(StatusCodes.Status404NotFound);
    }
}
