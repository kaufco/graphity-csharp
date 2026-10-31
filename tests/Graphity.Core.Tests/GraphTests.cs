namespace Graphity.Core.Tests;

public sealed class GraphTests
{
    [Fact]
    public void EmptyGraphReportsZero()
    {
        var g = TestGraphs.Empty();
        Assert.Equal(0, g.NodeCount);
        Assert.Equal(0, g.EdgeCount);
        Assert.True(g.IsEmpty);
    }

    [Fact]
    public void IsolatedGraphHasNodesNoEdges()
    {
        var g = TestGraphs.Isolated(5);
        Assert.Equal(5, g.NodeCount);
        Assert.Equal(0, g.EdgeCount);
        Assert.False(g.IsEmpty);
    }

    [Fact]
    public void NodeAndEdgeCountsMatchGraphBuilder()
    {
        // 0->1, 0->2, 1->3, 2->3 (diamond).
        var g = TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3);
        Assert.Equal(4, g.NodeCount);
        Assert.Equal(4, g.EdgeCount);
    }

    [Fact]
    public void OutDegreeReportsOutEdges()
    {
        var g = TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3);
        Assert.Equal(2, g.OutDegree(0));
        Assert.Equal(1, g.OutDegree(1));
        Assert.Equal(1, g.OutDegree(2));
        Assert.Equal(0, g.OutDegree(3));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void OutDegreeOutOfRangeThrows(int node)
    {
        var g = TestGraphs.Isolated(4);
        Assert.Throws<ArgumentOutOfRangeException>(() => g.OutDegree(node));
    }

    [Fact]
    public void MessagesWriteNegativeValuesAsJavaDoesInEveryCulture()
    {
        using var culture = new ForeignCulture();
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => TestGraphs.Isolated(4).OutDegree(-1));
        Assert.StartsWith("Index -1 out of bounds for length 4", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OutNodesReturnsSortedView()
    {
        var g = TestGraphs.Of(4, 0, 3, 0, 1, 0, 2);
        Assert.Equal([1, 2, 3], g.OutNodes(0).ToArray());
    }

    [Fact]
    public void OutNodesForNodeWithoutOutEdgesIsEmpty()
    {
        var g = TestGraphs.Of(2, 0, 1);
        Assert.True(g.OutNodes(1).IsEmpty);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void OutNodesOutOfRangeThrows(int node)
    {
        var g = TestGraphs.Isolated(4);
        Assert.Throws<ArgumentOutOfRangeException>(() => g.OutNodes(node));
    }

    [Fact]
    public void OutEdgeStartAndEndFrameOutNodes()
    {
        var g = TestGraphs.Of(3, 0, 1, 0, 2, 1, 2);
        int s = g.OutEdgeStart(0);
        int e = g.OutEdgeEnd(0);
        Assert.Equal(g.OutDegree(0), e - s);
        var targets = new int[e - s];
        for (int i = s; i < e; i++)
        {
            targets[i - s] = g.EdgeTarget(i);
        }
        Assert.Equal([1, 2], targets);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(100)]
    public void OutEdgeStartOutOfRangeThrows(int node)
    {
        var g = TestGraphs.Isolated(3);
        Assert.Throws<ArgumentOutOfRangeException>(() => g.OutEdgeStart(node));
        Assert.Throws<ArgumentOutOfRangeException>(() => g.OutEdgeEnd(node));
    }

    [Fact]
    public void EdgeTargetReturnsTargetByEdgeIndex()
    {
        var g = TestGraphs.Of(3, 0, 1, 0, 2, 1, 2);
        Assert.Equal(1, g.EdgeTarget(0));
        Assert.Equal(2, g.EdgeTarget(1));
        Assert.Equal(2, g.EdgeTarget(2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(100)]
    public void EdgeTargetOutOfRangeThrows(int edgeIndex)
    {
        var g = TestGraphs.Of(2, 0, 1, 1, 0, 0, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => g.EdgeTarget(edgeIndex));
    }

    [Fact]
    public void EdgeSourceInvertsEdgeTargetMapping()
    {
        var g = TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3);
        for (int e = 0; e < g.EdgeCount; e++)
        {
            int source = g.EdgeSource(e);
            int target = g.EdgeTarget(e);
            Assert.True(g.HasOutNode(source, target));
            Assert.InRange(e, g.OutEdgeStart(source), g.OutEdgeEnd(source) - 1);
        }
    }

    [Fact]
    public void EdgeSourceReturnsCorrectSourceAcrossAllEdges()
    {
        var g = TestGraphs.Of(5,
            0, 2, 0, 4,
            2, 3,
            3, 1, 3, 4);
        Assert.Equal(0, g.EdgeSource(0));
        Assert.Equal(0, g.EdgeSource(1));
        Assert.Equal(2, g.EdgeSource(2));
        Assert.Equal(3, g.EdgeSource(3));
        Assert.Equal(3, g.EdgeSource(4));
    }

    [Fact]
    public void EdgeSourceHandlesNodesWithNoOutEdges()
    {
        // Node 0 has no out-edges; edge 0 belongs to node 1.
        var g = TestGraphs.Of(3, 1, 2);
        Assert.Equal(1, g.EdgeSource(0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(100)]
    public void EdgeSourceOutOfRangeThrows(int edgeIndex)
    {
        var g = TestGraphs.Of(3, 0, 1, 0, 2, 1, 2, 2, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => g.EdgeSource(edgeIndex));
    }

    [Fact]
    public void HasOutNodeDetectsExistingEdges()
    {
        var g = TestGraphs.Of(3, 0, 1, 0, 2, 1, 2);
        Assert.True(g.HasOutNode(0, 1));
        Assert.True(g.HasOutNode(0, 2));
        Assert.True(g.HasOutNode(1, 2));
    }

    [Fact]
    public void EdgeIndexOfFindsEdgesAndReportsMissingOnes()
    {
        // Edge indices: 0 -> 1 is 0, 0 -> 2 is 1, 1 -> 2 is 2, 2 -> 0 is 3.
        var g = TestGraphs.Of(3, 0, 1, 0, 2, 1, 2, 2, 0);
        Assert.Equal(0, g.EdgeIndexOf(0, 1));
        Assert.Equal(1, g.EdgeIndexOf(0, 2));
        Assert.Equal(2, g.EdgeIndexOf(1, 2));
        Assert.Equal(3, g.EdgeIndexOf(2, 0));
        Assert.Equal(-1, g.EdgeIndexOf(1, 0));
        Assert.Equal(-1, g.EdgeIndexOf(0, 0));
        Assert.Equal(-1, g.EdgeIndexOf(0, 999));
        Assert.Equal(-1, g.EdgeIndexOf(0, -1));
        Assert.Equal(3, BiAdjacentGraph.Of(g).EdgeIndexOf(2, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void EdgeIndexOfRejectsSourceOutOfRange(int source)
    {
        var g = TestGraphs.Of(3, 0, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => g.EdgeIndexOf(source, 1));
    }

    [Fact]
    public void HasOutNodeRejectsMissingEdges()
    {
        var g = TestGraphs.Of(3, 0, 1, 0, 2, 1, 2);
        Assert.False(g.HasOutNode(0, 0));
        Assert.False(g.HasOutNode(1, 0));
        Assert.False(g.HasOutNode(2, 0));
        Assert.False(g.HasOutNode(0, 999));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(1000)]
    public void HasOutNodeOutOfRangeSourceThrows(int node)
    {
        var g = TestGraphs.Isolated(3);
        Assert.Throws<ArgumentOutOfRangeException>(() => g.HasOutNode(node, 0));
    }

    [Fact]
    public void ContainsSelfLoopDetectsSelfLoops()
    {
        var g = TestGraphs.Of(3, 0, 0, 1, 2, 2, 2);
        Assert.True(g.ContainsSelfLoop(0));
        Assert.False(g.ContainsSelfLoop(1));
        Assert.True(g.ContainsSelfLoop(2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(1000)]
    public void ContainsSelfLoopOutOfRangeThrows(int node)
    {
        var g = TestGraphs.Isolated(3);
        Assert.Throws<ArgumentOutOfRangeException>(() => g.ContainsSelfLoop(node));
    }

    [Fact]
    public void BiAdjacentGraphComputesCorrectInEdges()
    {
        // Diamond 0->1, 0->2, 1->3, 2->3. In-edges: 1<-{0}, 2<-{0}, 3<-{1,2}.
        var bi = BiAdjacentGraph.Of(TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3));
        Assert.Empty(bi.InNodes(0).ToArray());
        Assert.Equal([0], bi.InNodes(1).ToArray());
        Assert.Equal([0], bi.InNodes(2).ToArray());
        Assert.Equal([1, 2], bi.InNodes(3).ToArray());
    }

    [Fact]
    public void BiAdjacentGraphPreservesOutAdjacency()
    {
        var g = TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3);
        var bi = BiAdjacentGraph.Of(g);
        for (int i = 0; i < g.NodeCount; i++)
        {
            Assert.Equal(g.OutNodes(i).ToArray(), bi.OutNodes(i).ToArray());
        }
    }

    [Fact]
    public void BiAdjacentGraphOnEmptyGraphIsEmpty()
    {
        var bi = BiAdjacentGraph.Of(TestGraphs.Empty());
        Assert.Equal(0, bi.NodeCount);
        Assert.Equal(0, bi.EdgeCount);
    }

    [Fact]
    public void BiAdjacentGraphInAdjacencyIsSorted()
    {
        // Node 3 receives edges from 0, 2, 1 - must end up sorted as [0, 1, 2].
        var g = TestGraphs.Of(4, 0, 3, 1, 3, 2, 3);
        Assert.Equal([0, 1, 2], BiAdjacentGraph.Of(g).InNodes(3).ToArray());
    }

    [Fact]
    public void UniAdjacentGraphOnPlainGraphReturnsSelf()
    {
        var g = TestGraphs.Path(3);
        Assert.Same(g, g.UniAdjacentGraph());
    }

    [Fact]
    public void EqualsBasedOnStructure()
    {
        var a = TestGraphs.Of(3, 0, 1, 1, 2);
        var b = TestGraphs.Of(3, 0, 1, 1, 2);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void EqualsWithDifferentStructureFalse() =>
        Assert.NotEqual(TestGraphs.Of(3, 0, 1, 1, 2), TestGraphs.Of(3, 0, 2, 1, 2));

    [Fact]
    public void EqualsWithDifferentNodeCountFalse() =>
        Assert.NotEqual(TestGraphs.Isolated(3), TestGraphs.Isolated(4));

    [Fact]
    public void EqualsIgnoresBiVsUniAdjacentType()
    {
        var uni = TestGraphs.Of(3, 0, 1, 1, 2);
        var bi = BiAdjacentGraph.Of(uni);
        Assert.True(uni.Equals(bi));
        Assert.True(bi.Equals(uni));
        Assert.True(uni.Equals((object)bi));
        Assert.Equal(uni.GetHashCode(), bi.GetHashCode());
    }

    [Fact]
    public void EqualsWithNullOrOtherTypeFalse()
    {
        var g = TestGraphs.Path(3);
        Assert.False(g.Equals(null));
        Assert.False(g.Equals((object?)null));
        Assert.False(g.Equals("not a graph"));
    }

    [Fact]
    public void EqualsSameInstanceReturnsTrue()
    {
        var g = TestGraphs.Path(3);
        Assert.True(g.Equals(g));
    }

    [Fact]
    public void HashCodeMatchesTheJavaFormula()
    {
        // 31 * Arrays.hashCode(outAdjacencyStart) + Arrays.hashCode(outAdjacency), as in Java.
        var g = TestGraphs.Of(2, 0, 1);
        int startHash = 31 * ((31 * ((31 * 1) + 0)) + 1) + 1;
        int adjHash = (31 * 1) + 1;
        Assert.Equal((31 * startHash) + adjHash, g.GetHashCode());
    }

    [Fact]
    public void NodesEnumeratesEveryNodeInOrder()
    {
        Assert.Equal([0, 1, 2, 3], TestGraphs.Path(4).Nodes());
        Assert.Empty(TestGraphs.Empty().Nodes());
    }

    [Fact]
    public void ForEachEdgeVisitsEveryEdgeInEdgeIndexOrder()
    {
        for (long seed = 0; seed < 20; seed++)
        {
            Graph graph = RandomGraphs.Gnp(30, 0.1, seed);
            var indices = new List<int>();
            graph.ForEachEdge((source, target, edgeIndex) =>
            {
                Assert.Equal(source, graph.EdgeSource(edgeIndex));
                Assert.Equal(target, graph.EdgeTarget(edgeIndex));
                indices.Add(edgeIndex);
            });
            Assert.Equal(Enumerable.Range(0, graph.EdgeCount), indices);
        }
    }

    [Fact]
    public void ForEachEdgeOnABiAdjacentGraphAndItsTransposeVisitsTheirOwnEdges()
    {
        var bi = BiAdjacentGraph.Of(TestGraphs.Of(3, 0, 1, 0, 2, 2, 1));
        foreach (BiAdjacentGraph graph in new[] { bi, bi.Transposed() })
        {
            var seen = new List<int>();
            graph.ForEachEdge((source, target, edgeIndex) =>
            {
                Assert.Equal(edgeIndex, graph.EdgeIndexOf(source, target));
                seen.Add(edgeIndex);
            });
            Assert.Equal([0, 1, 2], seen);
        }
        var transposedSources = new List<int>();
        bi.Transposed().ForEachEdge((source, target, edgeIndex) => transposedSources.Add(source));
        // Transposed: 1 -> 0, 1 -> 2, 2 -> 0.
        Assert.Equal([1, 1, 2], transposedSources);
    }

    [Fact]
    public void ForEachEdgeOnAGraphWithoutEdgesCallsNothing()
    {
        int count = 0;
        TestGraphs.Isolated(3).ForEachEdge((source, target, edgeIndex) => count++);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ToStringListsSmallGraphsCompletely()
    {
        Assert.Equal("Graph[nodes=3, edges=3] {0 -> [1, 2], 1 -> [], 2 -> [0]}", TestGraphs.Of(3, 0, 1, 0, 2, 2, 0).ToString());
        Assert.Equal("Graph[nodes=0, edges=0] {}", TestGraphs.Empty().ToString());
    }

    [Fact]
    public void ToStringNamesTheBiAdjacentType() =>
        Assert.Equal("BiAdjacentGraph[nodes=2, edges=1] {0 -> [1], 1 -> []}", BiAdjacentGraph.Of(TestGraphs.Path(2)).ToString());

    [Fact]
    public void ToStringCutsAfterSixteenNodes()
    {
        string text = TestGraphs.Isolated(17).ToString();
        Assert.StartsWith("Graph[nodes=17, edges=0] {0 -> [], ", text, StringComparison.Ordinal);
        Assert.EndsWith("15 -> [], ...}", text, StringComparison.Ordinal);
        Assert.DoesNotContain("16 ->", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToStringListsExactlySixteenNodesWithoutAMarker()
    {
        string text = TestGraphs.Isolated(16).ToString();
        Assert.EndsWith("15 -> []}", text, StringComparison.Ordinal);
        Assert.DoesNotContain("...", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToStringCutInTheLastListHasNoTrailingMarker()
    {
        // Nodes 0..8 with 7 out-nodes each use 63 of the budget; node 9 is the last node.
        var b = new GraphBuilder();
        b.AddNodes(10);
        for (int v = 0; v < 9; v++)
        {
            for (int t = 0; t < 7; t++)
            {
                b.AddEdge(v, t);
            }
        }
        b.AddOutEdges(9, 0, 1);
        Assert.EndsWith("9 -> [0, ...]}", b.Build().ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ToStringIsBoundedOnAHub()
    {
        var b = new GraphBuilder();
        b.AddNodes(1_000_000);
        for (int v = 1; v < 1_000_000; v++)
        {
            b.AddEdge(0, v);
        }
        string text = b.Build().ToString();
        Assert.True(text.Length < 600);
        Assert.StartsWith("Graph[nodes=1000000, edges=999999] {0 -> [1, 2, ", text, StringComparison.Ordinal);
        Assert.EndsWith(", 64, ...], ...}", text, StringComparison.Ordinal);

        var high = new GraphBuilder();
        high.AddNodes(1_000_000);
        for (int v = 1; v <= 100; v++)
        {
            high.AddEdge(0, 1_000_000 - v);
        }
        string highText = high.Build().ToString();
        Assert.True(highText.Length < 1024);
        Assert.EndsWith(", ...], ...}", highText, StringComparison.Ordinal);
    }

    [Fact]
    public void ToStringStopsWhenTheTargetBudgetRunsOutBetweenLists()
    {
        // 64 out-nodes on node 0 exhaust the budget; node 1 still has edges.
        var b = new GraphBuilder();
        b.AddNodes(66);
        for (int v = 1; v <= 64; v++)
        {
            b.AddEdge(0, v);
        }
        b.AddEdge(1, 65);
        Assert.Contains("63, 64], 1 -> [...], ...}", b.Build().ToString(), StringComparison.Ordinal);
    }
}
