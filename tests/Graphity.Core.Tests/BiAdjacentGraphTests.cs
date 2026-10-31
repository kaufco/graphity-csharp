namespace Graphity.Core.Tests;

public sealed class BiAdjacentGraphTests
{
    /// <summary>Standard fixture: diamond 0->1, 0->2, 1->3, 2->3.</summary>
    private static BiAdjacentGraph Diamond() => BiAdjacentGraph.Of(TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3));

    [Fact]
    public void InDegreeReportsIncomingEdges()
    {
        var g = Diamond();
        Assert.Equal(0, g.InDegree(0));
        Assert.Equal(1, g.InDegree(1));
        Assert.Equal(1, g.InDegree(2));
        Assert.Equal(2, g.InDegree(3));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void InDegreeOutOfRangeThrows(int node) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Diamond().InDegree(node));

    [Fact]
    public void DegreeIsSumOfInAndOut()
    {
        var g = Diamond();
        for (int i = 0; i < g.NodeCount; i++)
        {
            Assert.Equal(g.InDegree(i) + g.OutDegree(i), g.Degree(i));
        }
    }

    [Fact]
    public void DegreeCountsSelfLoopTwice()
    {
        // Self-loop 0->0 contributes to in-degree and out-degree independently.
        var g = BiAdjacentGraph.Of(TestGraphs.Of(1, 0, 0));
        Assert.Equal(1, g.InDegree(0));
        Assert.Equal(1, g.OutDegree(0));
        Assert.Equal(2, g.Degree(0));
    }

    [Fact]
    public void DegreeCountsReciprocalPairTwice()
    {
        // 0->1 and 1->0: node 1 is both out- and in-node of 0, but a single neighbor.
        var g = BiAdjacentGraph.Of(TestGraphs.Of(2, 0, 1, 1, 0));
        Assert.Equal(2, g.Degree(0));
        Assert.Equal([1], g.Neighbors(0).ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void DegreeOutOfRangeThrows(int node) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Diamond().Degree(node));

    [Fact]
    public void InNodesReturnsSortedView()
    {
        var g = Diamond();
        Assert.Equal([1, 2], g.InNodes(3).ToArray());
        Assert.Empty(g.InNodes(0).ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void InNodesOutOfRangeThrows(int node) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Diamond().InNodes(node));

    [Fact]
    public void HasInNodeDetectsIncomingEdges()
    {
        var g = Diamond();
        Assert.True(g.HasInNode(3, 1));
        Assert.True(g.HasInNode(3, 2));
        Assert.False(g.HasInNode(3, 0));
        Assert.False(g.HasInNode(0, 1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void HasInNodeOutOfRangeNodeThrows(int node) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Diamond().HasInNode(node, 0));

    [Fact]
    public void OfReturnsABiAdjacentGraphUnchanged()
    {
        var g = Diamond();
        Assert.Same(g, BiAdjacentGraph.Of(g));
    }

    [Fact]
    public void UniAdjacentGraphReturnsEqualUniGraph()
    {
        var bi = Diamond();
        var uni = bi.UniAdjacentGraph();
        Assert.True(uni.Equals(bi));
        Assert.IsNotType<BiAdjacentGraph>(uni);
    }

    [Fact]
    public void TransposedSwapsInAndOutEdges()
    {
        var g = Diamond();
        var t = g.Transposed();
        for (int i = 0; i < g.NodeCount; i++)
        {
            Assert.Equal(g.InNodes(i).ToArray(), t.OutNodes(i).ToArray());
            Assert.Equal(g.OutNodes(i).ToArray(), t.InNodes(i).ToArray());
        }
    }

    [Fact]
    public void TransposedIsBiAdjacent() => Assert.IsType<BiAdjacentGraph>(Diamond().Transposed());

    [Fact]
    public void TransposedTwiceEqualsOriginal()
    {
        var g = Diamond();
        Assert.True(g.Transposed().Transposed().Equals(g));
    }

    [Fact]
    public void TransposedEdgeIndexSwapsEndpoints()
    {
        var g = Diamond();
        var t = g.Transposed();
        for (int edgeIndex = 0; edgeIndex < g.EdgeCount; edgeIndex++)
        {
            int transposedIndex = g.TransposedEdgeIndex(edgeIndex);
            Assert.Equal(g.EdgeTarget(edgeIndex), t.EdgeSource(transposedIndex));
            Assert.Equal(g.EdgeSource(edgeIndex), t.EdgeTarget(transposedIndex));
        }
    }

    [Fact]
    public void TransposedEdgeIndexIsItsOwnInverseAcrossTheTranspose()
    {
        var g = Diamond();
        var t = g.Transposed();
        for (int edgeIndex = 0; edgeIndex < g.EdgeCount; edgeIndex++)
        {
            Assert.Equal(edgeIndex, t.TransposedEdgeIndex(g.TransposedEdgeIndex(edgeIndex)));
        }
    }

    [Fact]
    public void TransposedEdgeIndexRelabelsEdges()
    {
        // 0->2 is edge 0, 1->0 is edge 1. In the transpose 0->1 comes first, so the two swap.
        var g = BiAdjacentGraph.Of(TestGraphs.Of(3, 1, 0, 0, 2));
        Assert.Equal(1, g.TransposedEdgeIndex(0));
        Assert.Equal(0, g.TransposedEdgeIndex(1));
    }

    [Fact]
    public void TransposedEdgeIndexHandlesSelfLoop()
    {
        // Node 1 has a self-loop (edge 1) next to 1->2 (edge 2); 0->1 is edge 0.
        var g = BiAdjacentGraph.Of(TestGraphs.Of(3, 0, 1, 1, 1, 1, 2));
        int loop = g.TransposedEdgeIndex(1);
        var t = g.Transposed();
        Assert.Equal(1, t.EdgeSource(loop));
        Assert.Equal(1, t.EdgeTarget(loop));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void TransposedEdgeIndexOutOfRangeThrows(int edgeIndex) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Diamond().TransposedEdgeIndex(edgeIndex));

    [Fact]
    public void NeighborsMergesInAndOutSorted()
    {
        // Node 2 has out-neighbor {3} and in-neighbor {0}; expect [0, 3].
        var g = Diamond();
        Assert.Equal([1, 2], g.Neighbors(0).ToArray());
        Assert.Equal([0, 3], g.Neighbors(2).ToArray());
        Assert.Equal([1, 2], g.Neighbors(3).ToArray());
    }

    [Fact]
    public void NeighborsDeduplicatesOverlapBetweenInAndOut()
    {
        // Bidirectional edge 0<->1: appears in both in-list and out-list of each.
        var g = BiAdjacentGraph.Of(TestGraphs.Of(2, 0, 1, 1, 0));
        Assert.Equal([1], g.Neighbors(0).ToArray());
        Assert.Equal([0], g.Neighbors(1).ToArray());
    }

    [Fact]
    public void NeighborsIncludesSelfOnSelfLoop()
    {
        var g = BiAdjacentGraph.Of(TestGraphs.Of(2, 0, 0, 0, 1));
        Assert.Equal([0, 1], g.Neighbors(0).ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void NeighborsOutOfRangeThrows(int node) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Diamond().Neighbors(node));

    [Fact]
    public void HasNeighborDetectsEitherDirection()
    {
        var g = Diamond();
        Assert.True(g.HasNeighbor(0, 1));
        Assert.True(g.HasNeighbor(1, 0));
        Assert.True(g.HasNeighbor(3, 1));
        Assert.True(g.HasNeighbor(1, 3));
        Assert.False(g.HasNeighbor(1, 2));
    }

    [Fact]
    public void IsRootNodeTrueForSources()
    {
        var g = Diamond();
        Assert.True(g.IsRootNode(0));
        Assert.False(g.IsRootNode(1));
        Assert.False(g.IsRootNode(2));
        Assert.False(g.IsRootNode(3));
    }

    [Fact]
    public void IsRootNodeFalseOnSelfLoop() =>
        Assert.False(BiAdjacentGraph.Of(TestGraphs.Of(1, 0, 0)).IsRootNode(0));
}
