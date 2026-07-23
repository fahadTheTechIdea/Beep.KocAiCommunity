using Beep.KocAiCommunity.Application.Common;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class KocCsvTests
{
    [Fact]
    public void Parses_simple_rows()
    {
        var rows = KocCsv.ParseRecords("id,label\n1,a\n2,b\n").ToList();
        rows.Should().HaveCount(3);
        rows[0].Should().Equal("id", "label");
        rows[2].Should().Equal("2", "b");
    }

    [Fact]
    public void Handles_quoted_comma_and_embedded_newline_and_escaped_quote()
    {
        // One header + one data record whose text field contains a comma, a newline, and a quote.
        var csv = "id,note\n7,\"a, b\nc \"\"q\"\"\"\n";
        var rows = KocCsv.ParseRecords(csv).ToList();

        rows.Should().HaveCount(2);          // NOT miscounted by the embedded newline
        rows[1].Should().HaveCount(2);        // NOT split into extra columns by the comma
        rows[1][0].Should().Be("7");
        rows[1][1].Should().Be("a, b\nc \"q\"");
    }

    [Fact]
    public void Skips_blank_lines()
    {
        KocCsv.ParseRecords("id,label\n\n1,a\n\n").Should().HaveCount(2);
    }

    [Fact]
    public void Roundtrips_through_quote_and_parse()
    {
        var fields = new[] { "7", "a, b\nc \"q\"" };
        var line = KocCsv.WriteRow(fields);
        var parsed = KocCsv.ParseRecords(line).Single();
        parsed.Should().Equal(fields);
    }

    [Fact]
    public void Handles_crlf_line_endings()
    {
        KocCsv.ParseRecords("id,label\r\n1,a\r\n2,b\r\n").Should().HaveCount(3);
    }
}
