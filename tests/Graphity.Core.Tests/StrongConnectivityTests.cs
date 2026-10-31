namespace Graphity.Core.Tests;

public sealed class StrongConnectivityTests
{
    private static StrongConnectivity Of(Graph graph) => Components.StrongConnectivityOf(BiAdjacentGraph.Of(graph));

    private static List<string> Bridges(StrongConnectivity result)
    {
        var bridges = result.StrongBridges;
        var list = new List<string>();
        for (int i = 0; i < bridges.Count; i++)
        {
            list.Add($"{bridges.SourceNodes[i]}->{bridges.TargetNodes[i]}");
        }
        return list;
    }

    private static int[] Points(StrongConnectivity result) => result.StrongArticulationPoints.ToArray();

    // ---- Definitions ----

    [Fact]
    public void CycleWithoutChordsHasOnlyStrongBridgesAndArticulationPoints()
    {
        var result = Of(TestGraphs.Cycle(5));
        Assert.Equal(["0->1", "1->2", "2->3", "3->4", "4->0"], Bridges(result));
        Assert.Equal([0, 1, 2, 3, 4], Points(result));
    }

    [Fact]
    public void TwoNodeCycleHasTwoStrongBridgesButNoArticulationPoint()
    {
        var result = Of(TestGraphs.Cycle(2));
        Assert.Equal(["0->1", "1->0"], Bridges(result));
        Assert.Empty(Points(result));
    }

    [Fact]
    public void RedundantPathsLeaveNothing()
    {
        var result = Of(TestGraphs.Clique(4));
        Assert.Empty(Bridges(result));
        Assert.Empty(Points(result));
    }

    [Fact]
    public void EdgeBetweenTwoTanglesIsNotAStrongBridge()
    {
        // Cycles {0, 1, 2} and {3, 4, 5}, joined by 2 -> 3.
        var g = TestGraphs.Of(6, 0, 1, 1, 2, 2, 0, 3, 4, 4, 5, 5, 3, 2, 3);
        var result = Of(g);
        Assert.Equal(["0->1", "1->2", "2->0", "3->4", "4->5", "5->3"], Bridges(result));
        Assert.Equal([0, 1, 2, 3, 4, 5], Points(result));
    }

    [Fact]
    public void HubOfTwoCyclesIsTheOnlyArticulationPoint()
    {
        // Cycles 0 -> 1 -> 0 and 0 -> 2 -> 0 share node 0.
        var result = Of(TestGraphs.Of(3, 0, 1, 1, 0, 0, 2, 2, 0));
        Assert.Equal([0], Points(result));
        Assert.Equal(["0->1", "0->2", "1->0", "2->0"], Bridges(result));
    }

    [Fact]
    public void ChordIsNoStrongBridge()
    {
        // 0 -> 1 -> 2 -> 0 plus the chord 0 -> 2, which the cycle bypasses; node 1 is not needed
        // for 0 and 2 to reach each other.
        var result = Of(TestGraphs.Of(3, 0, 1, 1, 2, 2, 0, 0, 2));
        Assert.Equal(["0->1", "1->2", "2->0"], Bridges(result));
        Assert.Equal([0, 2], Points(result));
    }

    [Fact]
    public void SelfLoopIsNeverAStrongBridge()
    {
        Assert.Empty(Bridges(Of(TestGraphs.Of(1, 0, 0))));
        Assert.Equal(["0->1", "1->2", "2->0"], Bridges(Of(TestGraphs.Of(3, 0, 1, 1, 2, 2, 0, 1, 1))));
    }

    [Fact]
    public void AcyclicAndEmptyGraphsHaveNothing()
    {
        Graph[] graphs =
        [
            TestGraphs.Empty(), TestGraphs.Isolated(3), TestGraphs.Path(5), TestGraphs.OutStar(4),
            RandomGraphs.RandomDag(40, 0.2, 7), RandomGraphs.RandomTree(40, 7),
        ];
        foreach (Graph g in graphs)
        {
            var result = Of(g);
            Assert.Empty(Bridges(result));
            Assert.Empty(Points(result));
        }
    }

