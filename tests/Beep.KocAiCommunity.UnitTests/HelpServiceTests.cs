using Beep.KocAiCommunity.Application.Help;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class HelpServiceTests
{
    private static readonly HelpService Svc = new();

    [Fact]
    public void Lists_all_articles_and_categories()
    {
        Svc.List(null, null).Should().HaveCountGreaterThanOrEqualTo(6).And.Contain(a => a.Slug == "getting-started");
        Svc.Categories.Should().Contain("FAQ");
    }

    [Fact]
    public void Filters_by_category()
    {
        Svc.List("FAQ", null).Should().OnlyContain(a => a.Category == "FAQ").And.NotBeEmpty();
    }

    [Fact]
    public void Searches_title_body_and_tags()
    {
        // "barrels" appears in the earning-barrels article's tags and body.
        Svc.List(null, "barrels").Should().Contain(a => a.Slug == "earning-barrels");

        // "leakage" appears in the workflow guide and in the split node reference. Asserting the search
        // finds the right articles, not an exact count — a count breaks every time content is added,
        // which is the opposite of what this test is for.
        Svc.List(null, "leakage").Select(a => a.Slug)
            .Should().Contain("build-a-workflow").And.Contain("nodes-split");
    }

    [Fact]
    public void Gets_by_slug()
    {
        Svc.Get("faq").Should().NotBeNull();
        Svc.Get("does-not-exist").Should().BeNull();
    }
}
