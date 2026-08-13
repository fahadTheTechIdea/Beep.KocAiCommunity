using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Desktop.Local;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using Beep.KocAiCommunity.Web.Components.Dialogs;
using Beep.KocAiCommunity.Web.Components.Pages;
using Web = Beep.KocAiCommunity.Web;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

/// <summary>
/// The one-editor rule, held from the component side: the console appears only for whoever manages
/// the competition, its Task select cannot leave the metric's family, the submit tab speaks the
/// task's own language — and the launcher owns no field the console also has.
/// </summary>
public class HostConsoleTests : TestContext
{
    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ApplicationName { get; set; } = "tests";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
    }

    private sealed class FakeApi(CompetitionDto competition) : RemoteFallbackKocApiClient(null!)
    {
        public override Task<CompetitionDto?> GetCompetitionAsync(Guid competitionId, CancellationToken ct = default) =>
            Task.FromResult<CompetitionDto?>(competition);

        public override Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(Guid competitionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LeaderboardEntryDto>>([]);

        public override Task<string?> GetCompetitionDataAsync(Guid competitionId, string which, CancellationToken ct = default) =>
            Task.FromResult<string?>("id,f1\ne1,1\ne2,2");

        public override Task<IReadOnlyList<CompetitionTranslationDto>> GetCompetitionTranslationsAsync(Guid competitionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CompetitionTranslationDto>>(
                [new("en", "Raw English title", "Raw English brief"), new("ar", "عنوان", "نبذة")]);

        public override Task<IReadOnlyList<CompetitionCategoryDto>> GetCompetitionCategoriesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CompetitionCategoryDto>>(
                [new("production", "Production", "", "OilBarrel", true, 0, 1)]);
    }

    private static CompetitionDto Competition(bool canManage, string task = "AnomalyDetection", string scorer = "auc") =>
        new(Guid.NewGuid(), "Payroll Integrity", "Catch the bad run.", "active",
            "Company", null, true, true, "label", "id", task, null,
            ParticipantCount: 3, SubmissionCount: 9, HostName: "Sara", QuotaPerDay: 5,
            MetricName: scorer.ToUpperInvariant(), HigherIsBetter: true, CreatedUtc: DateTime.UtcNow,
            ScorerCode: scorer,
            SupportedTasks: scorer == "auc" ? ["AnomalyDetection"] : ["BinaryClassification", "MulticlassClassification"],
            CanManage: canManage, MyQuotaRemainingToday: 4);

    private IRenderedComponent<CompetitionDetail> Render(CompetitionDto competition, string tab = "overview")
    {
        Services.AddMudServices();
        Services.AddLogging();
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IKocApiClient>(new FakeApi(competition));
        Services.AddSingleton(new DevIdentity());
        Services.AddSingleton(new RealtimeOptions("http://localhost/never-connects"));
        Services.AddSingleton(new Beep.KocAiCommunity.Web.Services.HeroImageStorage(new FakeEnv()));

        // MudTabs renders only the active panel, so each test lands on the tab it is about — the same
        // ?tab= deep link a person would use.
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
            .NavigateTo($"/compete/{competition.Id}?tab={tab}");
        var cut = RenderComponent<CompetitionDetail>(p => p.Add(x => x.Id, competition.Id));

        // The page loads its competition asynchronously; every assertion below is about the loaded
        // page, not the spinner.
        cut.WaitForState(() => cut.Markup.Contains("Payroll Integrity"), TimeSpan.FromSeconds(10));
        return cut;
    }

    [Fact]
    public void The_console_shows_only_for_whoever_manages_the_competition()
    {
        // CanManage comes computed from the server — a hosting grant alone opened this tab before,
        // and every save then bounced with "only the creator…".
        Render(Competition(canManage: true), tab: "host").Markup.Should().Contain("About this challenge");
    }

    [Fact]
    public void A_hosting_grant_alone_does_not_open_someone_elses_console()
    {
        // CanManage=false with the host deep link: the tab simply is not there to land on.
        var cut = Render(Competition(canManage: false), tab: "host");
        cut.Markup.Should().NotContain("About this challenge");
    }

    [Fact]
    public void The_task_family_is_the_scorers_family_and_nothing_else()
    {
        // The console's Task select binds CompetitionTaskCatalog.ForScorer — pinning the rule at its
        // source: an AUC competition can only ever be an anomaly challenge, and accuracy can never
        // drift into scoring a regression.
        Web.Components.Shared.CompetitionTaskCatalog.ForScorer("auc")
            .Select(t => t.Key).Should().Equal("AnomalyDetection");
        Web.Components.Shared.CompetitionTaskCatalog.ForScorer("accuracy")
            .Select(t => t.Key).Should().Equal("BinaryClassification", "MulticlassClassification");
        Web.Components.Shared.CompetitionTaskCatalog.ForScorer("rmse")
            .Select(t => t.Key).Should().Equal("Regression", "Forecasting");
    }

    [Fact]
    public void The_console_edits_both_languages_side_by_side_from_the_raw_text()
    {
        var cut = Render(Competition(canManage: true), tab: "host");

        // The English comes from the translations read (the raw record), never from the translated
        // page view — and the Arabic sits beside it, not in another room.
        cut.Markup.Should().Contain("Raw English title");
        cut.Markup.Should().Contain("عنوان");
        cut.Markup.Should().Contain("Save — all languages");
    }

    [Fact]
    public void The_submit_tab_speaks_the_tasks_own_language_and_shows_the_remaining_quota()
    {
        var cut = Render(Competition(canManage: false, task: "AnomalyDetection", scorer: "auc"), tab: "submissions");

        // AUC wants a ranking score, not id,label — the one line of guidance must say so.
        cut.Markup.Should().Contain("anomaly score");
        cut.Markup.Should().Contain("4 of 5");
        cut.Markup.Should().Contain("sample_submission.csv");
    }

    [Fact]
    public void The_launcher_owns_no_field_the_console_also_has()
    {
        Services.AddMudServices();
        Services.AddLogging();
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IKocApiClient>(new FakeApi(Competition(canManage: true)));

        // Rendered through the provider, the way a real click opens it.
        var provider = RenderComponent<MudDialogProvider>();
        var service = Services.GetRequiredService<IDialogService>();
        provider.InvokeAsync(() => service.ShowAsync<CreateCompetitionDialog>("New competition"));

        var markup = provider.Markup;
        markup.Should().Contain("Competition title", "the launcher names the record — the one field born here");
        markup.Should().Contain("What do competitors predict?", "and freezes the task-and-metric pair");

        // Everything else belongs to the console alone. One field in two places is one drift waiting
        // to happen — this is the single-writer rule, pinned.
        markup.Should().NotContain("Overview — what competitors predict");
        markup.Should().NotContain("Who can compete");
        markup.Should().NotContain("Submissions per day");
        markup.Should().NotContain("Reveal date");
        markup.Should().NotContain("العربية");
        markup.Should().NotContain("Hero image");
    }
}
