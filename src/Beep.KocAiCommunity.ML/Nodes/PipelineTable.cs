using Beep.KocAiCommunity.Application.Common;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;

namespace Beep.KocAiCommunity.ML.Nodes;

/// <summary>
/// The uniform data contract that flows between every pipeline node on a <c>Table</c> port. Physically
/// a header CSV file — the one representation both engines speak: DuckDB reads/writes CSV natively and
/// ML.NET loads it. A DuckDB node and an ML.NET node therefore produce and consume the identical thing,
/// so nodes are interchangeable and freely ordered.
/// </summary>
public sealed record PipelineTable(string CsvPath, IReadOnlyList<string> Columns, long RowCount)
{
    /// <summary>Wraps an existing CSV file as a table (reads its header + row count).</summary>
    public static PipelineTable FromCsvFile(string csvPath)
    {
        using var reader = new StreamReader(csvPath);
        string[]? header = null;
        long rows = 0;
        foreach (var record in KocCsv.ParseRecords(reader))
        {
            if (header is null)
            {
                header = record;
            }
            else
            {
                rows++;
            }
        }

        var columns = header is null ? [] : header.Select(h => h.Trim()).ToList();
        return new PipelineTable(csvPath, columns, rows);
    }

    /// <summary>Materializes a DuckDB table to a fresh CSV-backed table.</summary>
    public static PipelineTable FromDuck(DuckDbSession duck, string tableName, ICollection<string> tempFiles)
    {
        var path = NewTemp(tempFiles);
        duck.ExportCsv(tableName, path);
        return new PipelineTable(path, [.. duck.Columns(tableName)], duck.RowCount(tableName));
    }

    /// <summary>Materializes an ML.NET view to a fresh CSV-backed table.</summary>
    public static PipelineTable FromMlView(IDataView view, ICollection<string> tempFiles)
    {
        var path = NewTemp(tempFiles);
        MlCsv.Write(view, path);
        return FromCsvFile(path);
    }

    /// <summary>Loads this table into a DuckDB table of the given name.</summary>
    public void LoadIntoDuck(DuckDbSession duck, string tableName) => duck.LoadCsv(CsvPath, tableName);

    /// <summary>
    /// Loads this table into an ML.NET <see cref="IDataView"/>, designating the label if present.
    /// <paramref name="forceTextColumn"/> pins a named column to <see cref="DataKind.String"/> instead of
    /// letting the type sniffer re-infer it — used for the id column at predict time, so a numeric-looking
    /// or zero-padded id (e.g. <c>00123</c>) is never silently retyped to a number and re-serialized as
    /// <c>123</c>, which would break the id-aligned join against the answer key.
    /// </summary>
    public IDataView LoadIntoMl(MLContext ml, string labelColumn, string? forceTextColumn = null)
    {
        var inferLabel = Columns.Contains(labelColumn) ? labelColumn : Columns.FirstOrDefault() ?? labelColumn;
        var columns = ml.Auto().InferColumns(CsvPath, labelColumnName: inferLabel, groupColumns: false);
        var options = columns.TextLoaderOptions;
        if (!string.IsNullOrEmpty(forceTextColumn))
        {
            foreach (var col in options.Columns)
            {
                if (string.Equals(col.Name, forceTextColumn, StringComparison.Ordinal))
                {
                    col.DataKind = DataKind.String;
                }
            }
        }

        return ml.Data.CreateTextLoader(options).Load(CsvPath);
    }

    public bool HasColumn(string name) => Columns.Contains(name);

    private static string NewTemp(ICollection<string> tempFiles)
    {
        var path = PipelineTemp.New();
        tempFiles.Add(path);
        return path;
    }
}
