using System.Data;
using Beep.KocAiCommunity.Application.Common;
using DuckDB.NET.Data;

namespace Beep.KocAiCommunity.ML.Nodes;

/// <summary>
/// A short-lived in-memory DuckDB connection used as the SQL/ETL engine for DuckDB pipeline nodes.
/// It only ever sees the pipeline's working table and any explicitly registered dataset tables — no
/// host filesystem or network access beyond the temp CSVs the engine itself writes/reads for crossing
/// to and from ML.NET.
/// </summary>
public sealed class DuckDbSession : IDisposable
{
    private readonly DuckDBConnection _connection;

    // Columns (e.g. the id column) that must be read as text rather than have their type re-sniffed —
    // keeps a zero-padded or numeric-looking id like "00123" from becoming the integer 123 on load and
    // then being re-serialized without the leading zeros, which would break id-aligned scoring.
    private readonly HashSet<string> _textColumns;

    public DuckDbSession(IEnumerable<string>? textColumns = null)
    {
        _textColumns = textColumns is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(textColumns, StringComparer.Ordinal);

        _connection = new DuckDBConnection("DataSource=:memory:");
        _connection.Open();

        // Determinism: a single worker thread with insertion order preserved makes every operation —
        // exports, aggregations, and ORDER BY tie-breaking — reproducible run-to-run. Pipeline data is
        // small, so serial execution costs nothing and buys a stable, replayable engine.
        Execute("SET threads TO 1;");
        Execute("SET preserve_insertion_order TO true;");
    }