    [Fact]
    public void EntryOfItsComponentIsFoundToo()
    {
        // Node 0, the lowest of its component and so the entry the computation starts from, is found
        // by the separate check for entries. Cycles 0 -> 1 -> 2 -> 0 and 0 -> 3 -> 4 -> 0 share
        // node 0; node 5 hangs off as a sink, and the edge 4 -> 5 between components changes nothing.
        var g = TestGraphs.Of(6, 0, 1, 1, 2, 2, 0, 0, 3, 3, 4, 4, 0, 4, 5);
        Assert.Equal([0, 1, 2, 3, 4], Points(Of(g)));
    }

    [Fact]
    public void EdgeIndicesNameTheReportedEdges()
    {
        // Not in the Java test class: the edge indices of the strong bridges, which Java checks in
        // EdgeIndicesInResultsTest, here on a small graph with bridges found in both directions.
        var g = TestGraphs.Of(6, 0, 1, 1, 2, 2, 0, 0, 3, 3, 4, 4, 0, 4, 5);
        var bridges = Of(g).StrongBridges;
        for (int p = 0; p < bridges.Count; p++)
        {
            Assert.Equal(g.EdgeIndexOf(bridges.SourceNodes[p], bridges.TargetNodes[p]), bridges.EdgeIndices[p]);
        }
    }

    // ---- Against removal and recount ----

    [Fact]
    public void MatchesRemovalOnRandomGraphs()
    {
        var random = new JavaRandom(31);
        for (int iteration = 0; iteration < 400; iteration++)
        {
            int n = 1 + random.NextInt(12);
            double p = 0.05 + (random.NextDouble() * 0.4);
            AssertMatchesRemoval(RandomGraphs.Gnp(n, p, random.NextLong()), $"gnp {iteration}");
        }
        for (long seed = 0; seed < 150; seed++)
        {
            LabeledGraph lg = RandomGraphs.AnyGraph(seed);
            if (lg.Graph.NodeCount <= 60)
            {
                AssertMatchesRemoval(lg.Graph, lg.Describe());
            }
        }
    }

    private static void AssertMatchesRemoval(Graph g, string description)
    {
        int n = g.NodeCount;
        var sccs = Components.SccsOf(g);
        int count = sccs.GroupCount;

        var expectedBridges = new List<string>();
        for (int e = 0; e < g.EdgeCount; e++)
        {
            if (Components.SccsOf(Without(g, e, -1)).GroupCount > count)
            {
                string key = $"{g.EdgeSource(e)}->{g.EdgeTarget(e)}";
                if (!expectedBridges.Contains(key))
                {
                    expectedBridges.Add(key);
                }
            }
        }
        var expectedPoints = new List<int>();
        for (int v = 0; v < n; v++)
        {
            // v stays as an isolated node, which adds one component of its own.
            int remaining = Components.SccsOf(Without(g, -1, v)).GroupCount - 1;
            int before = count - (sccs.SizeOf(sccs.Get(v)) == 1 ? 1 : 0);
            if (remaining > before)
            {
                expectedPoints.Add(v);
            }
        }

        var result = Of(g);
        Assert.True(expectedBridges.SequenceEqual(Bridges(result)),
            $"strong bridges of {description}: [{string.Join(", ", Bridges(result))}], expected [{string.Join(", ", expectedBridges)}]");
        Assert.True(expectedPoints.SequenceEqual(Points(result)),
            $"strong articulation points of {description}: [{string.Join(", ", Points(result))}], expected [{string.Join(", ", expectedPoints)}]");
    }

    /// <summary>The graph without edge index <paramref name="edge"/> and without the edges at node <paramref name="node"/>.</summary>
    private static Graph Without(Graph g, int edge, int node)
    {
        var edges = new List<int>();
        for (int e = 0; e < g.EdgeCount; e++)
        {
            int source = g.EdgeSource(e);
            int target = g.EdgeTarget(e);
            if (e == edge || source == node || target == node)
            {
                continue;
            }
            edges.Add(source);
            edges.Add(target);
        }
        return TestGraphs.Of(g.NodeCount, [.. edges]);
    }
}
