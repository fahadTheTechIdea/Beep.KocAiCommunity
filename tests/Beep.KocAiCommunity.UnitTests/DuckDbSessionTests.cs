using Beep.KocAiCommunity.ML.Nodes;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>Confirms the embedded DuckDB native engine loads and runs, and CSV round-trips work.</summary>
public class DuckDbSessionTests
{
    [Fact]
    public void Opens_and_runs_sql()
    {
        using var db = new DuckDbSession();
        db.Execute("CREATE TABLE t(a INTEGER, b INTEGER);");
        db.Execute("INSERT INTO t VALUES (1, 2), (3, 4);");
        db.RowCount("t").Should().Be(2);
        DuckDbSession.ToLong(db.Scalar("SELECT SUM(a + b) FROM t;")).Should().Be(10);
    }

    [Fact]
    public void Loads_and_exports_csv_round_trip()
    {
        var inPath = Path.Combine(Path.GetTempPath(), $"duck-in-{Guid.NewGuid():N}.csv");
        var outPath = Path.Combine(Path.GetTempPath(), $"duck-out-{Guid.NewGuid():N}.csv");
        File.WriteAllText(inPath, "id,pressure,zone\nW1,3200,north\nW2,2700,south\n");
        try
        {
            using var db = new DuckDbSession();
            db.LoadCsv(inPath, "wells");
            db.Columns("wells").Should().ContainInOrder("id", "pressure", "zone");
            db.RowCount("wells").Should().Be(2);

            // A real SQL transform: filter + derive a column.
            db.CreateTableAs("high", "SELECT id, pressure, pressure / 1000.0 AS kpsi FROM wells WHERE pressure > 3000");
            db.RowCount("high").Should().Be(1);

            db.ExportCsv("high", outPath);
            var text = File.ReadAllText(outPath);
            text.Should().Contain("id,pressure,kpsi").And.Contain("W1");
        }
        finally
        {
            File.Delete(inPath);
            File.Delete(outPath);
        }
    }
}