    /// <summary>Runs a non-query statement (CREATE/COPY/INSERT/DROP …).</summary>
    public void Execute(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Runs a scalar query.</summary>
    public object? Scalar(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    /// <summary>Loads a CSV file into a table (header required). Applies the carried per-column schema
    /// (<paramref name="columnTypes"/>, parallel to the CSV header) so types are preserved rather than
    /// re-sniffed; any configured text column present in the file is additionally pinned to VARCHAR.</summary>
    public void LoadCsv(string csvPath, string tableName, IReadOnlyList<PipelineColumnType>? columnTypes = null)
        => Execute($"CREATE OR REPLACE TABLE {Quote(tableName)} AS SELECT * FROM read_csv_auto({Literal(csvPath)}, header = true{TypesClause(csvPath, columnTypes)});");

    // Builds the read_csv_auto `types` override from the carried schema plus the configured text-column
    // pins, keyed by the file's actual header (an override for an absent column would error, so the header
    // is checked first). DateTime is left to auto-detection. Empty when nothing applies.
    private string TypesClause(string csvPath, IReadOnlyList<PipelineColumnType>? columnTypes)
    {
        if (_textColumns.Count == 0 && columnTypes is null)
        {
            return string.Empty;
        }

        string[]? header = null;
        try
        {
            using var reader = new StreamReader(csvPath);
            foreach (var record in KocCsv.ParseRecords(reader))
            {
                header = record;
                break;
            }
        }
        catch
        {
            return string.Empty;
        }

        if (header is null)
        {
            return string.Empty;
        }

        var trimmed = header.Select(h => h.Trim()).ToArray();
        var overrides = new Dictionary<string, string>(StringComparer.Ordinal);

        if (columnTypes is not null && columnTypes.Count == trimmed.Length)
        {
            for (var i = 0; i < trimmed.Length; i++)
            {
                if (DuckType(columnTypes[i]) is { } duckType)
                {
                    overrides[trimmed[i]] = duckType;
                }
            }
        }

        // Text pins win — the id column must be VARCHAR regardless of what the schema captured.
        foreach (var name in trimmed.Where(_textColumns.Contains))
        {
            overrides[name] = "VARCHAR";
        }

        return overrides.Count == 0
            ? string.Empty
            : $", types = {{{string.Join(", ", overrides.Select(kv => $"{Literal(kv.Key)}: {Literal(kv.Value)}"))}}}";
    }

    // Canonical column type -> DuckDB type. DateTime returns null (left to auto-detection to avoid a
    // parse mismatch on an unexpected timestamp format).
    private static string? DuckType(PipelineColumnType type) => type switch
    {
        PipelineColumnType.Integer => "BIGINT",
        PipelineColumnType.Real => "DOUBLE",
        PipelineColumnType.Boolean => "BOOLEAN",
        PipelineColumnType.Text => "VARCHAR",
        _ => null,
    };

    /// <summary>Materializes a table (or the result of a query) to a header CSV file.</summary>
    public void ExportCsv(string tableName, string csvPath)
        => Execute($"COPY (SELECT * FROM {Quote(tableName)}) TO {Literal(csvPath)} (HEADER, DELIMITER ',');");

    /// <summary>Replaces the working table with the result of a SELECT.</summary>
    public void CreateTableAs(string tableName, string selectSql)
        => Execute($"CREATE OR REPLACE TABLE {Quote(tableName)} AS {selectSql};");

    /// <summary>
    /// Replaces a table with the result of a SELECT that may reference the table itself (materialize
    /// into a scratch table, then swap) — the safe pattern for in-place SQL transforms.
    /// </summary>
    public void ReplaceTable(string tableName, string selectSql)
    {
        Execute($"CREATE OR REPLACE TABLE __koc_next AS {selectSql};");
        Execute($"DROP TABLE IF EXISTS {Quote(tableName)};");
        Execute($"ALTER TABLE __koc_next RENAME TO {Quote(tableName)};");
    }

    public long RowCount(string tableName) => ToLong(Scalar($"SELECT COUNT(*) FROM {Quote(tableName)};"));

    /// <summary>Coerces a DuckDB scalar to long (aggregates can come back as <see cref="System.Numerics.BigInteger"/>).</summary>
    public static long ToLong(object? value) => value switch
    {
        null or DBNull => 0,
        System.Numerics.BigInteger big => (long)big,
        _ => Convert.ToInt64(value),
    };

    /// <summary>The ordered column names of a table.</summary>
    public IReadOnlyList<string> Columns(string tableName)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {Quote(tableName)} LIMIT 0;";
        using var reader = cmd.ExecuteReader(CommandBehavior.SchemaOnly);
        return [.. Enumerable.Range(0, reader.FieldCount).Select(reader.GetName)];
    }

    /// <summary>The ordered, engine-neutral column types of a table (parallel to <see cref="Columns"/>).</summary>
    public IReadOnlyList<PipelineColumnType> ColumnTypes(string tableName)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {Quote(tableName)} LIMIT 0;";
        using var reader = cmd.ExecuteReader(CommandBehavior.SchemaOnly);
        return [.. Enumerable.Range(0, reader.FieldCount).Select(i => Canonical(reader.GetFieldType(i)))];
    }

    // Maps the CLR type DuckDB reports for a column to the engine-neutral canonical type.
    private static PipelineColumnType Canonical(Type clr)
    {
        if (clr == typeof(bool)) { return PipelineColumnType.Boolean; }
        if (clr == typeof(DateTime) || clr == typeof(DateTimeOffset)) { return PipelineColumnType.DateTime; }
        if (clr == typeof(float) || clr == typeof(double) || clr == typeof(decimal)) { return PipelineColumnType.Real; }
        if (clr == typeof(sbyte) || clr == typeof(byte) || clr == typeof(short) || clr == typeof(ushort)
            || clr == typeof(int) || clr == typeof(uint) || clr == typeof(long) || clr == typeof(ulong)
            || clr == typeof(System.Numerics.BigInteger))
        {
            return PipelineColumnType.Integer;
        }

        return PipelineColumnType.Text;
    }

    /// <summary>Quotes an identifier (table/column) for safe interpolation.</summary>
    public static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    /// <summary>Quotes a string literal (paths, values) for safe interpolation.</summary>
    public static string Literal(string value) => "'" + value.Replace("'", "''") + "'";

    public void Dispose() => _connection.Dispose();
}
