using Beep.KocAiCommunity.Application.Common;

namespace Beep.KocAiCommunity.Infrastructure.Competitions;

/// <summary>
/// Reads a scoring CSV (<c>{idColumn},value</c>) into ordered id/value records using the shared
/// RFC-4180 codec. The first record is always the header: the id column is located <b>by name</b>
/// (matching <paramref name="idColumn"/>, then the literal "id", else column 0) and the value is the
/// other column — the same by-name resolution the answer-key validator uses, so id and value never
/// diverge and a header whose first cell isn't literally "id" can't leak in as a phantom data row.
/// Duplicate ids are preserved here; callers decide how to fold them.
/// </summary>
internal static class CompetitionCsv
{
    public static async Task<List<(string Id, string Value)>> ReadAsync(Stream stream, string idColumn, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);

        var rows = new List<(string, string)>();
        var idIndex = 0;
        var valueIndex = 1;
        var haveHeader = false;
        foreach (var record in KocCsv.ParseRecords(text))
        {
            if (!haveHeader)
            {
                haveHeader = true;
                idIndex = FindColumn(record, idColumn);
                if (idIndex < 0)
                {
                    idIndex = FindColumn(record, "id");
                }

                if (idIndex < 0)
                {
                    idIndex = 0;
                }

                valueIndex = FirstOther(record.Length, idIndex);
                continue;
            }

            if (record.Length < 2)
            {
                continue;
            }

            var id = idIndex < record.Length ? record[idIndex].Trim() : string.Empty;
            var value = valueIndex < record.Length ? record[valueIndex].Trim() : string.Empty;
            rows.Add((id, value));
        }

        return rows;
    }

    private static int FindColumn(string[] header, string name)
        => Array.FindIndex(header, h => h.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));

    // The first column index that isn't the id column (the value column), defaulting to 1.
    private static int FirstOther(int length, int idIndex)
    {
        for (var i = 0; i < length; i++)
        {
            if (i != idIndex)
            {
                return i;
            }
        }

        return 1;
    }
}
