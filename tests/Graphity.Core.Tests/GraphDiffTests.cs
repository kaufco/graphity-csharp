namespace Graphity.Core.Tests;

public sealed class GraphDiffTests
{
    private static HashSet<long> EdgeSet(Graph graph)
    {
        var set = new HashSet<long>();
        graph.ForEachEdge((source, target, edgeIndex) => set.Add(((long)source << 32) | (uint)target));
        return set;
    }

    private static List<long> Keys(Edges edges)
    {
        var list = new List<long>();
        for (int p = 0; p < edges.Count; p++)
        {
            list.Add(((long)edges.SourceNodes[p] << 32) | (uint)edges.TargetNodes[p]);
        }
        return list;
    }

    /// <summary><paramref name="graph"/> with some edges dropped and some added, over the same nodes.</summary>
    private static Graph Edited(Graph graph, long seed)
    {
        var random = new JavaRandom(seed);
        var b = new GraphBuilder();
        b.AddNodes(graph.NodeCount);
        graph.ForEachEdge((source, target, edgeIndex) =>
        {
            if (random.NextInt(5) != 0)
            {
                b.AddEdge(source, target);
            }
        });
        int n = graph.NodeCount;
        for (int i = 0; i < 1 + random.NextInt(10); i++)
        {
            b.AddEdge(random.NextInt(n), random.NextInt(n));
        }
        return b.Build();
    }

    [Fact]
    public void DiffMatchesTheSetDifferenceAndNamesEachEdgeInItsGraph()
    {
        for (long seed = 0; seed < 200; seed++)
        {
            Graph before = RandomGraphs.AnyGraph(seed).Graph;
            Graph after = Edited(before, seed);
            var diff = Graphs.Diff(before, after);

            var expectedAdded = EdgeSet(after);
            expectedAdded.ExceptWith(EdgeSet(before));
            var expectedRemoved = EdgeSet(before);
            expectedRemoved.ExceptWith(EdgeSet(after));

            Assert.True(expectedAdded.Order().SequenceEqual(Keys(diff.AddedEdges)), $"added, seed {seed}");
            Assert.True(expectedRemoved.Order().SequenceEqual(Keys(diff.RemovedEdges)), $"removed, seed {seed}");
            AssertIndices(after, diff.AddedEdges);
            AssertIndices(before, diff.RemovedEdges);
            Assert.Equal(before.Equals(after), diff.IsEmpty);
        }
    }

    private static void AssertIndices(Graph graph, Edges edges)
    {
        for (int p = 0; p < edges.Count; p++)
        {
            Assert.Equal(graph.EdgeIndexOf(edges.SourceNodes[p], edges.TargetNodes[p]), edges.EdgeIndices[p]);
        }
    }

    [Fact]
    public void EqualGraphsHaveAnEmptyDiff()
    {
        Graph graph = RandomGraphs.Gnp(40, 0.1, 3);
        var diff = Graphs.Diff(graph, BiAdjacentGraph.Of(graph));
        Assert.True(diff.IsEmpty);
        Assert.Equal(0, diff.AddedEdges.Count);
        Assert.Equal(0, diff.RemovedEdges.Count);
        Assert.Equal("GraphDiff[added=0, removed=0]", diff.ToString());
    }

    [Fact]
    public void DiffOfASmallChange()
    {
        Graph before = TestGraphs.Of(3, 0, 1, 1, 2);
        Graph after = TestGraphs.Of(3, 0, 1, 0, 2);
        var diff = Graphs.Diff(before, after);
        Assert.Equal([0], diff.AddedEdges.SourceNodes.ToArray());
        Assert.Equal([2], diff.AddedEdges.TargetNodes.ToArray());
        Assert.Equal([1], diff.AddedEdges.EdgeIndices.ToArray());
        Assert.Equal([1], diff.RemovedEdges.SourceNodes.ToArray());
        Assert.Equal([2], diff.RemovedEdges.TargetNodes.ToArray());
        Assert.Equal([1], diff.RemovedEdges.EdgeIndices.ToArray());
        Assert.Equal("GraphDiff[added=1, removed=1]", diff.ToString());
    }

    [Fact]
    public void DiffListsSourcesTargetsAndIndicesInStepOverSeveralNodes()
    {
        Graph before = TestGraphs.Of(4, 0, 1, 0, 3, 1, 2, 2, 0);
        Graph after = TestGraphs.Of(4, 0, 2, 0, 3, 1, 0, 1, 2, 2, 1);
        var diff = Graphs.Diff(before, after);
        Assert.Equal([0, 1, 2], diff.AddedEdges.SourceNodes.ToArray());
        Assert.Equal([2, 0, 1], diff.AddedEdges.TargetNodes.ToArray());
        Assert.Equal([0, 2, 4], diff.AddedEdges.EdgeIndices.ToArray());
        Assert.Equal([0, 2], diff.RemovedEdges.SourceNodes.ToArray());
        Assert.Equal([1, 0], diff.RemovedEdges.TargetNodes.ToArray());
        Assert.Equal([0, 3], diff.RemovedEdges.EdgeIndices.ToArray());
    }

