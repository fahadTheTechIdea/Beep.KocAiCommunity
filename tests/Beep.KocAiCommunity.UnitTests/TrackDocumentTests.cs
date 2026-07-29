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

        // A translation deliberately shares its original's order — it takes the same place in the
        // catalogue — so the ordering only has to be unambiguous within one language.
        documents.GroupBy(d => d.Language)
            .Should().OnlyContain(g => g.Select(d => d.Order).Distinct().Count() == g.Count(),
                "two tracks at the same order in the same language sort arbitrarily");

        documents.Select(d => (d.ContentKey, d.Language)).Should()
            .OnlyHaveUniqueItems("the seeder matches an existing track by content key and language");
    }

    [Fact]
    public void A_translation_is_paired_with_its_original_by_file_name()
    {
        var documents = TrackDocument.All();

        var arabic = documents.Where(d => d.Language == TrackLanguages.Arabic).ToList();
        arabic.Should().NotBeEmpty("at least one track is translated, which is what proves the path works");

        // The pairing is the whole mechanism: 07-anomaly-detection.ar.md must key on the same material
        // as 07-anomaly-detection.md, or the two would seed as unrelated tracks and the reader would
        // never be offered the other language.
        foreach (var translation in arabic)
        {
            documents.Should().Contain(
                d => d.ContentKey == translation.ContentKey && d.Language == TrackLanguages.English,
                "'{0}' is keyed on '{1}' and needs the English document it translates", translation.Title, translation.ContentKey);

            translation.ContentKey.Should().NotContain(".ar", "the language suffix names the file, not the material");
        }
    }

    [Fact]
    public void An_arabic_document_declares_its_language_and_splits_on_arabic_headings()
    {
        // A translator writes headings in the language they are writing in. Requiring the English word
        // "Lesson" inside an Arabic document would make the file unmaintainable by the person who owns it.
        var document = TrackDocument.Parse("""
            title: اكتشاف الشاذ
            summary: اعثر على القراءات التي لا تنتمي.
            level: Advanced
            order: 7
            language: ar

            ## الدرس 1 — مشكلة الأعطال المُصنَّفة
            التصنيف يحتاج أمثلة لما تبحث عنه.

            ## الدرس 2 — مقبض الرتبة
            ابدأ بالقيمة الافتراضية.
            """);

        document.Should().NotBeNull();
        document!.Language.Should().Be(TrackLanguages.Arabic);
        document.Title.Should().Be("اكتشاف الشاذ");
        document.Lessons.Should().HaveCount(2);
        document.Lessons[0].Title.Should().Be("مشكلة الأعطال المُصنَّفة");
        document.Lessons[1].Content.Should().Contain("الافتراضية");
    }

    [Fact]
    public void A_document_with_no_language_header_is_english()
    {
        // Every track written before translation existed says nothing about its language, and all of
        // them are English. Defaulting anywhere else would relabel the entire catalogue.
        TrackDocument.All().Where(d => d.ContentKey == "04-classification")
            .Should().OnlyContain(d => d.Language == TrackLanguages.English);
    }

    [Theory]
    [InlineData("ar", "ar")]
    [InlineData("AR", "ar")]
    [InlineData("fr", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void An_unrecognised_language_reads_as_english(string? requested, string expected) =>
        TrackLanguages.Normalize(requested).Should().Be(expected);

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
