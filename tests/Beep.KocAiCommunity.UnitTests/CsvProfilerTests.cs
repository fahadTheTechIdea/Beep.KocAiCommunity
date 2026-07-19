using System.Text;
using Beep.KocAiCommunity.Infrastructure.Datasets;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class CsvProfilerTests
{
    private static Stream Csv(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public void Infers_types_nullability_and_stats()
    {
        const string csv = "age,ratio,name,active\n" +
                           "30,1.5,alice,true\n" +
                           "40,2.5,bob,false\n" +
                           ",3.5,carol,true\n";   // age has one null

        var result = CsvProfiler.Profile(Csv(csv));

        result.TotalRows.Should().Be(3);
        result.Columns.Should().HaveCount(4);

        var age = result.Columns.Single(c => c.Name == "age");
        age.DataType.Should().Be("integer");
        age.Nullable.Should().BeTrue();
        age.NullCount.Should().Be(1);
        age.Min.Should().Be(30);
        age.Max.Should().Be(40);
        age.Mean.Should().Be(35);

        result.Columns.Single(c => c.Name == "ratio").DataType.Should().Be("number");
        result.Columns.Single(c => c.Name == "name").DataType.Should().Be("string");
        result.Columns.Single(c => c.Name == "active").DataType.Should().Be("boolean");
        result.Columns.Single(c => c.Name == "name").DistinctCount.Should().Be(3);
    }

    [Fact]
    public void Profile_is_reproducible()
    {
        const string csv = "x,y\n1,10\n2,20\n3,30\n";
        var a = CsvProfiler.Profile(Csv(csv));
        var b = CsvProfiler.Profile(Csv(csv));

        a.Should().BeEquivalentTo(b);
    }
}
