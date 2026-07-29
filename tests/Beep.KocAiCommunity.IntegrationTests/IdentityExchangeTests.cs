using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Identity;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// Inside KOC, IIS authenticates the browser's hop to the Web — not the Web's hop to the API. The Web
/// vouches for the verified account here, and that vouching has to be authenticated or anyone who can
/// reach the API could name themselves. It proves it holds the key both processes were given.
/// </summary>
public class IdentityExchangeTests
{
    /// <summary>The key <see cref="KocApiFactory"/> hands the host; the Web would read it from the setup file.</summary>
    private const string SharedKey = "dGVzdC1zaWduaW5nLWtleS0zMi1ieXRlcy1sb25nISEhIQ==";

    [Fact]
    public async Task A_vouched_for_corporate_account_receives_a_working_token()
    {
        using var factory = new RealTokenApiFactory();
        var client = factory.CreateAnonymousClient();

        var auth = await Exchange(client, @"KOC\aldhubaib", SharedKey, DateTimeOffset.UtcNow);
        auth.Should().NotBeNull();
        auth!.UserId.Should().Be(@"KOC\aldhubaib");
        auth.Roles.Should().Contain("PlatformAdmin", "the first arrival on an empty install administers it");

        // The token works on an ordinary endpoint, so the corporate visitor is a full citizen.
        var authenticated = factory.CreateAnonymousClient();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var me = await authenticated.GetFromJsonAsync<MeResponse>("/api/v1/me");
        me!.UserId.Should().Be(@"KOC\aldhubaib");
    }

    [Fact]
    public async Task A_caller_without_the_shared_key_cannot_vouch_for_anyone()
    {
        using var factory = new RealTokenApiFactory();
        var client = factory.CreateAnonymousClient();

        // Exactly the attack the signature exists to stop: name yourself an administrator.
        var response = await Post(client, @"KOC\intruder", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=", DateTimeOffset.UtcNow);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // And with no proof at all.
        var bare = await client.PostAsJsonAsync("/api/v1/auth/exchange", new ExchangeIdentityRequest(@"KOC\intruder"));
        bare.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_stale_proof_is_refused_so_one_cannot_be_replayed_later()
    {
        using var factory = new RealTokenApiFactory();
        var client = factory.CreateAnonymousClient();

        var old = DateTimeOffset.UtcNow - IdentityExchange.MaxSkew - TimeSpan.FromMinutes(1);
        (await Post(client, @"KOC\replayed", SharedKey, old)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void A_proof_is_only_good_for_the_user_it_was_made_for()
    {
        var at = DateTimeOffset.UtcNow;
        var signature = IdentityExchange.Sign(@"KOC\ordinary", at, SharedKey);

        // Swapping the user id under a valid signature must not verify.
        IdentityExchange.IsValid(@"KOC\ordinary", at.ToUnixTimeSeconds().ToString(), signature, SharedKey, at)
            .Should().BeTrue();
        IdentityExchange.IsValid(@"KOC\someone-important", at.ToUnixTimeSeconds().ToString(), signature, SharedKey, at)
            .Should().BeFalse();
    }

    private static async Task<AuthTokenResponse?> Exchange(HttpClient client, string userId, string key, DateTimeOffset at)
    {
        var response = await Post(client, userId, key, at);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "but said: {0}", await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string userId, string key, DateTimeOffset at)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/exchange")
        {
            Content = JsonContent.Create(new ExchangeIdentityRequest(userId)),
        };
        request.Headers.Add(IdentityExchange.TimestampHeader, at.ToUnixTimeSeconds().ToString());
        request.Headers.Add(IdentityExchange.SignatureHeader, IdentityExchange.Sign(userId, at, key));
        return await client.SendAsync(request);
    }
}