    [Fact]
    public void DiffAgainstAGraphWithoutEdgesListsEveryEdge()
    {
        Graph graph = RandomGraphs.Gnp(30, 0.1, 9);
        Graph empty = TestGraphs.Isolated(30);
        int[] all = Enumerable.Range(0, graph.EdgeCount).ToArray();
        Assert.Equal(all, Graphs.Diff(empty, graph).AddedEdges.EdgeIndices.ToArray());
        Assert.True(Graphs.Diff(empty, graph).RemovedEdges.IsEmpty);
        Assert.Equal(all, Graphs.Diff(graph, empty).RemovedEdges.EdgeIndices.ToArray());
        Assert.True(Graphs.Diff(TestGraphs.Empty(), TestGraphs.Empty()).IsEmpty);
    }

    [Fact]
    public void ATransposedViewEqualsTheBuiltTranspose()
    {
        Graph graph = RandomGraphs.Gnp(25, 0.15, 4);
        var view = BiAdjacentGraph.Of(graph).Transposed();
        var b = new GraphBuilder();
        b.AddNodes(graph.NodeCount);
        graph.ForEachEdge((source, target, edgeIndex) => b.AddEdge(target, source));
        Graph built = b.Build();
        Assert.True(Graphs.Diff(view, built).IsEmpty);
        Assert.Equal(Graphs.TopologyHash(built), Graphs.TopologyHash(view));
    }

    [Fact]
    public void DiffRejectsDifferentNodeCounts()
    {
        var e = Assert.Throws<ArgumentException>(() => Graphs.Diff(TestGraphs.Path(3), TestGraphs.Path(4)));
        Assert.Contains("same nodes", e.Message);
    }

    [Fact]
    public void TopologyHashIsConsistentWithEquals()
    {
        for (long seed = 0; seed < 50; seed++)
        {
            Graph graph = RandomGraphs.AnyGraph(seed).Graph;
            Graph copy = Graph.Of(Csr.OutAdjacencyStart(graph), Csr.OutAdjacency(graph));
            Assert.Equal(Graphs.TopologyHash(graph), Graphs.TopologyHash(copy));
            Assert.Equal(Graphs.TopologyHash(graph), Graphs.TopologyHash(BiAdjacentGraph.Of(graph)));
        }
    }

    [Fact]
    public void TopologyHashSeparatesDistinctGraphs()
    {
        var hashes = new HashSet<long>();
        var graphs = new HashSet<Graph>();
        for (long seed = 0; seed < 2000; seed++)
        {
            Graph graph = RandomGraphs.Gnp(1 + (int)(seed % 12), 0.3, seed);
            if (graphs.Add(graph))
            {
                Assert.True(hashes.Add(Graphs.TopologyHash(graph)), $"collision at seed {seed}");
            }
        }
        Assert.True(graphs.Count > 1000, $"distinct graphs: {graphs.Count}");
    }

    [Fact]
    public void TopologyHashTellsApartGraphsThatDifferOnlyInWhereAnEdgeStarts()
    {
        // Both have targets [0, 1] and two nodes and edges; degrees 2/0 against 1/1.
        Assert.NotEqual(Graphs.TopologyHash(TestGraphs.Of(2, 0, 0, 1, 1)), Graphs.TopologyHash(TestGraphs.Of(2, 0, 0, 0, 1)));
        Assert.NotEqual(Graphs.TopologyHash(TestGraphs.Isolated(4)), Graphs.TopologyHash(TestGraphs.Isolated(3)));
    }

    [Fact]
    public void TopologyHashKeepsItsValuesAcrossReleasesAndImplementations()
    {
        // The values of the Java implementation, fixed: only a release that announces a new hash in its
        // changelog may change them.
        long[] hashes =
        [
            Graphs.TopologyHash(TestGraphs.Empty()),
            Graphs.TopologyHash(TestGraphs.Isolated(1)),
            Graphs.TopologyHash(TestGraphs.Of(3, 0, 1, 0, 2, 2, 0)),
            Graphs.TopologyHash(TestGraphs.Of(4, 0, 1, 1, 2, 2, 3, 3, 0, 3, 3)),
        ];
        Assert.Equal([-5442815153047339753L, 481960320753566714L, -2177113494002845099L, -7127685637304178848L], hashes);
    }
}
