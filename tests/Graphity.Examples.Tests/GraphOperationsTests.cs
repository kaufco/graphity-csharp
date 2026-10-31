using System.Collections.Immutable;

namespace Examples.Tests;

public sealed class GraphOperationsTests
{
    [Fact]
    public void FindCycleReturnsModelAndStorageClosedByStorageToModel()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        ImmutableArray<int> cycle = GraphOperations.CycleInDependencies();
        Assert.Equal([4, 5], cycle.AsSpan().ToArray());
        Assert.Equal(["model", "storage"], Names.Of(cycle, names));
        Assert.True(dependencies.HasOutNode(cycle[1], cycle[0]));
    }

    [Fact]
    public void AdjacencyListOfListsTheWholeControlFlowOfLoad() =>
        Assert.Equal("""
            0 -> [1]
            1 -> [2, 5]
            2 -> [3, 4]
            3 -> [4]
            4 -> [1]
            5 -> []

            """, GraphOperations.CfgAsAdjacencyList());
}
