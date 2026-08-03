using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Learning;
using Beep.KocAiCommunity.Desktop.Local;
using Beep.KocAiCommunity.Web.Components.Pages;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

/// <summary>
/// Sitting a quiz.
/// <para>
/// The endpoints are covered elsewhere; what is only testable here is the rule that submitting is
/// blocked until every question is answered. Warning instead of blocking would let somebody spend a
/// real attempt on questions they meant to come back to, and no amount of "are you sure" undoes a
/// recorded failure afterwards.
/// </para>
/// </summary>
public class TrackQuizTests : TestContext
{
    private static readonly Guid TrackId = Guid.NewGuid();

    /// <summary>
    /// A client that answers only the quiz calls. RemoteFallbackKocApiClient exists for exactly this:
    /// everything not overridden refuses by name rather than needing 160 stubbed members.
    /// </summary>
    private sealed class QuizOnlyClient(QuizDto quiz) : RemoteFallbackKocApiClient(remote: null)
    {
        public SubmitQuizRequest? Submitted { get; private set; }

        public override Task<QuizDto?> GetTrackQuizAsync(Guid trackId, CancellationToken ct = default) =>
            Task.FromResult<QuizDto?>(quiz);

        public override Task<(QuizAttemptResultDto? Result, string? Error)> SubmitQuizAsync(
            Guid trackId, SubmitQuizRequest request, CancellationToken ct = default)
        {
            Submitted = request;

            var questions = quiz.Questions
                .Select(q => new QuizQuestionResultDto(q.Id, q.Text, true, [q.Answers[0].Id], [q.Answers[0].Id], "Because."))
                .ToList();

            return Task.FromResult<(QuizAttemptResultDto?, string?)>(
                (new QuizAttemptResultDto(Guid.NewGuid(), 1, questions.Count, questions.Count, 100, true,
                    DateTime.UtcNow, questions, CompletedTrack: true), null));
        }
    }

    private static QuizDto Quiz(int questionCount) => new(
        Guid.NewGuid(), TrackId, "A short check.", 70, IsMandatory: true,
        [.. Enumerable.Range(1, questionCount).Select(i => new QuizQuestionDto(
            Guid.NewGuid(), i, $"Question {i}?",
            [new QuizAnswerDto(Guid.NewGuid(), 1, "Right"), new QuizAnswerDto(Guid.NewGuid(), 2, "Wrong")]))]);

    private IRenderedComponent<TrackQuiz> Render(QuizOnlyClient client)
    {
        Services.AddMudServices();
        Services.AddLogging();
        Services.AddLocalization();
        Services.AddSingleton<IKocApiClient>(client);

        // A signed-in persona: a visitor is redirected away rather than shown the quiz.
        Services.AddSingleton(new DevIdentityOptions());
        Services.AddSingleton<DevIdentity>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        return RenderComponent<TrackQuiz>(p => p.Add(x => x.TrackId, TrackId));
    }

    [Fact]
    public void Submitting_is_blocked_until_every_question_is_answered()
    {
        var client = new QuizOnlyClient(Quiz(2));
        var cut = Render(client);

        var submit = cut.FindAll("button").First(b => b.TextContent.Contains("Submit"));
        submit.HasAttribute("disabled").Should().BeTrue("nothing is answered yet");

        // Answer the first of two.
        cut.FindAll(".koc-option")[0].Click();
        cut.FindAll("button").First(b => b.TextContent.Contains("Submit"))
            .HasAttribute("disabled").Should().BeTrue("one question is still outstanding");

        cut.Markup.Should().Contain("1", "the count of what is left is shown rather than left to be guessed");

        // Answer the second: the first option of the second question.
        cut.FindAll(".koc-option")[2].Click();
        cut.FindAll("button").First(b => b.TextContent.Contains("Submit"))
            .HasAttribute("disabled").Should().BeFalse("every question now has an answer");
    }

    [Fact]
    public void An_option_can_be_unpicked()
    {
        // Multi-select rather than radio buttons, deliberately: the learner is not told how many answers
        // are correct, and a radio group would tell them. Which means picking must be reversible.
        var client = new QuizOnlyClient(Quiz(1));
        var cut = Render(client);

        cut.FindAll(".koc-option")[0].Click();
        cut.FindAll(".koc-option--on").Should().HaveCount(1);

        cut.FindAll(".koc-option")[0].Click();
        cut.FindAll(".koc-option--on").Should().BeEmpty();
        cut.FindAll("button").First(b => b.TextContent.Contains("Submit"))
            .HasAttribute("disabled").Should().BeTrue("unpicking leaves the question unanswered again");
    }

    [Fact]
    public void Submitting_sends_what_was_picked_and_shows_the_review()
    {
        var client = new QuizOnlyClient(Quiz(1));
        var cut = Render(client);

        cut.FindAll(".koc-option")[0].Click();
        cut.FindAll("button").First(b => b.TextContent.Contains("Submit")).Click();

        client.Submitted.Should().NotBeNull();
        client.Submitted!.Responses.Should().ContainSingle()
            .Which.SelectedAnswerIds.Should().ContainSingle();

        // The explanation is the reason the review exists — a number alone teaches nothing.
        cut.Markup.Should().Contain("Because.");
        cut.Markup.Should().Contain("koc-quiz-verdict--pass");
    }
}
