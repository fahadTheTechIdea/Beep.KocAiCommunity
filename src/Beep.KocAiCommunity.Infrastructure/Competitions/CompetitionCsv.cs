using Beep.KocAiCommunity.Application.Common;

namespace Beep.KocAiCommunity.Infrastructure.Competitions;

/// <summary>
/// Reads a scoring CSV (<c>{idColumn},value</c>) into ordered id/value records using the shared
/// RFC-4180 codec, skipping the header row (first field equals the competition's id column or the
/// literal "id"). Duplicate ids are preserved here; callers decide how to fold them.
/// </summary>
internal static class CompetitionCsv
{
    public static async Task<List<(string Id, string Value)>> ReadAsync(Stream stream, string idColumn, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);

        var rows = new List<(string, string)>();
        var first = true;
        foreach (var record in KocCsv.ParseRecords(text))
        {
            if (record.Length < 2)
            {
                continue;
            }

            var id = record[0].Trim();
            if (first)
            {
                first = false;
                if (id.Equals(idColumn, StringComparison.OrdinalIgnoreCase) || id.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            rows.Add((id, record[1].Trim()));
        }

        return rows;
    }
}
