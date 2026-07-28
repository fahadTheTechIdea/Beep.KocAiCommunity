using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Identity;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// Registration and password sign-in in the LocalAccounts mode — the default for an ordinary web
/// deployment. These run against the host's real token validation, so a token that works here is a
/// token that works in production.
/// <para>
/// Each test builds its own host: "who registered first" is installation-wide state, so sharing one
/// database between tests would make the outcome depend on the order they happen to run in.
/// </para>
/// </summary>
public class AuthEndpointsTests
{
    private const string GoodPassword = "Wells-2026!";

    [Fact]
    public async Task The_first_account_becomes_the_platform_admin_and_can_use_the_api()
    {
        using var factory = new LocalAccountsApiFactory();
        var client = factory.CreateAnonymousClient();

        // A fresh install has nobody, so the UI can offer to create the administrator.
        var state = await client.GetFromJsonAsync<RegistrationStateResponse>("/api/v1/auth/state");
        state!.IsFirstAccount.Should().BeTrue();

        var registered = await Register(client, "first.admin@koc.com", GoodPassword, "First Admin");
        registered.Roles.Should().Contain("PlatformAdmin", "the first account has to be able to administer the platform");

        // The issued token is accepted by an endpoint that requires a real signed-in employee.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.AccessToken);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/v1/me");
        me!.DisplayName.Should().Be("First Admin");
        me.Roles.Should().Contain("PlatformAdmin");

        // Once an account exists, later visitors are ordinary members.
        var after = await client.GetFromJsonAsync<RegistrationStateResponse>("/api/v1/auth/state");
        after!.IsFirstAccount.Should().BeFalse();

        var second = await Register(factory.CreateAnonymousClient(), "second.person@koc.com", GoodPassword, null);
        second.Roles.Should().Contain("Employee");
        second.Roles.Should().NotContain("PlatformAdmin", "only the first account claims the platform");
    }

    [Fact]
    public async Task A_registered_name_falls_back_to_the_email_when_none_is_given()
    {
        using var factory = new LocalAccountsApiFactory();
        var registered = await Register(factory.CreateAnonymousClient(), "fahad.aldhubaib@koc.com", GoodPassword, null);
        registered.DisplayName.Should().Be("Fahad Aldhubaib", "a leaderboard shouldn't show a raw email address");
    }

    [Fact]
    public async Task Sign_in_returns_a_working_token_and_rejects_a_wrong_password()
    {
        using var factory = new LocalAccountsApiFactory();
        var client = factory.CreateAnonymousClient();
        await Register(client, "driller@koc.com", GoodPassword, "Driller");

        var ok = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("driller@koc.com", GoodPassword));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = (await ok.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.ExpiresUtc.Should().BeAfter(DateTime.UtcNow);

        var wrong = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("driller@koc.com", "not-the-password"));
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The same answer for an unknown email — the API must not confirm which addresses are registered.
        var unknown = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("nobody@koc.com", GoodPassword));
        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Error(unknown)).Should().Be(await Error(wrong));
    }

    [Fact]
    public async Task Registration_rejects_a_duplicate_email_and_a_weak_password()
    {
        using var factory = new LocalAccountsApiFactory();
        var client = factory.CreateAnonymousClient();
        await Register(client, "taken@koc.com", GoodPassword, null);

        var duplicate = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("taken@koc.com", GoodPassword));
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Error(duplicate)).Should().Contain("already exists");

        var weak = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("weak@koc.com", "short"));
        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Without_a_token_the_api_refuses_and_ignores_the_demo_persona_headers()
    {
        using var factory = new LocalAccountsApiFactory();
        var client = factory.CreateAnonymousClient();

        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The header that authenticates a caller in demo mode must carry no weight here — otherwise
        // anyone who can reach the API could claim to be an administrator.
        client.DefaultRequestHeaders.Add("X-Dev-User", "intruder");
        client.DefaultRequestHeaders.Add("X-Dev-Roles", "PlatformAdmin");
        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<AuthTokenResponse> Register(HttpClient client, string email, string password, string? displayName)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, password, displayName));
        response.StatusCode.Should().Be(HttpStatusCode.OK, "registration should succeed but said: {0}", await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
    }

    private static async Task<string?> Error(HttpResponseMessage response)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
    }
}
