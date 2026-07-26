using Beep.KocAiCommunity.Application.Common;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;

namespace Beep.KocAiCommunity.ML.Nodes;

/// <summary>
/// The engine-neutral type of a table column, carried across the CSV crossing so a load does not have to
/// re-sniff types from text (which silently retypes leading-zero ids, drifts numeric precision, or types a
/// column differently at train vs eval). Kept coarse on purpose: ML.NET numeric features are always Single,
/// so <see cref="Integer"/> and <see cref="Real"/> both load as Single there but map to distinct DuckDB
/// types (BIGINT vs DOUBLE) so a pure-SQL chain keeps its fidelity.
/// </summary>
public enum PipelineColumnType
{
    Text,
    Integer,
    Real,
    Boolean,
    DateTime,
}

/// <summary>
/// The uniform data contract that flows between every pipeline node on a <c>Table</c> port. Physically
/// a header CSV file — the one representation both engines speak: DuckDB reads/writes CSV natively and
/// ML.NET loads it. A DuckDB node and an ML.NET node therefore produce and consume the identical thing,
/// so nodes are interchangeable and freely ordered.
/// <para>
/// <see cref="Types"/> is the captured schema of the producing engine. When present it is authoritative,
/// so a reload preserves types instead of re-inferring them from the CSV text. It is null for a raw user
/// upload (whose types are genuinely unknown and must be inferred).
/// </para>
/// </summary>
public sealed record PipelineTable(
    string CsvPath,
    IReadOnlyList<string> Columns,
    long RowCount,
    IReadOnlyList<PipelineColumnType>? Types = null)
{
    /// <summary>Wraps an existing CSV file as a table (reads its header + row count). Types are unknown
    /// (a raw upload), so they are left null and inferred on load.</summary>
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

    /// <summary>Materializes a DuckDB table to a fresh CSV-backed table, carrying DuckDB's column types.</summary>
    public static PipelineTable FromDuck(DuckDbSession duck, string tableName, ICollection<string> tempFiles)
    {
        var path = NewTemp(tempFiles);
        duck.ExportCsv(tableName, path);
        var columns = duck.Columns(tableName).ToList();
        var types = duck.ColumnTypes(tableName);
        return new PipelineTable(path, columns, duck.RowCount(tableName), types.Count == columns.Count ? types : null);
    }

    /// <summary>Materializes an ML.NET view to a fresh CSV-backed table, carrying the view's column types.</summary>
    public static PipelineTable FromMlView(IDataView view, ICollection<string> tempFiles)
    {
        var path = NewTemp(tempFiles);
        MlCsv.Write(view, path);
        var types = MlCsv.DescribeColumns(view);
        var table = FromCsvFile(path);
        return types.Count == table.Columns.Count ? table with { Types = types } : table;
    }

    /// <summary>Loads this table into a DuckDB table of the given name, applying the carried types (if any).</summary>
    public void LoadIntoDuck(DuckDbSession duck, string tableName) => duck.LoadCsv(CsvPath, tableName, Types);

    /// <summary>
    /// Loads this table into an ML.NET <see cref="IDataView"/>. When the schema is carried, the loader is
    /// built from it (numeric → Single, matching the feature contract; text/bool/datetime preserved) with
    /// quoting enabled, so the exact types that flowed are honoured — no re-inference and no quoting-mismatch.
    /// Otherwise the types are inferred (a raw upload). <paramref name="forceTextColumn"/> pins a named
    /// column to <see cref="DataKind.String"/> either way — used for the id column at predict time, so a
    /// numeric-looking or zero-padded id (e.g. <c>00123</c>) is never retyped and re-serialized as <c>123</c>,
    /// which would break the id-aligned join against the answer key.
    /// </summary>
    public IDataView LoadIntoMl(MLContext ml, string labelColumn, string? forceTextColumn = null)
    {
        if (Types is { } types && types.Count == Columns.Count)
        {
            var cols = new TextLoader.Column[Columns.Count];
            for (var i = 0; i < Columns.Count; i++)
            {
                var kind = string.Equals(Columns[i], forceTextColumn, StringComparison.Ordinal)
                    ? DataKind.String
                    : ToDataKind(types[i]);
                cols[i] = new TextLoader.Column(Columns[i], kind, i);
            }

            // allowQuoting matches the RFC-4180 CSV every producer writes, so a quoted comma/newline value
            // round-trips into one field instead of being mis-split.
            return ml.Data.CreateTextLoader(cols, separatorChar: ',', hasHeader: true, allowQuoting: true).Load(CsvPath);
        }

        var inferLabel = Columns.Contains(labelColumn) ? labelColumn : Columns.FirstOrDefault() ?? labelColumn;
        var inferred = ml.Auto().InferColumns(CsvPath, labelColumnName: inferLabel, groupColumns: false);
        var options = inferred.TextLoaderOptions;
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

    // ML.NET numeric features are Single by contract, so both integer and real map to Single here (an
    // integer id that must stay exact is pinned to text separately via forceTextColumn).
    private static DataKind ToDataKind(PipelineColumnType type) => type switch
    {
        PipelineColumnType.Boolean => DataKind.Boolean,
        PipelineColumnType.DateTime => DataKind.DateTime,
        PipelineColumnType.Text => DataKind.String,
        _ => DataKind.Single,
    };

    private static string NewTemp(ICollection<string> tempFiles)
    {
        var path = PipelineTemp.New();
        tempFiles.Add(path);
        return path;
    }
}
