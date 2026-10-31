using System.Runtime.InteropServices;

namespace Graphity.Core.Tests;

/// <summary>
/// Java's batch methods come in an <c>int...</c> and an <c>IntList</c> overload; the C# port has one
/// <c>params ReadOnlySpan&lt;int&gt;</c> signature. The "parameter forms" tests therefore check loose
/// arguments, an array, a span over a <see cref="List{T}"/> and a <see cref="SortedIntView"/>.
/// </summary>
public sealed class GraphBuilderTests
{
    /// <summary>The node limit: Array.MaxLength - 1 (Java: Integer.MAX_VALUE - 9).</summary>
    private const int NodeLimit = ArrayLimits.MaxArrayLength - 1;

    /// <summary>The edge limit: Array.MaxLength (Java: Integer.MAX_VALUE - 8).</summary>
    private const int EdgeLimit = ArrayLimits.MaxArrayLength;

    private static ReadOnlySpan<int> SpanOf(List<int> list) => CollectionsMarshal.AsSpan(list);

    [Fact]
    public void NewBuilderBuildsEmptyGraph()
    {
        var graph = new GraphBuilder().Build();
        Assert.Equal(0, graph.NodeCount);
        Assert.Equal(0, graph.EdgeCount);
        Assert.True(graph.IsEmpty);
    }

    [Fact]
    public void AddNodeReturnsSequentialIds()
    {
        var b = new GraphBuilder();
        Assert.Equal(0, b.AddNode());
        Assert.Equal(1, b.AddNode());
        Assert.Equal(2, b.AddNode());
        Assert.Equal(3, b.Build().NodeCount);
    }

    [Fact]
    public void AddNodesReturnsFirstAddedId()
    {
        var b = new GraphBuilder();
        b.AddNode();
        Assert.Equal(1, b.AddNodes(3));
        Assert.Equal(4, b.Build().NodeCount);
    }

