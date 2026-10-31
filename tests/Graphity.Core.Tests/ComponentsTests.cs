namespace Graphity.Core.Tests;

/// <summary>
/// Covers the <see cref="Components"/> factory: <c>SccsOf</c>, <c>WccsOf</c>, <c>CcsOf</c>. Inherited
/// partition behavior is covered in <see cref="NodePartitionTests"/>, the Sccs-specific members in
/// <see cref="SccsTests"/>. Here: the factory builds the right partition, and SCC IDs follow the
/// documented reverse topological order.
/// </summary>
public sealed class ComponentsTests
{
    [Fact]
    public void SccsOfEmptyGraphHasNoGroups()
    {
        var sccs = Components.SccsOf(TestGraphs.Empty());
        Assert.Equal(0, sccs.GroupCount);
    }

    [Fact]
    public void SccsOfIsolatedNodesAreEachTheirOwnScc()
    {
        var sccs = Components.SccsOf(TestGraphs.Isolated(4));
        Assert.Equal(4, sccs.GroupCount);
    }

    [Fact]
    public void SccsOfPureCycleIsSingleScc()
    {
        var sccs = Components.SccsOf(TestGraphs.Cycle(4));
        Assert.Equal(1, sccs.GroupCount);
        Assert.Equal([0, 1, 2, 3], sccs.NodesOf(0).ToArray());
    }

