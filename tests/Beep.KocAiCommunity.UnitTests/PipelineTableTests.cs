using System.Text;
using Beep.KocAiCommunity.ML.Nodes;
using FluentAssertions;
using Microsoft.ML;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The uniform data contract: a DuckDB node and an ML.NET node produce and consume the *same*
/// PipelineTable, so the data round-trips identically through either engine.
/// </summary>
public class PipelineTableTests
{
    [Fact]
    public void Round_trips_through_duckdb_and_mlnet_as_the_same_table()
    {
        var temp = new List<string>();
        try
        {
            var srcPath = Path.Combine(Path.GetTempPath(), $"koc-src-{Guid.NewGuid():N}.csv");
            temp.Add(srcPath);
            File.WriteAllText(srcPath, "id,pressure,label\nw1,3200,true\nw2,900,false\nw3,3100,true\n");
            var source = PipelineTable.FromCsvFile(srcPath);
            source.Columns.Should().ContainInOrder("id", "pressure", "label");
            source.RowCount.Should().Be(3);

            // Through DuckDB: derive a column, filter — produces a PipelineTable.
            using var duck = new DuckDbSession();
            source.LoadIntoDuck(duck, "working");
            duck.ReplaceTable("working", "SELECT *, pressure / 1000.0 AS kpsi FROM working WHERE pressure > 1000");
            var afterDuck = PipelineTable.FromDuck(duck, "working", temp);
            afterDuck.Columns.Should().Contain("kpsi");
            afterDuck.RowCount.Should().Be(2);

            // The same table loads straight into ML.NET, and back out to an identical PipelineTable.
            var ml = new MLContext(seed: 1);
            var view = afterDuck.LoadIntoMl(ml, "label");
            var backToTable = PipelineTable.FromMlView(view, temp);
            backToTable.RowCount.Should().Be(2);
            backToTable.Columns.Should().Contain("kpsi");
        }
        finally
        {
            foreach (var f in temp)
            {
                File.Delete(f);
            }
        }
    }
}