    [Fact]
    public void AddNodesWithZeroCountIsNoOp()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        Assert.Equal(2, b.AddNodes(0));
        Assert.Equal(2, b.Build().NodeCount);
    }

    [Fact]
    public void AddNodesWithNegativeCountThrows() =>
        Assert.Throws<ArgumentException>(() => new GraphBuilder().AddNodes(-1));

    [Fact]
    public void AddEdgeWithInvalidSourceThrows()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdge(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdge(-1, 0));
    }

    [Fact]
    public void AddEdgeWithInvalidTargetThrows()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdge(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdge(0, -1));
    }

    [Fact]
    public void AddOutEdgesWithInvalidSourceThrows()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdges(5, [0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdges(-1, [0]));
    }

    [Fact]
    public void AddOutEdgesWithInvalidTargetThrows()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdges(0, [0, 99]));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdges(0, [0, -1]));
    }

    [Fact]
    public void BuildProducesSortedAdjacencyList()
    {
        var b = new GraphBuilder();
        b.AddNodes(4);
        b.AddEdge(0, 3);
        b.AddEdge(0, 1);
        b.AddEdge(0, 2);
        Assert.Equal([1, 2, 3], b.Build().OutNodes(0).ToArray());
    }

    [Fact]
    public void BuildDeduplicatesParallelEdges()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddEdge(0, 1);
        b.AddEdge(0, 1);
        b.AddEdge(0, 1);
        var graph = b.Build();
        Assert.Equal(1, graph.EdgeCount);
        Assert.Equal([1], graph.OutNodes(0).ToArray());
    }

    [Fact]
    public void BuildAllowsSelfLoops()
    {
        var b = new GraphBuilder();
        b.AddNodes(1);
        b.AddEdge(0, 0);
        var graph = b.Build();
        Assert.Equal(1, graph.EdgeCount);
        Assert.Equal([0], graph.OutNodes(0).ToArray());
        Assert.True(graph.ContainsSelfLoop(0));
    }

    [Fact]
    public void BuildTrimsAdjacencyArrayAfterDedup()
    {
        var b = new GraphBuilder();
        b.AddNodes(3);
        for (int i = 0; i < 100; i++)
        {
            b.AddEdge(0, 1);
        }
        b.AddEdge(0, 2);
        var graph = b.Build();
        Assert.Equal(2, graph.EdgeCount);
        Assert.Equal(2, Csr.OutAdjacency(graph).Length);
        Assert.Equal([1, 2], graph.OutNodes(0).ToArray());
    }

    [Fact]
    public void BuildWithIsolatedNodesProducesNoEdges()
    {
        var builder = new GraphBuilder();
        builder.AddNodes(5);
        var built = builder.Build();
        Assert.Equal(5, built.NodeCount);
        Assert.Equal(0, built.EdgeCount);
        for (int i = 0; i < 5; i++)
        {
            Assert.True(built.OutNodes(i).IsEmpty);
        }
    }

    [Fact]
    public void BuilderIsNotConsumedByBuild()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddEdge(0, 1);
        var first = b.Build();
        b.AddNode();
        b.AddEdge(1, 2);
        var second = b.Build();
        Assert.Equal(2, first.NodeCount);
        Assert.Equal(1, first.EdgeCount);
        Assert.Equal(3, second.NodeCount);
        Assert.Equal(2, second.EdgeCount);
    }

    [Fact]
    public void MultipleBuildsAreIndependent()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddEdge(0, 1);
        var first = b.Build();
        var second = b.Build();
        Assert.NotSame(first, second);
        Assert.Equal(first, second);
    }

    [Fact]
    public void AddOutEdgesAppendsAllTargets()
    {
        var b = new GraphBuilder();
        b.AddNodes(4);
        b.AddOutEdges(0, SpanOf([3, 1, 2]));
        Assert.Equal([1, 2, 3], b.Build().OutNodes(0).ToArray());
    }

    [Fact]
    public void AddOutEdgesAcceptsSortedIntView()
    {
        var source = TestGraphs.Path(4);
        var b = new GraphBuilder();
        b.AddNodes(4);
        for (int n = 0; n < source.NodeCount; n++)
        {
            b.AddOutEdges(n, source.OutNodes(n));
        }
        Assert.Equal(source, b.Build());
    }

    [Fact]
    public void AddGraphSingleImportPreservesFullTopology()
    {
        // Diamond with a self-loop: 0->0, 0->1, 0->2, 1->3, 2->3.
        var original = TestGraphs.Of(4, 0, 0, 0, 1, 0, 2, 1, 3, 2, 3);
        var b = new GraphBuilder();
        Assert.Equal(0, b.AddGraph(original));
        Assert.Equal(original, b.Build());
    }

    [Fact]
    public void AddGraphTwoImportsPreserveTopologyAtOffset()
    {
        var g1 = TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3);
        var g2 = TestGraphs.Of(3, 0, 1, 0, 2, 1, 2);
        var b = new GraphBuilder();
        Assert.Equal(0, b.AddGraph(g1));
        Assert.Equal(4, b.AddGraph(g2));
        var expected = TestGraphs.Of(7,
            0, 1, 0, 2, 1, 3, 2, 3,
            4, 5, 4, 6, 5, 6);
        Assert.Equal(expected, b.Build());
    }

    [Fact]
    public void AddGraphAllowsBridgingEdgesBetweenImports()
    {
        var g1 = TestGraphs.Of(3, 0, 1, 1, 2);
        var g2 = TestGraphs.Of(3, 0, 1, 1, 2);
        var b = new GraphBuilder();
        int off1 = b.AddGraph(g1);
        int off2 = b.AddGraph(g2);
        b.AddEdge(off1 + 2, off2 + 0);
        b.AddEdge(off1 + 0, off2 + 2);
        var expected = TestGraphs.Of(6,
            0, 1, 1, 2, 2, 3,
            3, 4, 4, 5,
            0, 5);
        Assert.Equal(expected, b.Build());
    }

    [Fact]
    public void AddGraphWorksAfterManualEdges()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddEdge(0, 1);
        Assert.Equal(2, b.AddGraph(TestGraphs.Of(3, 0, 1, 1, 2)));
        Assert.Equal(TestGraphs.Of(5, 0, 1, 2, 3, 3, 4), b.Build());
    }

    [Fact]
    public void AddGraphWorksBeforeManualEdges()
    {
        var b = new GraphBuilder();
        Assert.Equal(0, b.AddGraph(TestGraphs.Of(3, 0, 1, 1, 2)));
        b.AddNode();
        b.AddEdge(0, 3);
        b.AddEdge(2, 3);
        Assert.Equal(TestGraphs.Of(4, 0, 1, 0, 3, 1, 2, 2, 3), b.Build());
    }

    [Fact]
    public void AddGraphDeduplicatesAgainstExistingEdges()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddEdge(0, 1);
        b.AddGraph(TestGraphs.Of(2, 0, 1));
        Assert.Equal(TestGraphs.Of(4, 0, 1, 2, 3), b.Build());
    }

    [Fact]
    public void AddGraphOnEmptyGraphIsNoOp()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddEdge(0, 1);
        Assert.Equal(2, b.AddGraph(TestGraphs.Empty()));
        Assert.Equal(TestGraphs.Of(2, 0, 1), b.Build());
    }

    // ---- validation ----

    [Fact]
    public void RejectsUnknownNodes()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdge(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdge(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdge(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdges(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddInEdges(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddPath(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdgePairs(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddChild(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddChildren(5, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddParent(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddParents(5, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdges(0, SpanOf([5])));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdges(5, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddInEdges(5, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddInEdges(5, SpanOf([0])));
        // No rejected call created a node or an edge.
        Assert.Equal(TestGraphs.Isolated(2), b.Build());
    }

    [Fact]
    public void RejectsNegativeIdsInEveryBatchForm()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdges(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdges(0, SpanOf([-1])));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddInEdges(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddPath(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdgePairs(0, -1));
    }

    [Fact]
    public void LeavesTheBuilderUnchangedWhenABatchIsRejected()
    {
        var b = new GraphBuilder();
        b.AddNodes(3);
        b.AddEdge(0, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdges(0, 2, 99));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddPath(0, 2, 99));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdgePairs(0, 2, 1, 99));
        Assert.Equal(TestGraphs.Of(3, 0, 1), b.Build());
    }

    [Fact]
    public void RejectionMessageNamesNodeAndCount()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdge(0, 7));
        Assert.Contains("7", ex.Message, StringComparison.Ordinal);
        Assert.Contains("nodeCount=2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsValidReferences()
    {
        var b = new GraphBuilder();
        b.AddNodes(3);
        b.AddEdge(0, 1);
        b.AddPath(1, 2);
        Assert.Equal(TestGraphs.Of(3, 0, 1, 1, 2), b.Build());
    }

    [Fact]
    public void RejectsUnknownNodeAfterImportingAGraph()
    {
        var b = new GraphBuilder();
        b.AddGraph(TestGraphs.Isolated(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdge(0, 3));
    }

    [Fact]
    public void AndNodesMethodsStillCreateNodes()
    {
        var b = new GraphBuilder();
        b.AddEdgeAndNodes(2, 0);
        Assert.Equal(TestGraphs.Of(3, 2, 0), b.Build());
    }

    // ---- EnsureEdgeCapacity ----

    [Fact]
    public void EnsureEdgeCapacityDoesNotChangeTheResult()
    {
        var hinted = new GraphBuilder();
        hinted.EnsureEdgeCapacity(3);
        hinted.AddNodes(3);
        hinted.AddPath(0, 1, 2);
        Assert.Equal(TestGraphs.Of(3, 0, 1, 1, 2), hinted.Build());
    }

    [Fact]
    public void EnsureEdgeCapacityMidConstructionPreservesEdges()
    {
        var b = new GraphBuilder();
        b.AddNodes(3);
        b.AddEdge(0, 1);
        b.EnsureEdgeCapacity(1000);
        b.AddEdge(1, 2);
        Assert.Equal(TestGraphs.Of(3, 0, 1, 1, 2), b.Build());
    }

    [Fact]
    public void EnsureEdgeCapacityBelowCurrentSizeIsHarmless()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddEdge(0, 1);
        b.EnsureEdgeCapacity(0);
        Assert.Equal(TestGraphs.Of(2, 0, 1), b.Build());
    }

    [Fact]
    public void EnsureEdgeCapacityPreventsReallocation()
    {
        var b = new GraphBuilder();
        b.EnsureEdgeCapacity(1000);
        b.AddNodes(2);
        int reserved = b.EdgeCapacity;
        Assert.True(reserved >= 1000);
        for (int i = 0; i < 1000; i++)
        {
            b.AddEdge(0, 1);
        }
        Assert.Equal(reserved, b.EdgeCapacity);
    }

    [Fact]
    public void WithoutAHintTheEdgeStorageGrows()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddEdge(0, 1);
        int initial = b.EdgeCapacity;
        for (int i = 0; i < 1000; i++)
        {
            b.AddEdge(0, 1);
        }
        Assert.True(b.EdgeCapacity > initial);
    }

    [Fact]
    public void EnsureEdgeCapacityRejectsNegative() =>
        Assert.Throws<ArgumentException>(() => new GraphBuilder().EnsureEdgeCapacity(-1));

    [Fact]
    public void EnsureEdgeCapacityRejectsMoreThanTheEdgeLimit()
    {
        var b = new GraphBuilder();
        var ex = Assert.Throws<ArgumentException>(() => b.EnsureEdgeCapacity(int.MaxValue));
        Assert.Contains("edge limit", ex.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => b.EnsureEdgeCapacity(EdgeLimit + 1));
        Assert.Equal(0, b.EdgeCapacity);
    }

    [Fact]
    public void EnsureEdgeCapacityReservesExactly()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddOutEdges(0, new int[100]);
        b.EnsureEdgeCapacity(101);
        Assert.Equal(101, b.EdgeCapacity);
    }

    // ---- growth of the edge storage under batch calls ----

    [Fact]
    public void BatchCallsInALoopGrowTheEdgeStorageGeometrically()
    {
        // 10 000 calls of two edges each: growing by half takes about 20 capacity changes, growing
        // to the exact size one per call.
        int[] targets = [1, 1];
        var path = TestGraphs.Path(3);
        Assert.True(CapacityChanges(b => b.AddOutEdges(0, 1, 1)) <= 30);
        Assert.True(CapacityChanges(b => b.AddOutEdges(0, targets)) <= 30);
        Assert.True(CapacityChanges(b => b.AddInEdges(1, targets)) <= 30);
        Assert.True(CapacityChanges(b => b.AddPath(0, 1, 0)) <= 30);
        Assert.True(CapacityChanges(b => b.AddEdgePairs(0, 1, 1, 0)) <= 30);
        Assert.True(CapacityChanges(b => b.AddChildren(0, 2)) <= 30);
        Assert.True(CapacityChanges(b => b.AddParents(0, 2)) <= 30);
        Assert.True(CapacityChanges(b => b.AddGraph(path)) <= 30);
    }

    [Fact]
    public void ABatchBeyondTheGrowthStepAllocatesExactlyOnce()
    {
        var imported = new GraphBuilder();
        imported.AddGraph(TestGraphs.Path(1001));
        Assert.Equal(1000, imported.EdgeCapacity);

        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddEdge(0, 1);
        b.AddOutEdges(0, new int[1000]);
        Assert.Equal(1001, b.EdgeCapacity);
    }

    /// <summary>Counts how often 10 000 applications of <paramref name="call"/> change the edge capacity.</summary>
    private static int CapacityChanges(Action<GraphBuilder> call)
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        int capacity = b.EdgeCapacity;
        int changes = 0;
        for (int i = 0; i < 10_000; i++)
        {
            call(b);
            if (b.EdgeCapacity != capacity)
            {
                capacity = b.EdgeCapacity;
                changes++;
            }
        }
        return changes;
    }

    // ---- AddChild / AddChildren / AddParent / AddParents ----

    [Fact]
    public void AddChildReturnsNewIdAndLinksIt()
    {
        var b = new GraphBuilder();
        int root = b.AddNode();
        int child = b.AddChild(root);
        Assert.Equal(1, child);
        Assert.Equal(2, b.AddChild(child));
        Assert.Equal(TestGraphs.Of(3, 0, 1, 1, 2), b.Build());
    }

    [Fact]
    public void AddChildrenReturnsFirstIdAndLinksAll()
    {
        var b = new GraphBuilder();
        int root = b.AddNode();
        Assert.Equal(1, b.AddChildren(root, 3));
        Assert.Equal(TestGraphs.Of(4, 0, 1, 0, 2, 0, 3), b.Build());
    }

    [Fact]
    public void AddChildrenWithZeroCountAddsNothing()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        Assert.Equal(2, b.AddChildren(0, 0));
        Assert.Equal(TestGraphs.Isolated(2), b.Build());
    }

    [Fact]
    public void AddChildrenWithNegativeCountThrows()
    {
        var b = new GraphBuilder();
        b.AddNode();
        Assert.Throws<ArgumentException>(() => b.AddChildren(0, -1));
    }

    [Fact]
    public void AddParentReturnsNewIdAndLinksIt()
    {
        var b = new GraphBuilder();
        int leaf = b.AddNode();
        Assert.Equal(1, b.AddParent(leaf));
        Assert.Equal(TestGraphs.Of(2, 1, 0), b.Build());
    }

    [Fact]
    public void AddParentsReturnsFirstIdAndLinksAll()
    {
        var b = new GraphBuilder();
        int join = b.AddNode();
        Assert.Equal(1, b.AddParents(join, 2));
        Assert.Equal(TestGraphs.Of(3, 1, 0, 2, 0), b.Build());
    }

    [Fact]
    public void AddParentsWithZeroCountAddsNothing()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        Assert.Equal(2, b.AddParents(0, 0));
        Assert.Equal(TestGraphs.Isolated(2), b.Build());
    }

    [Fact]
    public void AddParentsWithNegativeCountThrows()
    {
        var b = new GraphBuilder();
        b.AddNode();
        Assert.Throws<ArgumentException>(() => b.AddParents(0, -1));
    }

    // ---- AddPath ----

    [Fact]
    public void AddPathChainsExistingNodes()
    {
        var b = new GraphBuilder();
        b.AddNodes(4);
        b.AddPath(3, 1, 0);
        Assert.Equal(TestGraphs.Of(4, 3, 1, 1, 0), b.Build());
    }

    [Fact]
    public void AddPathWithFewerThanTwoNodesAddsNoEdge()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddPath();
        b.AddPath(1);
        Assert.Equal(TestGraphs.Isolated(2), b.Build());
    }

    [Fact]
    public void AddPathAllowsRepeatedNodes()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddPath(0, 1, 0);
        Assert.Equal(TestGraphs.Of(2, 0, 1, 1, 0), b.Build());
    }

    // ---- AddOutEdges and AddInEdges ----

    [Fact]
    public void AddOutEdgesArrayMatchesSpanOfAList()
    {
        var viaArray = new GraphBuilder();
        viaArray.AddNodes(4);
        viaArray.AddOutEdges(0, new[] { 3, 1, 2 });

        var viaList = new GraphBuilder();
        viaList.AddNodes(4);
        viaList.AddOutEdges(0, SpanOf([3, 1, 2]));

        Assert.Equal(viaList.Build(), viaArray.Build());
    }

    [Fact]
    public void AddOutEdgesWithEmptyArrayAddsNothing()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddOutEdges(0, Array.Empty<int>());
        Assert.Equal(TestGraphs.Isolated(2), b.Build());
    }

    [Fact]
    public void AddInEdgesFansIn()
    {
        var b = new GraphBuilder();
        b.AddNodes(3);
        b.AddInEdges(0, new[] { 1, 2 });
        Assert.Equal(TestGraphs.InStar(3), b.Build());
    }

    [Fact]
    public void AddInEdgesWithEmptyArrayAddsNothing()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddInEdges(0, Array.Empty<int>());
        Assert.Equal(TestGraphs.Isolated(2), b.Build());
    }

    // ---- parameter forms: loose arguments, int[], span of a list, SortedIntView ----

    [Fact]
    public void TheParameterFormsAgreeForEveryBatchMethod()
    {
        AssertFormsAgree("AddOutEdges",
            (b, ids) => b.AddOutEdges(0, ids),
            (b, ids) => b.AddOutEdges(0, ids[0], ids[1], ids[2]),
            (b, ids) => b.AddOutEdges(0, SpanOf([.. ids])),
            (b, ids) => b.AddOutEdges(0, SortedView(ids)));
        AssertFormsAgree("AddInEdges",
            (b, ids) => b.AddInEdges(0, ids),
            (b, ids) => b.AddInEdges(0, ids[0], ids[1], ids[2]),
            (b, ids) => b.AddInEdges(0, SpanOf([.. ids])),
            (b, ids) => b.AddInEdges(0, SortedView(ids)));
        AssertFormsAgree("AddOutEdgesAndNodes",
            (b, ids) => b.AddOutEdgesAndNodes(0, ids),
            (b, ids) => b.AddOutEdgesAndNodes(0, ids[0], ids[1], ids[2]),
            (b, ids) => b.AddOutEdgesAndNodes(0, SpanOf([.. ids])),
            (b, ids) => b.AddOutEdgesAndNodes(0, SortedView(ids)));
        AssertFormsAgree("AddInEdgesAndNodes",
            (b, ids) => b.AddInEdgesAndNodes(0, ids),
            (b, ids) => b.AddInEdgesAndNodes(0, ids[0], ids[1], ids[2]),
            (b, ids) => b.AddInEdgesAndNodes(0, SpanOf([.. ids])),
            (b, ids) => b.AddInEdgesAndNodes(0, SortedView(ids)));
        AssertFormsAgree("AddPath",
            (b, ids) => b.AddPath(ids),
            (b, ids) => b.AddPath(ids[0], ids[1], ids[2]),
            (b, ids) => b.AddPath(SpanOf([.. ids])),
            null);
        AssertFormsAgree("AddPathAndNodes",
            (b, ids) => b.AddPathAndNodes(ids),
            (b, ids) => b.AddPathAndNodes(ids[0], ids[1], ids[2]),
            (b, ids) => b.AddPathAndNodes(SpanOf([.. ids])),
            null);
    }

    [Fact]
    public void BatchMethodsWithNoTargetsAddNothing()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddOutEdges(0);
        b.AddInEdges(0);
        b.AddOutEdges(0, SortedIntView.Empty);
        b.AddInEdges(0, SpanOf([]));
        b.AddPath(SortedIntView.Empty);
        Assert.Equal(TestGraphs.Isolated(2), b.Build());
    }

    [Fact]
    public void AndNodesBatchMethodsWithNoTargetsStillCreateTheAnchor()
    {
        var b = new GraphBuilder();
        b.AddOutEdgesAndNodes(2);
        Assert.Equal(TestGraphs.Isolated(3), b.Build());

        var fanIn = new GraphBuilder();
        fanIn.AddInEdgesAndNodes(2, SortedIntView.Empty);
        Assert.Equal(TestGraphs.Isolated(3), fanIn.Build());

        var path = new GraphBuilder();
        path.AddPathAndNodes(SortedIntView.Empty);
        Assert.Equal(0, path.Build().NodeCount);
    }

    [Fact]
    public void SpanMethodsAcceptAdjacencyOfAnExistingGraph()
    {
        var source = TestGraphs.Path(4);
        var bi = BiAdjacentGraph.Of(source);
        var copy = new GraphBuilder();
        copy.AddNodes(4);
        for (int n = 0; n < source.NodeCount; n++)
        {
            copy.AddInEdges(n, bi.InNodes(n));
        }
        Assert.Equal(source, copy.Build());
    }

    [Fact]
    public void SpanArgumentsAreReadNotRetained()
    {
        var nodes = new List<int> { 1, 2 };
        var b = new GraphBuilder();
        b.AddNodes(4);
        b.AddOutEdges(0, SpanOf(nodes));
        b.AddInEdges(0, SpanOf(nodes));
        b.AddPath(SpanOf(nodes));
        b.AddOutEdgesAndNodes(3, SpanOf(nodes));
        b.AddInEdgesAndNodes(3, SpanOf(nodes));
        b.AddPathAndNodes(SpanOf(nodes));
        nodes.Clear();
        nodes.Add(3);
        Assert.Equal(TestGraphs.Of(4, 0, 1, 0, 2, 1, 0, 2, 0, 1, 2, 3, 1, 3, 2, 1, 3, 2, 3), b.Build());
    }

    [Fact]
    public void RejectsUnknownNodesInEverySpanForm()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        var invalid = new List<int> { 0, 5 };
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdges(0, SpanOf(invalid)));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddInEdges(0, SpanOf(invalid)));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddPath(SpanOf(invalid)));
        Assert.Equal(TestGraphs.Isolated(2), b.Build());
    }

    [Fact]
    public void AndNodesSpanMethodsRejectNegativeIdsAndAddNothing()
    {
        var b = new GraphBuilder();
        b.AddEdgeAndNodes(0, 1);
        var invalid = new List<int> { 2, -1 };
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdgesAndNodes(0, SpanOf(invalid)));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddInEdgesAndNodes(0, SpanOf(invalid)));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddPathAndNodes(SpanOf(invalid)));
        Assert.Equal(TestGraphs.Of(2, 0, 1), b.Build());
    }

    [Fact]
    public void AndNodesSpanMethodsRejectIdsFarBeyondTheNodeLimit()
    {
        var b = new GraphBuilder();
        var invalid = new List<int> { int.MaxValue };
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdgesAndNodes(0, SpanOf(invalid)));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddInEdgesAndNodes(0, SpanOf(invalid)));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddPathAndNodes(SpanOf(invalid)));
        Assert.Equal(0, b.Build().NodeCount);
    }

    private delegate void BatchCall(GraphBuilder builder, int[] ids);

    private static SortedIntView SortedView(int[] ids)
    {
        int[] sorted = [.. ids.Order()];
        return SortedIntView.ViewOf(sorted, 0, sorted.Length);
    }

    /// <summary>
    /// Applies the same three node IDs through an array, loose arguments, a span over a list and a
    /// <see cref="SortedIntView"/> (for the fan methods, where order does not matter), and asserts
    /// that all built the same graph.
    /// </summary>
    private static void AssertFormsAgree(string method, BatchCall viaArray, BatchCall viaArguments,
        BatchCall viaList, BatchCall? viaView)
    {
        var reference = BuildWith(viaArray);
        Assert.True(reference.Equals(BuildWith(viaArguments)), $"{method} from loose arguments");
        Assert.True(reference.Equals(BuildWith(viaList)), $"{method} from a span of a list");
        if (viaView is not null)
        {
            Assert.True(reference.Equals(BuildWith(viaView)), $"{method} from a SortedIntView");
        }
    }

    private static Graph BuildWith(BatchCall call)
    {
        var b = new GraphBuilder();
        b.AddNodes(4);
        call(b, [3, 1, 2]);
        return b.Build();
    }

    // ---- AddEdgePairs ----

    [Fact]
    public void AddEdgePairsAddsOneEdgePerPair()
    {
        var b = new GraphBuilder();
        b.AddNodes(4);
        b.AddEdgePairs(0, 1, 2, 3);
        Assert.Equal(TestGraphs.Of(4, 0, 1, 2, 3), b.Build());
    }

    [Fact]
    public void AddEdgePairsWithNoArgumentsAddsNothing()
    {
        var b = new GraphBuilder();
        b.AddNodes(2);
        b.AddEdgePairs();
        Assert.Equal(TestGraphs.Isolated(2), b.Build());
    }

    [Fact]
    public void AddEdgePairsWithOddArgumentCountThrows()
    {
        var b = new GraphBuilder();
        b.AddNodes(3);
        Assert.Throws<ArgumentException>(() => b.AddEdgePairs(0, 1, 2));
    }

    [Fact]
    public void AddEdgePairsAndNodesWithOddArgumentCountAddsNothing()
    {
        var b = new GraphBuilder();
        Assert.Throws<ArgumentException>(() => b.AddEdgePairsAndNodes(0, 1, 2));
        Assert.Equal(0, b.Build().NodeCount);
    }

    // ---- the AndNodes family ----

    [Fact]
    public void AddEdgeAndNodesCreatesEveryIdUpToTheHighest()
    {
        var b = new GraphBuilder();
        b.AddEdgeAndNodes(7, 3);
        var graph = b.Build();
        Assert.Equal(8, graph.NodeCount);
        Assert.Equal(1, graph.EdgeCount);
        Assert.Equal([3], graph.OutNodes(7).ToArray());
    }

    [Fact]
    public void AndNodesMethodsNeverReduceTheNodeCount()
    {
        var b = new GraphBuilder();
        b.AddNodes(10);
        b.AddEdgeAndNodes(0, 1);
        b.AddOutEdgesAndNodes(0, 2);
        b.AddInEdgesAndNodes(0, 3);
        b.AddPathAndNodes(4, 5);
        b.AddEdgePairsAndNodes(6, 7);
        b.AddEdgesAndNodes(BackEdges.Of(TestGraphs.Cycle(3)));
        Assert.Equal(10, b.Build().NodeCount);
    }

    [Fact]
    public void AddOutEdgesAndNodesMatchesPreallocatedVariant()
    {
        var grown = new GraphBuilder();
        grown.AddOutEdgesAndNodes(4, 0, 9);

        var preallocated = new GraphBuilder();
        preallocated.AddNodes(10);
        preallocated.AddOutEdges(4, 0, 9);

        Assert.Equal(preallocated.Build(), grown.Build());
    }

    [Fact]
    public void AddInEdgesAndNodesMatchesPreallocatedVariant()
    {
        var grown = new GraphBuilder();
        grown.AddInEdgesAndNodes(4, 0, 9);

        var preallocated = new GraphBuilder();
        preallocated.AddNodes(10);
        preallocated.AddInEdges(4, 0, 9);

        Assert.Equal(preallocated.Build(), grown.Build());
    }

    [Fact]
    public void AddPathAndNodesMatchesPreallocatedVariant()
    {
        var grown = new GraphBuilder();
        grown.AddPathAndNodes(2, 5, 0);

        var preallocated = new GraphBuilder();
        preallocated.AddNodes(6);
        preallocated.AddPath(2, 5, 0);

        Assert.Equal(preallocated.Build(), grown.Build());
    }

    [Fact]
    public void AddPathAndNodesWithOneNodeStillCreatesIt()
    {
        var b = new GraphBuilder();
        b.AddPathAndNodes(4);
        Assert.Equal(TestGraphs.Isolated(5), b.Build());
    }

    [Fact]
    public void AndNodesMethodsRejectNegativeIds()
    {
        var b = new GraphBuilder();
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdgeAndNodes(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdgeAndNodes(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdgesAndNodes(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddInEdgesAndNodes(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddPathAndNodes(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdgePairsAndNodes(0, -1));
        // Not reachable through results of a graph; checked on a hand-made list.
        var negative = Edges.Wrap([0], [-1], new int[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdgesAndNodes(negative));
    }

    [Fact]
    public void AndNodesMethodsRejectIdsFarBeyondTheNodeLimit()
    {
        var b = new GraphBuilder();
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdgeAndNodes(0, int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddPathAndNodes(int.MaxValue));
        Assert.Equal(0, b.Build().NodeCount);
    }

    [Fact]
    public void AndNodesMethodsLeaveTheBuilderUnchangedWhenRejected()
    {
        var b = new GraphBuilder();
        b.AddEdgeAndNodes(0, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdgesAndNodes(0, 2, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddPathAndNodes(0, 3, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdgesAndNodes(
            Edges.Wrap([0, 4], [5, int.MaxValue], new int[2])));
        Assert.Equal(TestGraphs.Of(2, 0, 1), b.Build());
    }

    [Fact]
    public void AddChildrenReportsAnInvalidCountBeforeAnInvalidAnchor()
    {
        var b = new GraphBuilder();
        Assert.Throws<ArgumentException>(() => b.AddChildren(99, -1));
        Assert.Throws<ArgumentException>(() => b.AddParents(99, -1));
    }

    [Fact]
    public void AddEdgesTakesTheEdgesOfAResult()
    {
        // 0 -> 1 -> 2 -> 0, 2 -> 1: the back edges, as a graph of their own.
        Graph graph = TestGraphs.Of(3, 0, 1, 1, 2, 2, 0, 2, 1);
        var back = BackEdges.Of(graph);
        var b = new GraphBuilder();
        b.AddNodes(graph.NodeCount);
        b.AddEdges(back);
        Graph backGraph = b.Build();
        Assert.Equal(back.Count, backGraph.EdgeCount);
        for (int p = 0; p < back.Count; p++)
        {
            Assert.True(backGraph.HasOutNode(back.SourceNodes[p], back.TargetNodes[p]));
        }
    }

    [Fact]
    public void AddEdgesRejectsAMissingSourceAndAddsNothing()
    {
        var back = BackEdges.Of(TestGraphs.Cycle(3)); // 2 -> 0
        var b = new GraphBuilder();
        b.AddNodes(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdges(back));
        Assert.Equal(0, b.Build().EdgeCount);
    }

    [Fact]
    public void AddEdgesRejectsAMissingTargetAtALaterPositionAndAddsNothing()
    {
        var bridges = Components.BiconnectivityOf(BiAdjacentGraph.Of(TestGraphs.Path(3))).Bridges;
        Assert.Equal([1, 2], bridges.TargetNodes.ToArray());
        var b = new GraphBuilder();
        b.AddNodes(2);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdges(bridges));
        Assert.Contains("target", ex.Message, StringComparison.Ordinal);
        Assert.Equal(TestGraphs.Isolated(2), b.Build());
    }

    [Fact]
    public void ANodeOutOfRangeNamesTheParameterOfTheMethodCalled()
    {
        // The message keeps Java's role word ("path node", "endpoint node"), while ParamName is the
        // parameter that the caller passed.
        Edges back = BackEdges.Of(TestGraphs.Cycle(3)); // 2 -> 0
        Edges bridges = Components.BiconnectivityOf(BiAdjacentGraph.Of(TestGraphs.Path(3))).Bridges; // 0 -> 1, 1 -> 2
        (Action<GraphBuilder> Call, string Parameter)[] cases =
        [
            (b => b.AddChild(9), "parent"),
            (b => b.AddChildren(9, 1), "parent"),
            (b => b.AddParent(9), "child"),
            (b => b.AddParents(9, 1), "child"),
            (b => b.AddEdge(9, 0), "source"),
            (b => b.AddEdge(0, 9), "target"),
            (b => b.AddEdgeAndNodes(-1, 0), "source"),
            (b => b.AddEdgeAndNodes(0, -1), "target"),
            (b => b.AddOutEdges(9, 0), "source"),
            (b => b.AddOutEdges(0, 9), "targets"),
            (b => b.AddOutEdgesAndNodes(-1, 0), "source"),
            (b => b.AddOutEdgesAndNodes(0, -1), "targets"),
            (b => b.AddInEdges(9, 0), "target"),
            (b => b.AddInEdges(0, 9), "sources"),
            (b => b.AddInEdgesAndNodes(-1, 0), "target"),
            (b => b.AddInEdgesAndNodes(0, -1), "sources"),
            (b => b.AddPath(0, 1, 7), "nodes"),
            (b => b.AddPathAndNodes(0, -1), "nodes"),
            (b => b.AddEdgePairs(0, 9), "endpoints"),
            (b => b.AddEdgePairsAndNodes(0, -1), "endpoints"),
            (b => b.AddEdges(back), "edges"),
            (b => b.AddEdges(bridges), "edges"),
        ];
        foreach (var (call, parameter) in cases)
        {
            var b = new GraphBuilder();
            b.AddNodes(2);
            Assert.Equal(parameter, Assert.Throws<ArgumentOutOfRangeException>(() => call(b)).ParamName);
        }
        var path = Assert.Throws<ArgumentOutOfRangeException>(() => new GraphBuilder().AddPath(0));
        Assert.StartsWith("path node does not exist: 0 (nodeCount=0)", path.Message, StringComparison.Ordinal);
        var edges = new GraphBuilder();
        edges.AddNodes(2);
        Assert.StartsWith("target node does not exist: 2", Assert.Throws<ArgumentOutOfRangeException>(
            () => edges.AddEdges(bridges)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddEdgesAndNodesCreatesTheNodesItReferences()
    {
        var bridges = Components.BiconnectivityOf(BiAdjacentGraph.Of(TestGraphs.Path(5))).Bridges;
        var b = new GraphBuilder();
        b.AddEdgesAndNodes(bridges);
        Assert.Equal(TestGraphs.Path(5), b.Build());
    }

    [Fact]
    public void AddEdgesOfAnEmptyResultAddsNothing()
    {
        var b = new GraphBuilder();
        b.AddEdges(BackEdges.Of(TestGraphs.Path(3)));
        b.AddEdgesAndNodes(BackEdges.Of(TestGraphs.Path(3)));
        Assert.Equal(0, b.Build().NodeCount);
    }

    // ---- node limit ----

    [Fact]
    public void SingleNodeMethodsFailAtTheNodeLimit()
    {
        var b = new GraphBuilder();
        b.AddNodes(NodeLimit);
        Assert.Throws<InvalidOperationException>(() => b.AddNode());
        Assert.Throws<InvalidOperationException>(() => b.AddChild(0));
        Assert.Throws<InvalidOperationException>(() => b.AddParent(0));
        Assert.Equal(NodeLimit, b.AddNodes(0));
        Assert.Equal(0, b.EdgeCapacity);
    }

    [Fact]
    public void CountMethodsRejectACountBeyondTheNodeLimit()
    {
        var b = new GraphBuilder();
        Assert.Throws<ArgumentException>(() => b.AddNodes(int.MaxValue));
        b.AddNodes(NodeLimit - 2);
        var ex = Assert.Throws<ArgumentException>(() => b.AddNodes(3));
        Assert.Contains("node limit", ex.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => b.AddChildren(0, 3));
        Assert.Throws<ArgumentException>(() => b.AddParents(0, 3));
        Assert.Equal(NodeLimit - 2, b.AddNodes(0));
        Assert.Equal(0, b.EdgeCapacity);
        Assert.Equal(NodeLimit - 2, b.AddChildren(0, 2));
        Assert.Equal(NodeLimit, b.AddNodes(0));
    }

    [Fact]
    public void AddGraphRejectsAGraphBeyondTheNodeLimit()
    {
        var b = new GraphBuilder();
        b.AddNodes(NodeLimit - 2);
        Assert.Throws<ArgumentException>(() => b.AddGraph(TestGraphs.Path(3)));
        Assert.Equal(NodeLimit - 2, b.AddNodes(0));
        Assert.Equal(0, b.EdgeCapacity);
        Assert.Equal(NodeLimit - 2, b.AddGraph(TestGraphs.Path(2)));
        Assert.Equal(NodeLimit, b.AddNodes(0));
    }

    [Fact]
    public void AndNodesMethodsRejectIdsAtTheNodeLimit()
    {
        var b = new GraphBuilder();
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdgeAndNodes(0, NodeLimit));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddOutEdgesAndNodes(NodeLimit, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddInEdgesAndNodes(0, SpanOf([NodeLimit])));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddPathAndNodes(0, NodeLimit));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdgePairsAndNodes(NodeLimit, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddEdgesAndNodes(Edges.Wrap([0], [NodeLimit], new int[1])));
        Assert.Equal(0, b.AddNodes(0));
        // The highest creatable ID fills the builder up to the limit.
        b.AddEdgeAndNodes(0, NodeLimit - 1);
        Assert.Equal(NodeLimit, b.AddNodes(0));
        Assert.Throws<InvalidOperationException>(() => b.AddNode());
    }

    // ---- edge limit ----

    [Fact]
    public void CountMethodsRejectEdgesBeyondTheEdgeLimitAndChangeNothing()
    {
        // Three edges plus NodeLimit - 1 new ones are one more than the edge limit; the node count
        // would still fit.
        var b = new GraphBuilder();
        b.AddNode();
        b.AddOutEdges(0, 0, 0, 0);
        var ex = Assert.Throws<ArgumentException>(() => b.AddChildren(0, NodeLimit - 1));
        Assert.Contains("edge limit", ex.Message, StringComparison.Ordinal);
        ex = Assert.Throws<ArgumentException>(() => b.AddParents(0, NodeLimit - 1));
        Assert.Contains("edge limit", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, b.AddNodes(0));
        Assert.Equal(TestGraphs.Of(1, 0, 0), b.Build());
    }

    [Fact]
    public void BatchMethodsRejectEdgesBeyondTheEdgeLimitBeforeCreatingNodes()
    {
        // One edge plus four new ones on a builder whose edge limit is 4. Java reaches its real
        // limit with lists backed by no array; a span always has memory behind it, so the C# test
        // lowers the limit instead. A path of k nodes has k - 1 edges.
        var b = new GraphBuilder(edgeLimit: 4);
        b.AddEdgeAndNodes(0, 0);
        int[] zeros = new int[4];
        int[] path = new int[5];
        var ex = Assert.Throws<ArgumentException>(() => b.AddOutEdges(0, zeros));
        Assert.Contains("edge limit", ex.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => b.AddInEdges(0, zeros));
        Assert.Throws<ArgumentException>(() => b.AddPath(path));
        Assert.Throws<ArgumentException>(() => b.AddEdgePairs(0, 0, 0, 0, 0, 0, 0, 0));
        Assert.Throws<ArgumentException>(() => b.AddEdges(Edges.Wrap(zeros, zeros, new int[4])));
        Assert.Throws<ArgumentException>(() => b.AddOutEdgesAndNodes(5, zeros));
        Assert.Throws<ArgumentException>(() => b.AddInEdgesAndNodes(5, zeros));
        Assert.Throws<ArgumentException>(() => b.AddPathAndNodes(5, 0, 0, 0, 0));
        Assert.Throws<ArgumentException>(() => b.AddEdgePairsAndNodes(5, 0, 0, 0, 0, 0, 0, 0));
        Assert.Throws<ArgumentException>(() => b.AddEdgesAndNodes(Edges.Wrap([5, 0, 0, 0], zeros, new int[4])));
        Assert.Throws<ArgumentException>(() => b.AddGraph(TestGraphs.Path(5)));
        Assert.Throws<ArgumentException>(() => b.EnsureEdgeCapacity(5));
        Assert.Equal(1, b.AddNodes(0));
        Assert.Equal(TestGraphs.Of(1, 0, 0), b.Build());

        // Up to the limit itself, a batch is accepted.
        b.AddOutEdgesAndNodes(3, 0, 0, 0);
        Assert.Equal(4, b.AddNodes(0));
        Assert.Equal(TestGraphs.Of(4, 0, 0, 3, 0), b.Build());
    }

    [Fact]
    public void BuildRejectsMoreEdgesThanTheEdgeLimit()
    {
        // Only the single-edge methods get past the limit; the check that Build runs is tested on
        // its own at the real limit, and through Build under a lower one.
        GraphBuilder.CheckEdgeLimit(EdgeLimit, EdgeLimit);
        var ex = Assert.Throws<ArgumentException>(() => GraphBuilder.CheckEdgeLimit(EdgeLimit + 1, EdgeLimit));
        Assert.Equal($"{EdgeLimit + 1} edges added exceed the edge limit of {EdgeLimit}", ex.Message);
        Assert.Throws<ArgumentException>(() => GraphBuilder.CheckEdgeLimit(int.MaxValue, EdgeLimit));

        var b = new GraphBuilder(edgeLimit: 2);
        b.AddEdgeAndNodes(0, 1);
        b.AddEdge(1, 0);
        Assert.Equal(TestGraphs.Of(2, 0, 1, 1, 0), b.Build());
        b.AddEdge(0, 0);
        ex = Assert.Throws<ArgumentException>(b.Build);
        Assert.Equal("3 edges added exceed the edge limit of 2", ex.Message);
    }
}