    [Fact]
    public void SccsOfDagHasOneSccPerNode()
    {
        // Diamond DAG 0->1, 0->2, 1->3, 2->3.
        var sccs = Components.SccsOf(TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3));
        Assert.Equal(4, sccs.GroupCount);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(1, sccs.SizeOf(sccs.Get(i)));
        }
    }

    [Fact]
    public void SccsOfSelfLoopedNodeFormsSingletonScc()
    {
        var sccs = Components.SccsOf(TestGraphs.Of(1, 0, 0));
        Assert.Equal(1, sccs.GroupCount);
        Assert.Equal([0], sccs.NodesOf(0).ToArray());
    }

    [Fact]
    public void SccsOfTwoDisjointSccsFindsBoth()
    {
        // Two disjoint cycles: {0,1,2} and {3,4,5}.
        var g = TestGraphs.Of(6,
            0, 1, 1, 2, 2, 0,
            3, 4, 4, 5, 5, 3);
        var sccs = Components.SccsOf(g);
        Assert.Equal(2, sccs.GroupCount);
        Assert.Equal(3, sccs.SizeOf(0));
        Assert.Equal(3, sccs.SizeOf(1));
        Assert.Equal(sccs.Get(0), sccs.Get(1));
        Assert.Equal(sccs.Get(1), sccs.Get(2));
        Assert.Equal(sccs.Get(3), sccs.Get(4));
        Assert.Equal(sccs.Get(4), sccs.Get(5));
        Assert.NotEqual(sccs.Get(0), sccs.Get(3));
    }

    [Fact]
    public void SccsOfChainOfSingletonsAssignsReverseTopoIds()
    {
        // 0 -> 1 -> 2 -> 3: sink first, so node 3 gets ID 0, node 2 ID 1, node 1 ID 2, node 0 ID 3.
        var sccs = Components.SccsOf(TestGraphs.Path(4));
        Assert.Equal(0, sccs.Get(3));
        Assert.Equal(1, sccs.Get(2));
        Assert.Equal(2, sccs.Get(1));
        Assert.Equal(3, sccs.Get(0));
    }

    [Fact]
    public void SccsOfTextbookGraphAssignsIdsInReverseTopoOrder()
    {
        // 0 -> 1; 1 -> 2, 1 -> 4; cycle {2, 3}; cycle {4, 5, 6}.
        var g = TestGraphs.Of(7,
            0, 1,
            1, 2, 1, 4,
            2, 3, 3, 2,
            4, 5, 5, 6, 6, 4);
        var sccs = Components.SccsOf(g);
        Assert.Equal(4, sccs.GroupCount);
        AssertReverseTopoInvariant(g, sccs);
    }

    [Fact]
    public void SccsOfMixedGraphMaintainsReverseTopoInvariant()
    {
        // Cycle {0,1}, DAG 2 -> 0, 2 -> 3, 3 -> 4, cycle {5,6,7}, 4 -> 5.
        var g = TestGraphs.Of(8,
            0, 1, 1, 0,
            2, 0, 2, 3,
            3, 4,
            4, 5,
            5, 6, 6, 7, 7, 5);
        var sccs = Components.SccsOf(g);
        Assert.Equal(5, sccs.GroupCount);
        AssertReverseTopoInvariant(g, sccs);
    }

    [Fact]
    public void SccsOfCondenseIsDag()
    {
        // Two SCCs connected by a bridge edge.
        var g = TestGraphs.Of(4,
            0, 1, 1, 0,
            1, 2,
            2, 3, 3, 2);
        var sccs = Components.SccsOf(g);
        var condensed = sccs.Condense();
        Assert.Equal(sccs.GroupCount, condensed.NodeCount);
        Assert.True(Graphs.IsDag(condensed));
    }

    [Fact]
    public void WccsOfEmptyGraphHasNoGroups()
    {
        var wccs = Components.WccsOf(TestGraphs.Empty());
        Assert.Equal(0, wccs.GroupCount);
    }

    [Fact]
    public void WccsOfIsolatedNodesAreEachOwnWcc()
    {
        var wccs = Components.WccsOf(TestGraphs.Isolated(4));
        Assert.Equal(4, wccs.GroupCount);
    }

    [Fact]
    public void WccsOfConnectedChainIsSingleWcc()
    {
        var wccs = Components.WccsOf(TestGraphs.Path(5));
        Assert.Equal(1, wccs.GroupCount);
        Assert.Equal([0, 1, 2, 3, 4], wccs.NodesOf(0).ToArray());
    }

    [Fact]
    public void WccsOfIgnoresEdgeDirection()
    {
        // 0 -> 1, 2 -> 1: one WCC {0, 1, 2}; the SCCs would be three singletons.
        var g = TestGraphs.Of(3, 0, 1, 2, 1);
        var wccs = Components.WccsOf(g);
        Assert.Equal(1, wccs.GroupCount);
        Assert.Equal([0, 1, 2], wccs.NodesOf(0).ToArray());
    }

    [Fact]
    public void WccsOfTwoDisjointComponents()
    {
        // {0, 1, 2} via 0->1, 1->2; {3, 4} via 3->4.
        var g = TestGraphs.Of(5, 0, 1, 1, 2, 3, 4);
        var wccs = Components.WccsOf(g);
        Assert.Equal(2, wccs.GroupCount);
        int g1 = wccs.Get(0);
        int g2 = wccs.Get(3);
        Assert.NotEqual(g1, g2);
        Assert.Equal([0, 1, 2], wccs.NodesOf(g1).ToArray());
        Assert.Equal([3, 4], wccs.NodesOf(g2).ToArray());
    }

    [Fact]
    public void WccsOfSelfLoopStandsAlone()
    {
        // {0} with a self-loop, {1, 2} via 1->2.
        var g = TestGraphs.Of(3, 0, 0, 1, 2);
        var wccs = Components.WccsOf(g);
        Assert.Equal(2, wccs.GroupCount);
    }

    [Fact]
    public void CcsOfMatchesWccsOf()
    {
        var g = TestGraphs.Of(5, 0, 1, 1, 2, 3, 4);
        var ccs = Components.CcsOf(g);
        var wccs = Components.WccsOf(g);
        Assert.Equal(wccs.GroupCount, ccs.GroupCount);
        for (int i = 0; i < g.NodeCount; i++)
        {
            Assert.Equal(wccs.Get(i), ccs.Get(i));
        }
    }

    /// <summary>For every edge u -> v between different SCCs, <c>sccs.Get(u) &gt; sccs.Get(v)</c>.</summary>
    private static void AssertReverseTopoInvariant(Graph graph, Sccs sccs)
    {
        for (int u = 0; u < graph.NodeCount; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                int suId = sccs.Get(u);
                int svId = sccs.Get(v);
                if (suId != svId)
                {
                    Assert.True(suId > svId,
                        $"cross-SCC edge {u} -> {v}: sccs.Get({u})={suId} must be > sccs.Get({v})={svId}");
                }
            }
        }
    }
}
