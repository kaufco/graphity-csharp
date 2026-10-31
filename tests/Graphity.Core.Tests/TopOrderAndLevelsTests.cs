using System.Collections.Immutable;

namespace Graphity.Core.Tests;

/// <remarks>
/// Java's <c>ofReturnsUnmodifiableLists</c> has no counterpart beyond the type: <see cref="TopOrderAndLevels.Order"/>
/// and <see cref="TopOrderAndLevels.Levels"/> are <see cref="ImmutableArray{T}"/>s without mutators.
/// </remarks>
public sealed class TopOrderAndLevelsTests
{
    [Fact]
    public void OfEmptyGraphHasZeroLevels()
    {
        var tol = TopOrderAndLevels.Of(TestGraphs.Empty());
        Assert.True(tol.Order.IsEmpty);
        Assert.True(tol.Levels.IsEmpty);
        Assert.Equal(0, tol.LevelCount);
    }

    [Fact]
    public void OfIsolatedNodesPutsAllAtLevelZero()
    {
        var tol = TopOrderAndLevels.Of(TestGraphs.Isolated(3));
        Assert.Equal(3, tol.Order.Length);
        Assert.Equal(1, tol.LevelCount);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(0, tol.LevelOf(i));
        }
    }

    [Fact]
    public void OfPathAssignsIncrementingLevels()
    {
        var tol = TopOrderAndLevels.Of(TestGraphs.Path(5));
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(i, tol.LevelOf(i));
        }
        Assert.Equal(5, tol.LevelCount);
    }

    [Fact]
    public void OfDiamondAssignsSharedLevels()
    {
        // 0 -> 1, 0 -> 2, 1 -> 3, 2 -> 3.
        var tol = TopOrderAndLevels.Of(TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3));
        Assert.Equal(0, tol.LevelOf(0));
        Assert.Equal(1, tol.LevelOf(1));
        Assert.Equal(1, tol.LevelOf(2));
        Assert.Equal(2, tol.LevelOf(3));
        Assert.Equal(3, tol.LevelCount);
    }

    [Fact]
    public void OfLevelIsMaxPredecessorPlusOne()
    {
        // 0 -> 1, 0 -> 3, 1 -> 2, 2 -> 3: node 3 is one above its deepest predecessor 2.
        var tol = TopOrderAndLevels.Of(TestGraphs.Of(4, 0, 1, 0, 3, 1, 2, 2, 3));
        Assert.Equal(0, tol.LevelOf(0));
        Assert.Equal(1, tol.LevelOf(1));
        Assert.Equal(2, tol.LevelOf(2));
        Assert.Equal(3, tol.LevelOf(3));
    }

    [Fact]
    public void OfDisconnectedDagsAssignsIndependentLevels()
    {
        // {0 -> 1} and {2 -> 3 -> 4}.
        var tol = TopOrderAndLevels.Of(TestGraphs.Of(5, 0, 1, 2, 3, 3, 4));
        Assert.Equal(0, tol.LevelOf(0));
        Assert.Equal(1, tol.LevelOf(1));
        Assert.Equal(0, tol.LevelOf(2));
        Assert.Equal(1, tol.LevelOf(3));
        Assert.Equal(2, tol.LevelOf(4));
        Assert.Equal(3, tol.LevelCount);
    }

    [Fact]
    public void OfOrderSatisfiesTopologicalInvariant()
    {
        var g = TestGraphs.Of(6, 0, 1, 0, 2, 1, 3, 2, 3, 3, 4, 3, 5);
        AssertInvariantSourceBeforeTarget(g, TopOrderAndLevels.Of(g).Order);
    }

    [Fact]
    public void OfLevelsAndLevelOfAgreeForAllNodes()
    {
        var g = TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3);
        var tol = TopOrderAndLevels.Of(g);
        for (int i = 0; i < g.NodeCount; i++)
        {
            Assert.Equal(tol.LevelOf(i), tol.Levels[i]);
        }
    }

    [Fact]
    public void OfGraphWithCycleThrows()
    {
        var e = Assert.Throws<ArgumentException>(() => TopOrderAndLevels.Of(TestGraphs.Cycle(3)));
        Assert.StartsWith("graph contains a cycle", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OfGraphWithSelfLoopThrows() =>
        Assert.Throws<ArgumentException>(() => TopOrderAndLevels.Of(TestGraphs.Of(2, 0, 0, 0, 1)));

    [Fact]
    public void OfUsesBiAdjacentFastPathConsistently()
    {
        // The same graph via Graph and via BiAdjacentGraph.
        var g = TestGraphs.Of(5, 0, 1, 0, 2, 1, 3, 2, 3, 3, 4);
        var tolUni = TopOrderAndLevels.Of(g);
        var tolBi = TopOrderAndLevels.Of(BiAdjacentGraph.Of(g));
        Assert.Equal(tolBi.Order.ToArray(), tolUni.Order.ToArray());
        Assert.Equal(tolBi.Levels.ToArray(), tolUni.Levels.ToArray());
        Assert.Equal(tolBi.LevelCount, tolUni.LevelCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void LevelOfOutOfRangeThrows(int node)
    {
        var tol = TopOrderAndLevels.Of(TestGraphs.Path(4));
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => tol.LevelOf(node));
        Assert.Contains($"Index {node} out of bounds for length 4", e.Message);
    }

    private static void AssertInvariantSourceBeforeTarget(Graph graph, ImmutableArray<int> order)
    {
        var position = new int[graph.NodeCount];
        for (int i = 0; i < order.Length; i++)
        {
            position[order[i]] = i;
        }
        for (int u = 0; u < graph.NodeCount; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                Assert.True(position[u] < position[v]);
            }
        }
    }
}
