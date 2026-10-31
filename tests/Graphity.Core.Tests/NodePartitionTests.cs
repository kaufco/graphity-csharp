namespace Graphity.Core.Tests;

public sealed class NodePartitionTests
{
    /// <summary>Nodes 0..4 in three groups {0, 3} / {1, 4} / {2}.</summary>
    private static NodePartition ThreeGroupPartition(Graph graph) =>
        new(graph, [0, 1, 2, 0, 1], [0, 3, 1, 4, 2], [0, 2, 4, 5]);

    private static Graph EmptyGraph() => new GraphBuilder().Build();

    [Fact]
    public void GroupCountReportsNumberOfGroups() => Assert.Equal(3, ThreeGroupPartition(EmptyGraph()).GroupCount);

    [Fact]
    public void IndexCountReportsTotalNodes()
    {
        var p = ThreeGroupPartition(EmptyGraph());
        Assert.Equal(5, p.IndexCount);
        Assert.Equal(5, p.NodeCount);
    }

    [Fact]
    public void GetReturnsGroupIdForEachNode()
    {
        var p = ThreeGroupPartition(EmptyGraph());
        Assert.Equal(0, p.Get(0));
        Assert.Equal(1, p.Get(1));
        Assert.Equal(2, p.Get(2));
        Assert.Equal(0, p.Get(3));
        Assert.Equal(1, p.Get(4));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(100)]
    public void GetOutOfRangeThrows(int node)
    {
        var p = ThreeGroupPartition(EmptyGraph());
        Assert.Throws<ArgumentOutOfRangeException>(() => p.Get(node));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.IsMapped(node));
    }

    [Fact]
    public void IsPartialIsFalseForPartition() => Assert.False(ThreeGroupPartition(EmptyGraph()).IsPartial);

    [Fact]
    public void IndicesOfReturnsSortedMembers()
    {
        var p = ThreeGroupPartition(EmptyGraph());
        Assert.Equal([0, 3], p.IndicesOf(0).ToArray());
        Assert.Equal([1, 4], p.IndicesOf(1).ToArray());
        Assert.Equal([2], p.IndicesOf(2).ToArray());
    }

    [Fact]
    public void NodesOfDelegatesToIndicesOf()
    {
        var p = ThreeGroupPartition(EmptyGraph());
        Assert.Equal([0, 3], p.NodesOf(0).ToArray());
        Assert.Equal([1, 4], p.NodesOf(1).ToArray());
        Assert.Equal([2], p.NodesOf(2).ToArray());
    }

    [Fact]
    public void SizeOfReportsGroupSize()
    {
        var p = ThreeGroupPartition(EmptyGraph());
        Assert.Equal(2, p.SizeOf(0));
        Assert.Equal(2, p.SizeOf(1));
        Assert.Equal(1, p.SizeOf(2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(100)]
    public void IndicesOfOutOfRangeThrows(int groupId)
    {
        var p = ThreeGroupPartition(EmptyGraph());
        Assert.Throws<ArgumentOutOfRangeException>(() => p.IndicesOf(groupId));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(100)]
    public void SizeOfOutOfRangeThrows(int groupId)
    {
        var p = ThreeGroupPartition(EmptyGraph());
        Assert.Throws<ArgumentOutOfRangeException>(() => p.SizeOf(groupId));
    }

    [Fact]
    public void CondenseEmptyPartitionReturnsEmptyGraph()
    {
        var p = new NodePartition(EmptyGraph(), [], [], [0]);
        var condensed = p.Condense();
        Assert.Equal(0, condensed.NodeCount);
        Assert.Equal(0, condensed.EdgeCount);
    }

    [Fact]
    public void CondenseSingleGroupHasNoEdges()
    {
        // 0 -> 1, 1 -> 0, all nodes in one group.
        var builder = new GraphBuilder();
        builder.AddNodes(2);
        builder.AddEdge(0, 1);
        builder.AddEdge(1, 0);
        var p = new NodePartition(builder.Build(), [0, 0], [0, 1], [0, 2]);
        var condensed = p.Condense();
        Assert.Equal(1, condensed.NodeCount);
        Assert.Equal(0, condensed.EdgeCount);
    }

    [Fact]
    public void CondenseOmitsIntraGroupEdges()
    {
        // 0 -> 1 (intra), 0 -> 2 (inter), 1 -> 2 (inter); partition {0, 1} / {2}.
        var builder = new GraphBuilder();
        builder.AddNodes(3);
        builder.AddEdge(0, 1);
        builder.AddEdge(0, 2);
        builder.AddEdge(1, 2);
        var p = new NodePartition(builder.Build(), [0, 0, 1], [0, 1, 2], [0, 2, 3]);
        var condensed = p.Condense();
        Assert.Equal(2, condensed.NodeCount);
        Assert.Equal(1, condensed.EdgeCount);
        Assert.Equal([1], condensed.OutNodes(0).ToArray());
        Assert.True(condensed.OutNodes(1).IsEmpty);
    }

    [Fact]
    public void CondenseDeduplicatesParallelInterGroupEdges()
    {
        // 0 -> 2, 1 -> 2: both sources in group 0, the target in group 1; one condensed edge.
        var builder = new GraphBuilder();
        builder.AddNodes(3);
        builder.AddEdge(0, 2);
        builder.AddEdge(1, 2);
        var p = new NodePartition(builder.Build(), [0, 0, 1], [0, 1, 2], [0, 2, 3]);
        var condensed = p.Condense();
        Assert.Equal(1, condensed.EdgeCount);
        Assert.Equal([1], condensed.OutNodes(0).ToArray());
    }

    [Fact]
    public void CondenseProducesSortedAdjacencyLists()
    {
        // 0 -> 3 (g0 -> g2), 0 -> 2 (g0 -> g1), 1 -> 2 (g0 -> g1 again); partition {0,1} / {2} / {3}.
        var builder = new GraphBuilder();
        builder.AddNodes(4);
        builder.AddEdge(0, 3);
        builder.AddEdge(0, 2);
        builder.AddEdge(1, 2);
        var p = new NodePartition(builder.Build(), [0, 0, 1, 2], [0, 1, 2, 3], [0, 2, 3, 4]);
        Assert.Equal([1, 2], p.Condense().OutNodes(0).ToArray());
    }

    // --- Of(Graph, groupOf, groupCount) ---

    [Fact]
    public void OfGroupsNodesAsGiven()
    {
        // 0 -> 1 -> 2 -> 3, 3 -> 0; groups {0, 1} and {2, 3}.
        var graph = TestGraphs.Of(4, 0, 1, 1, 2, 2, 3, 3, 0);
        var p = NodePartition.Of(graph, [0, 0, 1, 1], 2);
        Assert.Equal(2, p.GroupCount);
        Assert.Equal(4, p.NodeCount);
        Assert.False(p.IsPartial);
        Assert.Equal([0, 1], p.NodesOf(0).ToArray());
        Assert.Equal([2, 3], p.NodesOf(1).ToArray());
        var condensed = p.Condense();
        Assert.Equal([1], condensed.OutNodes(0).ToArray());
        Assert.Equal([0], condensed.OutNodes(1).ToArray());
    }

    [Fact]
    public void OfLeavesUngroupedNodesOutOfTheCondensation()
    {
        // 0 -> 1 -> 2, 0 -> 2; node 1 in no group.
        var graph = TestGraphs.Of(3, 0, 1, 1, 2, 0, 2);
        var p = NodePartition.Of(graph, [0, -1, 1], 2);
        Assert.True(p.IsPartial);
        Assert.Equal(2, p.MappedCount);
        Assert.Equal(-1, p.Get(1));
        Assert.False(p.IsMapped(1));
        var condensed = p.Condense();
        Assert.Equal(1, condensed.EdgeCount);
        Assert.True(condensed.HasOutNode(0, 1));
        // Edge indices: 0 -> 1 is 0, 0 -> 2 is 1, 1 -> 2 is 2.
        var provenance = Graphs.EdgePartition(condensed, p);
        Assert.Equal([1], provenance.EdgesOf(0).ToArray());
        Assert.Equal(-1, provenance.Get(0));
        Assert.Equal(-1, provenance.Get(2));
    }

    [Fact]
    public void OfKeepsEmptyGroupsAsIsolatedNodes()
    {
        var graph = TestGraphs.Of(2, 0, 1);
        var p = NodePartition.Of(graph, [2, 0], 4);
        Assert.Equal(0, p.SizeOf(1));
        Assert.Equal(0, p.SizeOf(3));
        var condensed = p.Condense();
        Assert.Equal(4, condensed.NodeCount);
        Assert.Equal(1, condensed.EdgeCount);
        Assert.True(condensed.HasOutNode(2, 0));
    }

    [Fact]
    public void OfCopiesTheArray()
    {
        var graph = TestGraphs.Of(2, 0, 1);
        int[] groupOf = [0, 1];
        var p = NodePartition.Of(graph, groupOf, 2);
        groupOf[0] = 1;
        Assert.Equal(0, p.Get(0));
    }

    [Fact]
    public void OfOnAnEmptyGraph()
    {
        var p = NodePartition.Of(EmptyGraph(), [], 0);
        Assert.Equal(0, p.GroupCount);
        Assert.Equal(0, p.Condense().NodeCount);
        var withGroups = NodePartition.Of(EmptyGraph(), [], 2);
        Assert.Equal(2, withGroups.Condense().NodeCount);
    }

    [Fact]
    public void OfRejectsInvalidInput()
    {
        var graph = TestGraphs.Of(2, 0, 1);
        Assert.Throws<ArgumentException>(() => NodePartition.Of(graph, [0], 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NodePartition.Of(graph, [0, 1], 1));
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => NodePartition.Of(graph, [0, -2], 1));
        Assert.StartsWith("group of node 1 is -2, outside -1..0 (Parameter 'groupOf')", e.Message,
            StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => NodePartition.Of(graph, [int.MinValue, 0], 1));
        Assert.Throws<ArgumentException>(() => NodePartition.Of(graph, [-1, -1], -1));
        Assert.Throws<ArgumentException>(() => NodePartition.Of(graph, [-1, -1], int.MaxValue));
    }

    [Fact]
    public void OfRejectsMoreGroupsThanAGraphCanHaveNodes()
    {
        // The offsets would exceed the largest array; rejected before anything is allocated.
        var graph = TestGraphs.Of(2, 0, 1);
        var e = Assert.Throws<ArgumentException>(() => NodePartition.Of(graph, [-1, -1], ArrayLimits.MaxNodeCount + 1));
        Assert.Contains("groupCount", e.Message);
    }

    // --- Reduce ---

    [Fact]
    public void ReduceGivesEmptyGroupsTheIdentityAndKeepsTheirObjectEntry()
    {
        var graph = TestGraphs.Of(4);
        var p = NodePartition.Of(graph, [1, -1, 1, 0], 3);
        // group 0 = {3}, group 1 = {0, 2}, group 2 empty; node 1 in no group
        Assert.Equal([7, 3, 0], p.Reduce<int>([1, 100, 2, 7], 0, (a, b) => a + b));
        Assert.Equal([7, 2, -1], p.Reduce<int>([1, 100, 2, 7], -1, Math.Max));
        Assert.Equal([7L, 3L, 0L], p.Reduce<long>([1, 100, 2, 7], 0L, (a, b) => a + b));
        Assert.Equal([7.5, 3.5, 0.5], p.Reduce<double>([1, 100, 2, 7], 0.5, (a, b) => a + b));
        Assert.Throws<ArgumentException>(() => p.Reduce<int>(new int[3], 0, (a, b) => a + b));
        Assert.Throws<ArgumentException>(() => p.Reduce<string?>(new string?[4], new string?[2], string.Concat));
        string?[] groupValues = [null, null, "untouched"];
        p.Reduce<string?>(["a", "x", "b", "c"], groupValues, string.Concat);
        Assert.Equal(new string?[] { "c", "ab", "untouched" }, groupValues);
    }

    [Fact]
    public void ReduceRejectsTheSameArrayForValuesAndGroupValues()
    {
        var p = NodePartition.Of(TestGraphs.Of(2), [1, 0], 2);
        string[] values = ["a", "b"];
        Assert.Throws<ArgumentException>(() => p.Reduce<string>(values, values, string.Concat));
        Assert.Equal(["a", "b"], values);
    }

    [Fact]
    public void ReduceFoldsInAscendingMemberOrder()
    {
        var p = NodePartition.Of(TestGraphs.Of(3), [0, 0, 0], 1);
        Assert.Equal([123], p.Reduce<int>([1, 2, 3], 0, (acc, v) => (acc * 10) + v));
    }
}
