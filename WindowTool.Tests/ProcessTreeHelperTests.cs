using WindowTool.Service;

namespace WindowTool.Tests;

public class ProcessTreeHelperTests {
    [Fact]
    public void CollectProcessGroupIds_IncludesRootAndAllDescendants() {
        var relationships = new[] {
            new ProcessTreeHelper.ProcessRelationship(20, 10),
            new ProcessTreeHelper.ProcessRelationship(30, 20),
            new ProcessTreeHelper.ProcessRelationship(40, 10),
            new ProcessTreeHelper.ProcessRelationship(50, 99),
        };

        HashSet<int> result = ProcessTreeHelper.CollectProcessGroupIds(10, relationships);

        Assert.Equal([10, 20, 30, 40], result.Order());
    }

    [Fact]
    public void CollectProcessGroupIds_StopsWhenProcessRelationshipsContainCycle() {
        var relationships = new[] {
            new ProcessTreeHelper.ProcessRelationship(20, 10),
            new ProcessTreeHelper.ProcessRelationship(10, 20),
        };

        HashSet<int> result = ProcessTreeHelper.CollectProcessGroupIds(10, relationships);

        Assert.Equal([10, 20], result.Order());
    }
}
