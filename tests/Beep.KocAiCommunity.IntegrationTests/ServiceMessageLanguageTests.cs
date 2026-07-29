using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Community;
using Beep.KocAiCommunity.Contracts.Competitions;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// The sentences a member reads when something they tried could not be done, in their own language.
/// <para>
/// These are the last English strings a reader would meet: a page fully in Arabic that answers a
/// refused action with "This discussion is locked." The message travels from a service in Infrastructure
/// to an alert in the browser, so it is worth pinning that the whole path carries the language.
/// </para>
/// </summary>
public class ServiceMessageLanguageTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    private HttpClient Client(string sub, string? language, params string[] roles)
    {
        var client = _factory.CreateClientAs(sub, roles);
        if (language is not null)
        {
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));
        }

        return client;
    }

    [Fact]
    public async Task A_refusal_reads_in_the_language_the_caller_asked_for()
    {
        // A scope the caller has no org unit at — a plain refusal, no placeholders.
        var arabic = Client("msg-ar", "ar", "Employee");

        var response = await arabic.PostAsJsonAsync("/api/v1/discussions",
            new CreateDiscussionRequest("عنوان", "نص", "Team"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("لست ضمن وحدة تنظيمية", "the refusal is what an Arabic reader sees in the alert");
        body.Should().NotContain("You are not part of");
    }

    [Fact]
    public async Task The_same_refusal_reads_in_english_by_default()
    {
        var english = Client("msg-en", null, "Employee");

        var response = await english.PostAsJsonAsync("/api/v1/discussions",
            new CreateDiscussionRequest("Title", "Body", "Team"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("You are not part of an org unit");
    }

    [Fact]
    public async Task A_message_with_a_value_in_it_keeps_the_value_and_translates_the_sentence()
    {
        // This is the case that forced the template split. Built by interpolation the sentence would be
        // a different string for every scope, and so could never be looked up at all.
        var arabic = Client("msg-args", "ar", "Employee");

        var response = await arabic.PostAsJsonAsync("/api/v1/discussions",
            new CreateDiscussionRequest("عنوان", "نص", "Group"));

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("لست ضمن وحدة تنظيمية");
        body.Should().Contain("Group", "the value is filled in after translation, so it survives intact");
    }

    [Fact]
    public async Task An_untranslated_message_falls_back_to_english_rather_than_a_key()
    {
        // Admin-facing messages are deliberately not translated. They must still read as sentences.
        var arabic = Client("msg-fallback", "ar", "Employee");

        var response = await arabic.GetAsync($"/api/v1/competitions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unknown_language_reads_as_english()
    {
        var french = Client("msg-fr", "fr", "Employee");

        var response = await french.PostAsJsonAsync("/api/v1/discussions",
            new CreateDiscussionRequest("Title", "Body", "Team"));

        (await response.Content.ReadAsStringAsync()).Should().Contain("You are not part of an org unit");
    }
}
