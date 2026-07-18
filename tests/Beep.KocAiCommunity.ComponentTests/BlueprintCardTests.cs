using Beep.KocAiCommunity.Ui.Shared.Components;
using Bunit;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

public class BlueprintCardTests : TestContext
{
    [Fact]
    public void Renders_blueprint_border_and_corner_ticks()
    {
        var cut = RenderComponent<BlueprintCard>(parameters => parameters
            .AddChildContent("<span>content</span>"));

        cut.Markup.Should().Contain("koc-blueprint");
        cut.Markup.Should().Contain("koc-corner");
        cut.Markup.Should().Contain("content");
    }
}
