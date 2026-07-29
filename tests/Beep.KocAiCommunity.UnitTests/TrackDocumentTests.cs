using Beep.KocAiCommunity.Domain.Learning;
using Beep.KocAiCommunity.Infrastructure.Learning;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Track content is authored as markdown and embedded in the assembly. These check the documents are
/// actually found and parsed — a track that silently fails to load would simply be missing from the
/// catalogue, with nothing to indicate why.
/// </summary>
public class TrackDocumentTests
{
    [Fact]
    public void Every_embedded_document_parses_into_a_track()
    {
        var documents = TrackDocument.All();

        documents.Should().NotBeEmpty("the authored tracks are embedded in the Infrastructure assembly");
        documents.Should().OnlyContain(d =>
            !string.IsNullOrWhiteSpace(d.Title)
            && !string.IsNullOrWhiteSpace(d.Summary)
            && d.Lessons.Count > 0);
    }

    [Fact]
    public void Tracks_are_ordered_and_do_not_collide_with_the_starter_tracks()
    {
        var documents = TrackDocument.All();

        // Orders 0-3 belong to the original starter tracks seeded in code.
        documents.Should().OnlyContain(d => d.Order >= 4, "authored tracks continue after the starters");
        documents.Select(d => d.Order).Should().OnlyHaveUniqueItems("two tracks at the same order sort arbitrarily");
        documents.Select(d => d.Title).Should().OnlyHaveUniqueItems("the seeder matches existing tracks by title");
    }

    [Fact]
    public void Every_lesson_has_a_title_and_a_body()
    {
        foreach (var document in TrackDocument.All())
        {
            foreach (var (title, content) in document.Lessons)
            {
                title.Should().NotBeNullOrWhiteSpace("lesson {0} of '{1}'", document.Lessons.Count, document.Title);
                content.Should().NotBeNullOrWhiteSpace("'{0}' — lesson '{1}' has no body", document.Title, title);
            }
        }
    }

    [Fact]
    public void The_parser_reads_the_header_and_splits_on_lesson_headings()
    {
        var document = TrackDocument.Parse("""
            title: Predict a number
            summary: Estimate a rate from sensor readings.
            level: Intermediate
            order: 5

            ## Lesson 1 — Framing the question
            What are you actually predicting?

            ## Lesson 2 — Reading R²
            A number between 0 and 1.
            """);

        document.Should().NotBeNull();
        document!.Title.Should().Be("Predict a number");
        document.Level.Should().Be(TrackLevel.Intermediate);
        document.Order.Should().Be(5);
        document.Lessons.Should().HaveCount(2);
        document.Lessons[0].Title.Should().Be("Framing the question");
        document.Lessons[0].Content.Should().Contain("actually predicting");
        document.Lessons[1].Title.Should().Be("Reading R²");
    }

    [Fact]
    public void A_document_with_no_lessons_is_rejected_rather_than_seeded_empty()
    {
        TrackDocument.Parse("title: Nothing\nsummary: Empty\nlevel: Beginner\norder: 9").Should().BeNull();
    }
}
