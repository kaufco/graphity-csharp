namespace Graphity.Core.Tests;

public sealed class BackEdgesTests
{
    [Fact]
    public void OfEmptyGraphIsEmpty() => Assert.Equal(0, BackEdges.Of(TestGraphs.Empty()).Count);

    [Fact]
    public void OfDagHasNoBackEdges() =>
        Assert.Equal(0, BackEdges.Of(TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3)).Count);

    [Fact]
    public void OfPathHasNoBackEdges() => Assert.Equal(0, BackEdges.Of(TestGraphs.Path(5)).Count);

    [Fact]
    public void OfCycleHasOneBackEdge()
    {
        // 0 -> 1 -> 2 -> 3 -> 0: DFS from 0 descends to 3, and 3 -> 0 is a back edge.
        var be = BackEdges.Of(TestGraphs.Cycle(4));
        Assert.Equal(1, be.Count);
        Assert.Equal([3], be.SourceNodes.ToArray());
        Assert.Equal([0], be.TargetNodes.ToArray());
        Assert.Equal([3], be.EdgeIndices.ToArray());
    }

    [Fact]
    public void OfSelfLoopIsBackEdge()
    {
        var be = BackEdges.Of(TestGraphs.Of(1, 0, 0));
        Assert.Equal(1, be.Count);
        Assert.Equal([0], be.SourceNodes.ToArray());
        Assert.Equal([0], be.TargetNodes.ToArray());
    }

    [Fact]
    public void RemovingBackEdgesYieldsDag()
    {
        // Cycle {0,1,2} + bridge 2 -> 3 + cycle {3, 4, 5}.
        var g = TestGraphs.Of(6,
            0, 1, 1, 2, 2, 0,
            2, 3,
            3, 4, 4, 5, 5, 3);
        var be = BackEdges.Of(g);
        Assert.True(be.Count > 0);
        var backEdgeSet = new HashSet<long>();
        for (int i = 0; i < be.Count; i++)
        {
            backEdgeSet.Add(((long)be.SourceNodes[i] << 32) | (uint)be.TargetNodes[i]);
        }
        var builder = new GraphBuilder();
        builder.AddNodes(g.NodeCount);
        for (int u = 0; u < g.NodeCount; u++)
        {
            foreach (int v in g.OutNodes(u))
            {
                if (!backEdgeSet.Contains(((long)u << 32) | (uint)v))
                {
                    builder.AddEdge(u, v);
                }
            }
        }
        Assert.True(Graphs.IsDag(builder.Build()));
    }

    [Fact]
    public void OfMultipleDisjointCyclesFindsBackEdgePerCycle()
    {
        var g = TestGraphs.Of(6,
            0, 1, 1, 2, 2, 0,
            3, 4, 4, 5, 5, 3);
        Assert.Equal(2, BackEdges.Of(g).Count);
    }

    [Fact]
    public void OfGraphWithForwardEdgeHasNoBackEdge() =>
        // 0 -> 1, 0 -> 2, 1 -> 2: 0 -> 2 is a forward edge.
        Assert.Equal(0, BackEdges.Of(TestGraphs.Of(3, 0, 1, 0, 2, 1, 2)).Count);

    [Fact]
    public void OfGraphWithCrossEdgeHasNoBackEdge() =>
        // 0 -> 1, 0 -> 2, 2 -> 1: 2 -> 1 is a cross edge.
        Assert.Equal(0, BackEdges.Of(TestGraphs.Of(3, 0, 1, 0, 2, 2, 1)).Count);
}
