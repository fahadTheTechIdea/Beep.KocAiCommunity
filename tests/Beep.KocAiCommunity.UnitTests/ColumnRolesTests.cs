using Beep.KocAiCommunity.ML.Nodes;
using FluentAssertions;
using Microsoft.ML;
using Microsoft.ML.Data;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The first-class column-role split: X (features) / y (label) / id / fold, resolved from a run's label
/// and id and the table's columns — the single source of truth every model node goes through.
/// </summary>
public class ColumnRolesTests
{
    [Fact]
    public void Resolve_separates_features_from_label_id_and_fold_in_column_order()
    {
        var roles = ColumnRoles.Resolve("label", "id", ["id", "x1", "x2", "__fold", "label"]);

        roles.Label.Should().Be("label");
        roles.Id.Should().Be("id");
        roles.Fold.Should().Be("__fold");
        roles.HasId.Should().BeTrue();
        roles.Features.Should().Equal("x1", "x2"); // X = everything that is not a role, in table order
    }

    [Fact]
    public void Resolve_without_an_id_keeps_every_non_label_non_fold_column_as_a_feature()
    {
        var roles = ColumnRoles.Resolve("y", id: null, columns: ["a", "b", "y"]);

        roles.HasId.Should().BeFalse();
        roles.Features.Should().Equal("a", "b");
    }

    [Fact]
    public void Reserved_weight_and_group_columns_are_excluded_from_the_features()
    {
        var roles = ColumnRoles.Resolve("y", "id", ["id", "a", "w", "g", "y"], weight: "w", group: "g");

        roles.Weight.Should().Be("w");
        roles.Group.Should().Be("g");
        roles.Features.Should().Equal("a"); // only the true feature remains as X
    }

    [Fact]
    public void Split_materializes_features_only_X_and_label_only_y_views()
    {
        var ml = new MLContext(seed: 1);
        var path = Path.Combine(Path.GetTempPath(), $"koc-roles-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "id,x1,x2,label\na,1,2,true\nb,3,4,false\n");
        try
        {
            var loader = ml.Data.CreateTextLoader(
            [
                new TextLoader.Column("id", DataKind.String, 0),
                new TextLoader.Column("x1", DataKind.Single, 1),
                new TextLoader.Column("x2", DataKind.Single, 2),
                new TextLoader.Column("label", DataKind.Boolean, 3),
            ], hasHeader: true, separatorChar: ',');
            var view = loader.Load(path);

            var roles = ColumnRoles.Resolve("label", "id", ["id", "x1", "x2", "label"]);
            var (x, y) = roles.Split(ml, view);

            x.Schema.Select(c => c.Name).Should().Equal("x1", "x2"); // X: features only, id and label dropped
            y.Schema.Select(c => c.Name).Should().Equal("label");    // y: the target only
        }
        finally
        {
            File.Delete(path);
        }
    }
}
