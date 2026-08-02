using System.Text;

namespace Beep.KocAiCommunity.Application.Common;

/// <summary>
/// One RFC-4180 CSV codec for the whole platform. The pipeline writer quotes fields containing commas,
/// quotes, or newlines; the readers must parse the same way. Using a single implementation everywhere
/// (pipeline tables, id/prediction reads, and competition scorers) removes the class of bugs where a
/// naive <c>Split(',')</c> miscounts columns/rows or mis-reads ids when a text value contains a comma or
/// an embedded newline.
/// </summary>
public static class KocCsv
{
    /// <summary>
    /// Streams records from a reader, honouring quoted fields (embedded delimiters, escaped <c>""</c>,
    /// and embedded newlines). Fully-empty lines (a single zero-length field) are skipped, matching CSV
    /// convention. Field values are returned verbatim (quotes removed, content preserved — not trimmed).
    /// <para>
    /// <paramref name="delimiter"/> is a comma for everything the platform writes. It exists for reading
    /// files the platform did not write — Excel in an Arabic or European locale exports semicolons — so
    /// that those are parsed by this codec on the way in rather than by a second parser somewhere else.
    /// </para>
    /// </summary>
    public static IEnumerable<string[]> ParseRecords(TextReader reader, char delimiter = ',')
    {
        var field = new StringBuilder();
        var record = new List<string>();
        var inQuotes = false;

        int read;
        while ((read = reader.Read()) != -1)
        {
            var ch = (char)read;
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }

                continue;
            }

            // Not a switch: the delimiter is a parameter, and a case label has to be constant.
            if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == delimiter)
            {
                record.Add(field.ToString());
                field.Clear();
            }
            else if (ch == '\r')
            {
                if (reader.Peek() == '\n')
                {
                    reader.Read();
                }

                if (EmitRecord(record, field, out var crRow))
                {
                    yield return crRow!;
                }
            }
            else if (ch == '\n')
            {
                if (EmitRecord(record, field, out var lfRow))
                {
                    yield return lfRow!;
                }
            }
            else
            {
                field.Append(ch);
            }
        }

        if (EmitRecord(record, field, out var lastRow))
        {
            yield return lastRow!;
        }
    }

    /// <summary>Parses records from an in-memory string.</summary>
    public static IEnumerable<string[]> ParseRecords(string text, char delimiter = ',')
    {
        using var reader = new StringReader(text);
        foreach (var record in ParseRecords(reader, delimiter))
        {
            yield return record;
        }
    }

    /// <summary>Quotes a single field per RFC-4180 when it contains a delimiter, quote, or newline.</summary>
    public static string QuoteField(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    /// <summary>Joins fields into one RFC-4180 CSV row (each field quoted as needed).</summary>
    public static string WriteRow(IEnumerable<string> fields) => string.Join(',', fields.Select(QuoteField));

    // Completes the current record; returns false for a blank line (single empty field) so it is skipped.
    private static bool EmitRecord(List<string> record, StringBuilder field, out string[]? row)
    {
        record.Add(field.ToString());
        field.Clear();

        if (record.Count == 1 && record[0].Length == 0)
        {
            record.Clear();
            row = null;
            return false;
        }

        row = record.ToArray();
        record.Clear();
        return true;
    }
}
