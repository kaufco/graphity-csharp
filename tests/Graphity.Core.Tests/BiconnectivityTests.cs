namespace Graphity.Core.Tests;

public sealed class BiconnectivityTests
{
    private static Biconnectivity BiconnectivityOf(Graph graph) => Components.BiconnectivityOf(BiAdjacentGraph.Of(graph));

    /// <summary>Two triangles sharing node 0, plus a pendant chain 5 - 6 hanging off node 3.</summary>
    private static Graph BowtieWithTail() => TestGraphs.Of(7, 0, 1, 1, 2, 2, 0, 0, 3, 3, 4, 4, 0, 3, 5, 5, 6);

    [Fact]
    public void EveryEdgeOfAPathIsABridge()
    {
        var b = BiconnectivityOf(TestGraphs.Path(4));
        Assert.Equal(3, b.Bridges.Count);
        Assert.Equal([0, 1, 2], b.Bridges.SourceNodes.ToArray());
        Assert.Equal([1, 2, 3], b.Bridges.TargetNodes.ToArray());
    }

    [Fact]
    public void InnerNodesOfAPathAreArticulationPoints()
    {
        var b = BiconnectivityOf(TestGraphs.Path(4));
        Assert.Equal([1, 2], b.ArticulationPoints.ToArray());
    }

    [Fact]
    public void EveryEdgeOfAPathIsItsOwnBlock()
    {
        var b = BiconnectivityOf(TestGraphs.Path(4));
        Assert.Equal(3, b.Blocks.GroupCount);
        for (int block = 0; block < 3; block++)
        {
            Assert.Equal(1, b.Blocks.SizeOf(block));
        }
    }

    [Fact]
    public void ACycleHasNoBridgeAndNoArticulationPoint()
    {
        var b = BiconnectivityOf(TestGraphs.Cycle(5));
        Assert.True(b.Bridges.IsEmpty);
        Assert.True(b.ArticulationPoints.IsEmpty);
    }

    [Fact]
    public void ACycleIsOneBlock()
    {
        var b = BiconnectivityOf(TestGraphs.Cycle(5));
        Assert.Equal(1, b.Blocks.GroupCount);
        Assert.Equal([0, 1, 2, 3, 4], b.Blocks.EdgesOf(0).ToArray());
    }

    [Fact]
    public void AReciprocalPairIsOneUndirectedEdgeAndThusABridge()
    {
        // Storing an undirected edge in both directions must not turn it into a 2-cycle.
        var b = BiconnectivityOf(TestGraphs.Of(2, 0, 1, 1, 0));
        Assert.Equal(1, b.Bridges.Count);
        Assert.Equal(0, b.Bridges.SourceNodes[0]);
        Assert.Equal(1, b.Bridges.TargetNodes[0]);
    }

    [Fact]
    public void BothEdgeIndicesOfAReciprocalPairShareABlock()
    {
        var b = BiconnectivityOf(TestGraphs.Of(2, 0, 1, 1, 0));
        Assert.Equal(1, b.Blocks.GroupCount);
        Assert.Equal([0, 1], b.Blocks.EdgesOf(0).ToArray());
    }

    [Fact]
    public void SymmetricStorageYieldsTheSameResultAsSingleDirection()
    {
        var single = BiconnectivityOf(TestGraphs.Path(4));
        // The same path with every edge stored in both directions.
        var both = BiconnectivityOf(TestGraphs.Of(4, 0, 1, 1, 0, 1, 2, 2, 1, 2, 3, 3, 2));
        Assert.Equal(single.Bridges.SourceNodes.ToArray(), both.Bridges.SourceNodes.ToArray());
        Assert.Equal(single.Bridges.TargetNodes.ToArray(), both.Bridges.TargetNodes.ToArray());
        Assert.Equal(single.ArticulationPoints.ToArray(), both.ArticulationPoints.ToArray());
        Assert.Equal(single.Blocks.GroupCount, both.Blocks.GroupCount);
    }

    [Fact]
    public void ArticulationPointJoinsTwoTriangles()
    {
        // Two triangles sharing node 0, nothing else.
        var b = BiconnectivityOf(TestGraphs.Of(5, 0, 1, 1, 2, 2, 0, 0, 3, 3, 4, 4, 0));
        Assert.Equal([0], b.ArticulationPoints.ToArray());
        Assert.True(b.Bridges.IsEmpty);
        Assert.Equal(2, b.Blocks.GroupCount);
        Assert.Equal(3, b.Blocks.SizeOf(0));
        Assert.Equal(3, b.Blocks.SizeOf(1));
    }

