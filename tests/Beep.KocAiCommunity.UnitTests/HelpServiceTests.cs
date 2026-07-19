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
        // "leakage" appears only in the workflow article's body.
        Svc.List(null, "leakage").Should().ContainSingle().Which.Slug.Should().Be("build-a-workflow");
    }

    [Fact]
    public void Gets_by_slug()
    {
        Svc.Get("faq").Should().NotBeNull();
        Svc.Get("does-not-exist").Should().BeNull();
    }
}
