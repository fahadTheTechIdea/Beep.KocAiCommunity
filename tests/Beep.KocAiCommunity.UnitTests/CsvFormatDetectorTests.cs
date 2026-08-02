using System.Text;
using Beep.KocAiCommunity.Desktop.Local;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Working out what a CSV actually is.
/// <para>
/// This is the most likely first-contact failure for a KOC engineer and it used to be silent: Excel in
/// an Arabic locale exports semicolon-separated, older systems produce Windows-1256, and both imported
/// to something that looked fine in the list and then broke oddly in the designer.
/// </para>
/// </summary>
public class CsvFormatDetectorTests
{
    private static CsvFormat Detect(string text, Encoding? encoding = null) =>
        CsvFormatDetector.Detect((encoding ?? new UTF8Encoding(false)).GetBytes(text));

    [Theory]
    [InlineData(',')]
    [InlineData(';')]
    [InlineData('\t')]
    [InlineData('|')]
    public void Each_supported_delimiter_is_found(char delimiter)
    {
        var text = string.Join('\n',
            $"well{delimiter}pressure{delimiter}failed",
            $"BG-114{delimiter}120{delimiter}0",
            $"BG-115{delimiter}340{delimiter}1",
            $"BG-116{delimiter}210{delimiter}0");

        var format = Detect(text);

        format.Delimiter.Should().Be(delimiter);
        format.Confident.Should().BeTrue();
    }

    [Fact]
    public void A_delimiter_inside_quotes_does_not_sway_the_guess()
    {
        // The classic wrong answer: a notes column full of commas outvotes the semicolons that are
        // actually separating the fields.
        var text = string.Join('\n',
            "well;notes;failed",
            "BG-114;\"routine, no action, cleared\";0",
            "BG-115;\"checked, logged, closed\";1",
            "BG-116;\"noted, filed, done\";0");

        Detect(text).Delimiter.Should().Be(';');
    }

    [Fact]
    public void Consistency_beats_frequency()
    {
        // Commas appear more often overall, but not the same number of times per line — so they are
        // text, not structure. The semicolon count is identical on every row, which is what a
        // delimiter looks like.
        var text = string.Join('\n',
            "well;notes",
            "BG-114;one, two, three",
            "BG-115;four, five",
            "BG-116;six, seven, eight, nine");

        Detect(text).Delimiter.Should().Be(';');
    }

    [Fact]
    public void A_single_column_file_is_reported_as_a_guess_rather_than_a_finding()
    {
        // Nothing to count. A single-column file is legitimate and a mis-read one is not, and here
        // they look identical — so the UI is told to ask rather than assume.
        var format = Detect("well\nBG-114\nBG-115\n");

        format.Confident.Should().BeFalse();
        format.Delimiter.Should().Be(',', "comma is the least surprising thing to fall back to");
    }

    [Fact]
    public void An_empty_file_does_not_throw()
    {
        Detect("").Confident.Should().BeFalse();
    }

    [Fact]
    public void A_utf8_bom_is_taken_at_its_word()
    {
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes("a,b\n1,2\n");

        CsvFormatDetector.Detect(bytes).Encoding.CodePage.Should().Be(Encoding.UTF8.CodePage);
    }

    [Fact]
    public void Utf16_is_recognised_from_its_bom()
    {
        CsvFormatDetector.Detect(Encoding.Unicode.GetPreamble()).Encoding.CodePage
            .Should().Be(Encoding.Unicode.CodePage);
        CsvFormatDetector.Detect(Encoding.BigEndianUnicode.GetPreamble()).Encoding.CodePage
            .Should().Be(Encoding.BigEndianUnicode.CodePage);
    }

    [Fact]
    public void Arabic_column_names_in_utf8_stay_utf8()
    {
        var format = Detect("البئر,الضغط,معطّل\nBG-114,120,0\n");

        format.Encoding.CodePage.Should().Be(Encoding.UTF8.CodePage);
        format.Encoding.GetString(new UTF8Encoding(false).GetBytes("الضغط")).Should().Be("الضغط");
    }

    [Fact]
    public void A_windows_1256_export_is_recognised_as_arabic_not_as_the_machines_own_page()
    {
        // The failure this exists to stop. Falling back to the laptop's ANSI page — 1252 on an
        // English-configured machine — decodes these bytes to "ÇáÈÆÑ". The columns have to come out
        // as words on a machine that is not itself Arabic.
        var arabic = CsvFormatDetector.Arabic();
        var bytes = arabic.GetBytes("البئر,الضغط,معطّل\nBG-114,120,0\nBG-115,340,1\n");

        var format = CsvFormatDetector.Detect(bytes);

        format.Delimiter.Should().Be(',');
        format.Encoding.GetString(bytes).Should().StartWith("البئر");
    }

    [Fact]
    public void Western_text_with_the_odd_accent_is_not_mistaken_for_arabic()
    {
        // The other side of the guess: mostly-ASCII text with a few accented characters is Western,
        // and reading it as Arabic would be its own kind of mojibake.
        var western = CsvFormatDetector.SystemAnsi();
        var bytes = western.GetBytes("well,operator,notes\nBG-114,Bouygues,routine check café\n");

        CsvFormatDetector.Detect(bytes).Encoding.GetString(bytes).Should().Contain("Bouygues");
    }

    [Fact]
    public void A_windows_1256_file_decodes_to_readable_arabic()
    {
        // The point of the fallback: the column names have to come out as words.
        var arabic = CsvFormatDetector.Arabic();
        arabic.GetString(arabic.GetBytes("الضغط")).Should().Be("الضغط");
    }

    [Fact]
    public void Plain_ascii_is_utf8()
    {
        Detect("a,b\n1,2\n").Encoding.CodePage.Should().Be(Encoding.UTF8.CodePage);
    }

    [Fact]
    public void The_delimiter_has_a_name_a_person_can_read()
    {
        CsvFormat.DelimiterName(';').Should().Be("semicolon");
        CsvFormat.DelimiterName('\t').Should().Be("tab");
    }
}
