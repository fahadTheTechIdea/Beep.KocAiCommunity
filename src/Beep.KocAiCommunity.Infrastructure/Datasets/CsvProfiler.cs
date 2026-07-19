using System.Globalization;

namespace Beep.KocAiCommunity.Infrastructure.Datasets;

/// <summary>One profiled column: inferred type, nullability, and sampled summary statistics.</summary>
public sealed record CsvColumn(
    string Name, string DataType, bool Nullable, long NullCount, long DistinctCount, double? Min, double? Max, double? Mean);

/// <summary>The result of profiling a CSV: total/sampled row counts and per-column stats.</summary>
public sealed record CsvProfileResult(long TotalRows, long SampledRows, IReadOnlyList<CsvColumn> Columns);

/// <summary>
/// Streams a CSV and infers a schema + summary statistics from the first <c>sampleLimit</c> rows.
/// Deterministic (fixed sample, first-N) so a profile is reproducible. Simple comma splitting — quoted
/// fields with embedded commas are not handled (documented limitation).
/// </summary>
public static class CsvProfiler
{
    public const int DefaultSampleLimit = 10_000;

    public static CsvProfileResult Profile(Stream csv, int sampleLimit = DefaultSampleLimit)
    {
        using var reader = new StreamReader(csv);
        var header = reader.ReadLine();
        if (header is null)
        {
            return new CsvProfileResult(0, 0, []);
        }

        var names = header.Split(',');
        var n = names.Length;
        var nulls = new long[n];
        var distinct = new HashSet<string>[n];
        var isInt = new bool[n];
        var isNum = new bool[n];
        var isBool = new bool[n];
        var isDate = new bool[n];
        var sawValue = new bool[n];
        var min = new double[n];
        var max = new double[n];
        var sum = new double[n];
        var numCount = new long[n];
        for (var i = 0; i < n; i++)
        {
            distinct[i] = new HashSet<string>(StringComparer.Ordinal);
            isInt[i] = isNum[i] = isBool[i] = isDate[i] = true;
            min[i] = double.PositiveInfinity;
            max[i] = double.NegativeInfinity;
        }

        long total = 0;
        long sampled = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            total++;
            if (sampled >= sampleLimit)
            {
                continue; // keep counting rows, but stop sampling for stats
            }

            sampled++;
            var cells = line.Split(',');
            for (var i = 0; i < n; i++)
            {
                var cell = i < cells.Length ? cells[i].Trim() : string.Empty;
                if (cell.Length == 0)
                {
                    nulls[i]++;
                    continue;
                }

                sawValue[i] = true;
                distinct[i].Add(cell);

                if (long.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) { }
                else { isInt[i] = false; }

                if (double.TryParse(cell, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                {
                    numCount[i]++;
                    sum[i] += d;
                    if (d < min[i]) { min[i] = d; }
                    if (d > max[i]) { max[i] = d; }
                }
                else
                {
                    isNum[i] = false;
                }

                if (!bool.TryParse(cell, out _) && cell is not ("0" or "1")) { isBool[i] = false; }
                if (!DateTime.TryParse(cell, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) { isDate[i] = false; }
            }
        }

        var columns = new List<CsvColumn>(n);
        for (var i = 0; i < n; i++)
        {
            var type = InferType(sawValue[i], isInt[i], isNum[i], isBool[i], isDate[i]);
            double? mn = numCount[i] > 0 ? min[i] : null;
            double? mx = numCount[i] > 0 ? max[i] : null;
            double? mean = numCount[i] > 0 ? sum[i] / numCount[i] : null;
            var numeric = type is "integer" or "number";
            columns.Add(new CsvColumn(names[i], type, nulls[i] > 0, nulls[i], distinct[i].Count,
                numeric ? mn : null, numeric ? mx : null, numeric ? mean : null));
        }

        return new CsvProfileResult(total, sampled, columns);
    }

    private static string InferType(bool sawValue, bool isInt, bool isNum, bool isBool, bool isDate)
    {
        if (!sawValue) { return "string"; }
        if (isInt) { return "integer"; }   // digits-only (incl. 0/1) reads as integer, not boolean
        if (isBool) { return "boolean"; }  // true/false
        if (isNum) { return "number"; }
        if (isDate) { return "date"; }
        return "string";
    }
}
