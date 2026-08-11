using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Desktop.Local;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using Beep.KocAiCommunity.Web.Components.Layout;
using Beep.KocAiCommunity.Web.Components.Shared;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

/// <summary>
/// The two things a visitor meets before anything else: the prototype notice, and the control that says
/// which part of KOC they are looking at.
/// </summary>
public class DisclaimerAndAreaMenuTests : TestContext
{
    private sealed class FakeApi : RemoteFallbackKocApiClient
    {
        public FakeApi() : base(null!) { }

        public override Task<IReadOnlyList<CompetitionCategoryDto>> GetCompetitionCategoriesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CompetitionCategoryDto>>(
            [
                new("training", "Training & Development", "Courses.", "School", true, 0, 5),
                new("people", "People & HR", "Payroll.", "Groups", true, 1, 5),
            ]);
    }

    private void Common()
    {
        Services.AddMudServices();
        Services.AddLogging();
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IKocApiClient>(new FakeApi());
        Services.AddSingleton(new DevIdentity());
    }

    [Fact]
    public void The_prototype_notice_shows_by_default_in_both_languages()
    {
        Common();

        var cut = RenderComponent<DemoDisclaimer>();

        // Shown without waiting for demonstration accounts to be seeded: the competitions and datasets
        // that ship with the platform are illustrative either way.
        cut.Markup.Should().Contain("Prototype");
        cut.Markup.Should().Contain("prototype of the");

        // Both languages at once, not the reader's own — someone looking over a colleague's shoulder
        // must be able to read it too.
        cut.Markup.Should().Contain("نموذج أولي", "the Arabic heading is always present, not only for Arabic readers");
        cut.Markup.Should().Contain("شركة نفط الكويت");
        cut.Markup.Should().Contain("direction:rtl");
    }

    [Fact]
    public void Acknowledging_the_notice_dismisses_it()
    {
        Common();

        var cut = RenderComponent<DemoDisclaimer>();
        cut.FindAll("button").Should().NotBeEmpty();
        cut.Find("button").Click();

        cut.Markup.Should().BeEmpty("the notice is dismissed for the rest of the browser session");
    }

    [Fact]
    public void The_area_filter_activator_is_a_real_button_that_opens_the_menu()
    {
        Common();

        // The items open into the popover layer, not into the bar, so the provider has to be in the
        // tree for this to be a test of what the visitor actually sees.
        var popovers = RenderComponent<MudBlazor.MudPopoverProvider>();
        var cut = RenderComponent<TopNav>();

        // The bug this guards: MudBlazor's ActivatorContent does not toggle the menu by itself, so an
        // activator rendered as an inert div looked like a dropdown and did nothing when clicked.
        cut.Find(".koc-areafilter button").Click();

        popovers.Markup.Should().Contain("Training &amp; Development");
        popovers.Markup.Should().Contain("People &amp; HR");
        popovers.Markup.Should().Contain("All areas", "clearing the filter has to be reachable from the menu");
    }
}
