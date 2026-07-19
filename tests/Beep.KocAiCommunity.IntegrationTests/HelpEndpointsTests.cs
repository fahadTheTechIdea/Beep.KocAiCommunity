using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Help;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class HelpEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Articles_list_filter_search_and_read()
    {
        var me = _factory.CreateClientAs("help-user", "Employee");

        var all = await me.GetFromJsonAsync<List<HelpArticleSummaryDto>>("/api/v1/help/articles");
        all.Should().Contain(a => a.Slug == "getting-started");

        var faq = await me.GetFromJsonAsync<List<HelpArticleSummaryDto>>("/api/v1/help/articles?category=FAQ");
        faq.Should().OnlyContain(a => a.Category == "FAQ").And.NotBeEmpty();

        var search = await me.GetFromJsonAsync<List<HelpArticleSummaryDto>>("/api/v1/help/articles?q=leakage");
        search.Should().ContainSingle(a => a.Slug == "build-a-workflow");

        var article = await me.GetFromJsonAsync<HelpArticleDto>("/api/v1/help/articles/faq");
        article!.BodyMarkdown.Should().Contain("FAQ");

        (await me.GetAsync("/api/v1/help/articles/nope")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Help_requires_authentication()
    {
        _factory.CreateClientAs(null);
        var anon = _factory.CreateClient();
        (await anon.GetAsync("/api/v1/help/articles")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
