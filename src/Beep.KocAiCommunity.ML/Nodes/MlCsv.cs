using System.Globalization;
using System.Text;
using Beep.KocAiCommunity.Application.Common;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Beep.KocAiCommunity.ML.Nodes;

/// <summary>
/// Writes an ML.NET <see cref="IDataView"/> to a plain header CSV — the ML.NET → PipelineTable
/// direction of the uniform data contract. Scalar columns map 1:1; fixed-size vector columns (e.g.
/// after one-hot/concatenate) expand to <c>name.0, name.1, …</c>.
/// </summary>
internal static class MlCsv
{
    public static void Write(IDataView view, string path)
    {
        using var sw = new StreamWriter(path);
        Write(view, sw, writeHeader: true);
    }

    /// <summary>
    /// Writes rows to an open writer, optionally with a header and an appended constant column
    /// (e.g. the <c>__fold</c> marker) — lets the split node emit train+test rows into one file.
    /// </summary>
    public static void Write(IDataView view, TextWriter sw, bool writeHeader, (string Name, string Value)? extra = null)
    {
        var columns = view.Schema.Where(c => !c.IsHidden).ToList();
        using var cursor = view.GetRowCursor(columns);
        var writers = columns.Select(c => ColumnWriter.Create(cursor, c)).ToList();

        if (writeHeader)
        {
            var headers = writers.SelectMany(w => w.Headers).Select(Escape).ToList();
            if (extra is { } e)
            {
                headers.Add(Escape(e.Name));
            }

            sw.WriteLine(string.Join(",", headers));
        }

        var sb = new StringBuilder();
        while (cursor.MoveNext())
        {
            sb.Clear();
            for (var i = 0; i < writers.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                writers[i].AppendRow(sb);
            }

            if (extra is { } e)
            {
                sb.Append(',').Append(e.Value);
            }

            sw.WriteLine(sb.ToString());
        }
    }

    private static string Escape(string value) => KocCsv.QuoteField(value);

    private abstract class ColumnWriter
    {
        public required IReadOnlyList<string> Headers { get; init; }
        public abstract void AppendRow(StringBuilder sb);

        public static ColumnWriter Create(DataViewRowCursor cursor, DataViewSchema.Column col)
        {
            var type = col.Type;
            var itemType = (type as VectorDataViewType)?.ItemType ?? type;
            var vectorSize = (type as VectorDataViewType)?.Size ?? 0;

            if (vectorSize > 0 && itemType == NumberDataViewType.Single)
            {
                return new VectorSingleWriter(cursor.GetGetter<VBuffer<float>>(col), vectorSize)
                {
                    Headers = [.. Enumerable.Range(0, vectorSize).Select(i => $"{col.Name}.{i}")],
                };
            }

            var headers = new[] { col.Name };
            if (itemType == NumberDataViewType.Single) { return new ScalarWriter<float>(cursor.GetGetter<float>(col), v => v.ToString(CultureInfo.InvariantCulture)) { Headers = headers }; }
            if (itemType == NumberDataViewType.Double) { return new ScalarWriter<double>(cursor.GetGetter<double>(col), v => v.ToString(CultureInfo.InvariantCulture)) { Headers = headers }; }
            if (itemType == NumberDataViewType.Int32) { return new ScalarWriter<int>(cursor.GetGetter<int>(col), v => v.ToString(CultureInfo.InvariantCulture)) { Headers = headers }; }
            if (itemType == NumberDataViewType.Int64) { return new ScalarWriter<long>(cursor.GetGetter<long>(col), v => v.ToString(CultureInfo.InvariantCulture)) { Headers = headers }; }
            if (itemType == NumberDataViewType.UInt32) { return new ScalarWriter<uint>(cursor.GetGetter<uint>(col), v => v.ToString(CultureInfo.InvariantCulture)) { Headers = headers }; }
            if (itemType == BooleanDataViewType.Instance) { return new ScalarWriter<bool>(cursor.GetGetter<bool>(col), v => v ? "true" : "false") { Headers = headers }; }
            // A DateTime column (the sniffer can infer one) has no text getter; format it round-trippably
            // (ISO-8601) instead of asking for a ReadOnlyMemory<char> getter it can't provide (which throws).
            if (itemType == DateTimeDataViewType.Instance) { return new ScalarWriter<DateTime>(cursor.GetGetter<DateTime>(col), v => v.ToString("O", CultureInfo.InvariantCulture)) { Headers = headers }; }
            return new ScalarWriter<ReadOnlyMemory<char>>(cursor.GetGetter<ReadOnlyMemory<char>>(col), v => v.ToString()) { Headers = headers };
        }
    }

    private sealed class ScalarWriter<T>(ValueGetter<T> getter, Func<T, string> format) : ColumnWriter
    {
        public override void AppendRow(StringBuilder sb)
        {
            T value = default!;
            getter(ref value);
            sb.Append(Escape(format(value)));
        }
    }

    private sealed class VectorSingleWriter(ValueGetter<VBuffer<float>> getter, int size) : ColumnWriter
    {
        public override void AppendRow(StringBuilder sb)
        {
            VBuffer<float> value = default;
            getter(ref value);
            var dense = new float[size];
            value.CopyTo(dense);
            for (var i = 0; i < size; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(dense[i].ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}