    [Fact]
    public void BridgeAndBlocksCoexist()
    {
        var b = BiconnectivityOf(BowtieWithTail());
        Assert.Equal([3, 5], b.Bridges.SourceNodes.ToArray());
        Assert.Equal([5, 6], b.Bridges.TargetNodes.ToArray());
        Assert.Equal([0, 3, 5], b.ArticulationPoints.ToArray());
        // Two triangles plus the two bridges.
        Assert.Equal(4, b.Blocks.GroupCount);
    }

    [Fact]
    public void EveryBridgeLiesInABlockOfItsOwn()
    {
        var b = BiconnectivityOf(BowtieWithTail());
        var graph = BowtieWithTail();
        for (int i = 0; i < b.Bridges.Count; i++)
        {
            int source = b.Bridges.SourceNodes[i];
            int target = b.Bridges.TargetNodes[i];
            int edgeIndex = EdgeIndexOf(graph, source, target);
            Assert.Equal(1, b.Blocks.SizeOf(b.Blocks.Get(edgeIndex)));
        }
    }

    [Fact]
    public void SelfLoopsAreUnmapped()
    {
        // Triangle 0-1-2 plus a self-loop at node 1.
        var g = TestGraphs.Of(3, 0, 1, 1, 1, 1, 2, 2, 0);
        var b = BiconnectivityOf(g);
        Assert.True(b.Blocks.IsPartial);
        Assert.Equal(-1, b.Blocks.Get(1));
        Assert.Equal(3, b.Blocks.MappedCount);
        Assert.True(b.Bridges.IsEmpty);
        Assert.True(b.ArticulationPoints.IsEmpty);
    }

    [Fact]
    public void BlocksAreTotalWithoutSelfLoops()
    {
        var b = BiconnectivityOf(TestGraphs.Cycle(5));
        Assert.False(b.Blocks.IsPartial);
        Assert.Equal(5, b.Blocks.MappedCount);
    }

    [Fact]
    public void AGraphOfOnlySelfLoopsHasNothing()
    {
        var b = BiconnectivityOf(TestGraphs.Of(3, 0, 0, 1, 1, 2, 2));
        Assert.True(b.Bridges.IsEmpty);
        Assert.True(b.ArticulationPoints.IsEmpty);
        Assert.Equal(0, b.Blocks.GroupCount);
        Assert.Equal(0, b.Blocks.MappedCount);
    }

    [Fact]
    public void IsolatedNodesAreNeitherArticulationPointsNorBlocks()
    {
        var b = BiconnectivityOf(TestGraphs.Isolated(4));
        Assert.True(b.Bridges.IsEmpty);
        Assert.True(b.ArticulationPoints.IsEmpty);
        Assert.Equal(0, b.Blocks.GroupCount);
    }

    [Fact]
    public void TheEmptyGraphHasNothing()
    {
        var b = BiconnectivityOf(TestGraphs.Empty());
        Assert.True(b.Bridges.IsEmpty);
        Assert.True(b.ArticulationPoints.IsEmpty);
        Assert.Equal(0, b.Blocks.GroupCount);
        Assert.Equal(0, b.Blocks.EdgeCount);
    }

    [Fact]
    public void ACliqueHasNoArticulationPoint()
    {
        var b = BiconnectivityOf(TestGraphs.Clique(5));
        Assert.True(b.ArticulationPoints.IsEmpty);
        Assert.True(b.Bridges.IsEmpty);
        Assert.Equal(1, b.Blocks.GroupCount);
    }

    [Fact]
    public void SeparateComponentsAreHandledIndependently()
    {
        // Path 0-1 and triangle 2-3-4.
        var b = BiconnectivityOf(TestGraphs.Of(5, 0, 1, 2, 3, 3, 4, 4, 2));
        Assert.Equal([0], b.Bridges.SourceNodes.ToArray());
        Assert.Equal([1], b.Bridges.TargetNodes.ToArray());
        Assert.True(b.ArticulationPoints.IsEmpty);
        Assert.Equal(2, b.Blocks.GroupCount);
    }

    /// <summary>Edge index of <c>source → target</c>, or of the reverse edge if only that one exists.</summary>
    private static int EdgeIndexOf(Graph graph, int source, int target)
    {
        for (int edgeIndex = graph.OutEdgeStart(source); edgeIndex < graph.OutEdgeEnd(source); edgeIndex++)
        {
            if (graph.EdgeTarget(edgeIndex) == target)
            {
                return edgeIndex;
            }
        }
        for (int edgeIndex = graph.OutEdgeStart(target); edgeIndex < graph.OutEdgeEnd(target); edgeIndex++)
        {
            if (graph.EdgeTarget(edgeIndex) == source)
            {
                return edgeIndex;
            }
        }
        throw new ArgumentException($"no edge between {source} and {target}");
    }
}
