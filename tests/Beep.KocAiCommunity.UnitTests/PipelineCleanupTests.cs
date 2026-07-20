using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.ML.Nodes;
using FluentAssertions;
using Microsoft.ML;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>Every run's scratch CSVs and in-memory DuckDB are tracked and released; orphans get swept.</summary>
public class PipelineCleanupTests
{
    [Fact]
    public void Context_disposal_releases_its_scratch_files_and_duckdb()
    {
        string a, b;
        DuckDbSession duck;
        using (var ctx = new PipelineContext
        {
            Ml = new MLContext(seed: 1),
            Task = MlTaskType.BinaryClassification,
            Mode = PipelineMode.Execute,
            LabelColumn = "label",
        })
        {
            a = ctx.NewTemp();
            b = ctx.NewTemp();
            File.WriteAllText(a, "x\n1\n");
            File.WriteAllText(b, "x\n1\n");
            duck = ctx.Duck;                 // open the in-memory session
            duck.Execute("CREATE TABLE t(a INTEGER);");
        }

        // Disposal deleted every tracked scratch file and closed the DuckDB session.
        File.Exists(a).Should().BeFalse();
        File.Exists(b).Should().BeFalse();
        var afterDispose = () => duck.Execute("SELECT 1;");
        afterDispose.Should().Throw<Exception>("the session is disposed");
    }

    [Fact]
    public void Sweep_reclaims_only_stale_orphans()
    {
        var stale = PipelineTemp.New();
        var fresh = PipelineTemp.New();
        File.WriteAllText(stale, "x\n1\n");
        File.WriteAllText(fresh, "x\n1\n");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-5));
        try
        {
            PipelineTemp.SweepOrphans(TimeSpan.FromHours(2));
            File.Exists(stale).Should().BeFalse("it is older than the cutoff");
            File.Exists(fresh).Should().BeTrue("a recent file may belong to a running pipeline");
        }
        finally
        {
            PipelineTemp.Delete(stale);
            PipelineTemp.Delete(fresh);
        }
    }

}
