using System.Collections;

namespace Graphity.Core.Tests;

public sealed class NodeMappingTests
{
    private static BitArray BitsOf(int length, params ReadOnlySpan<int> set)
    {
        var bits = new BitArray(length);
        foreach (int bit in set)
        {
            bits[bit] = true;
        }
        return bits;
    }

    [Fact]
    public void OfFullMappingHasNoUnmappedEntries()
    {
        var nm = NodeMapping.Of([2, 0, 1]);
        Assert.Equal(3, nm.MappedCount);
        Assert.False(nm.IsPartial);
        Assert.Equal(2, nm.Get(0));
        Assert.Equal(0, nm.Get(1));
        Assert.Equal(1, nm.Get(2));
    }

    [Fact]
    public void OfPartialMappingMarksIsPartial()
    {
        var nm = NodeMapping.Of([2, -1, 0, -1]);
        Assert.Equal(2, nm.MappedCount);
        Assert.True(nm.IsPartial);
        Assert.True(nm.IsMapped(0));
        Assert.False(nm.IsMapped(1));
        Assert.True(nm.IsMapped(2));
        Assert.False(nm.IsMapped(3));
    }

    [Fact]
    public void OfEmptyArrayCreatesEmptyMapping()
    {
        var nm = NodeMapping.Of([]);
        Assert.Equal(0, nm.MappedCount);
        Assert.False(nm.IsPartial);
    }

    [Fact]
    public void OfRejectsValuesBelowMinusOne()
    {
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => NodeMapping.Of([0, -2, 1]));
        Assert.Contains("-2", e.Message);
    }

    [Fact]
    public void MessagesWriteNegativeValuesAsJavaDoesInEveryCulture()
    {
        using var culture = new ForeignCulture();
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => NodeMapping.Of([0, -2, 1]));
        Assert.StartsWith("a mapping value must be -1 or a node ID, but node 1 maps to -2", e.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OfAcceptsRepeatsAsTheCallersContract()
    {
        var nm = NodeMapping.Of([0, 0]);
        Assert.Equal(2, nm.MappedCount);
        Assert.Throws<ArgumentException>(() => nm.Inverted(1));
    }

    [Fact]
    public void OfCopiesInputArray()
    {
        int[] input = [1, 2, 3];
        var nm = NodeMapping.Of(input);
        input[0] = 999;
        Assert.Equal(1, nm.Get(0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(3)]
    [InlineData(1000)]
    public void GetOutOfRangeThrows(int id)
    {
        var nm = NodeMapping.Of([10, 20, 30]);
        Assert.Throws<ArgumentOutOfRangeException>(() => nm.Get(id));
        Assert.Throws<ArgumentOutOfRangeException>(() => nm.IsMapped(id));
    }

    [Fact]
    public void IndexCountIsTheSourceSpaceSizeIncludingUnmappedIds()
    {
        var nm = NodeMapping.Of([2, -1, 0, -1]);
        Assert.Equal(4, nm.IndexCount);
        Assert.Equal(2, nm.MappedCount);
        Assert.Equal(0, NodeMapping.Of([]).IndexCount);
    }

    [Fact]
    public void ForSubgraphFromBitArrayMapsDenseToOriginal()
    {
        var nm = NodeMapping.ForSubgraph(BitsOf(8, 2, 5, 7));
        Assert.Equal(3, nm.MappedCount);
        Assert.False(nm.IsPartial);
        Assert.Equal(2, nm.Get(0));
        Assert.Equal(5, nm.Get(1));
        Assert.Equal(7, nm.Get(2));
    }

    [Fact]
    public void ForSubgraphFromEmptyBitArrayCreatesEmptyMapping()
    {
        var nm = NodeMapping.ForSubgraph(new BitArray(0));
        Assert.Equal(0, nm.MappedCount);
        Assert.False(nm.IsPartial);
    }

    [Fact]
    public void ForSubgraphFromUnsortedSpanSortsIds()
    {
        int[] unsorted = [5, 2, 7];
        var nm = NodeMapping.ForSubgraph(unsorted);
        Assert.Equal(3, nm.MappedCount);
        Assert.Equal(2, nm.Get(0));
        Assert.Equal(5, nm.Get(1));
        Assert.Equal(7, nm.Get(2));
    }

    [Fact]
    public void ForSubgraphFromSortedIntViewSkipsSort()
    {
        var sorted = new SortedIntView([2, 5, 7]);
        var nm = NodeMapping.ForSubgraph(sorted);
        Assert.Equal(2, nm.Get(0));
        Assert.Equal(5, nm.Get(1));
        Assert.Equal(7, nm.Get(2));
    }

    [Fact]
    public void ForSubgraphRejectsDuplicatesInList()
    {
        int[] withDupes = [3, 1, 3];
        var e = Assert.Throws<ArgumentException>(() => NodeMapping.ForSubgraph(withDupes));
        Assert.Contains("3", e.Message);
    }

    [Fact]
    public void ForSubgraphRejectsANegativeIdInSortedIntView()
    {
        var withNegative = new SortedIntView([-1, 2]);
        Assert.Throws<ArgumentOutOfRangeException>(() => NodeMapping.ForSubgraph(withNegative));
    }

    [Fact]
    public void ForSubgraphRejectsNegativeIds()
    {
        int[] array = [2, -1];
        Assert.Throws<ArgumentOutOfRangeException>(() => NodeMapping.ForSubgraph(array));
        Assert.Throws<ArgumentOutOfRangeException>(() => NodeMapping.ForSubgraph(2, -1));
    }

    [Fact]
    public void ForSubgraphFromLooseArgumentsSortsIds()
    {
        var nm = NodeMapping.ForSubgraph(5, 2, 7);
        Assert.Equal(3, nm.MappedCount);
        Assert.False(nm.IsPartial);
        Assert.Equal(2, nm.Get(0));
        Assert.Equal(5, nm.Get(1));
        Assert.Equal(7, nm.Get(2));
    }

    [Fact]
    public void ForSubgraphFromNoArgumentsCreatesEmptyMapping()
    {
        var nm = NodeMapping.ForSubgraph();
        Assert.Equal(0, nm.MappedCount);
        Assert.False(nm.IsPartial);
    }

    [Fact]
    public void ForSubgraphCopiesInputArray()
    {
        int[] input = [5, 2, 7];
        var nm = NodeMapping.ForSubgraph(input);
        Assert.Equal([5, 2, 7], input);
        input[0] = 999;
        Assert.Equal(5, nm.Get(1));
    }

    [Fact]
    public void ForSubgraphArrayAndBitArrayAgree()
    {
        var fromBits = NodeMapping.ForSubgraph(BitsOf(8, 2, 5, 7));
        var fromArray = NodeMapping.ForSubgraph(7, 2, 5);
        Assert.Equal(fromBits.MappedCount, fromArray.MappedCount);
        for (int id = 0; id < fromBits.MappedCount; id++)
        {
            Assert.Equal(fromBits.Get(id), fromArray.Get(id));
        }
    }

    [Fact]
    public void ForSubgraphRejectsDuplicatesInArray() =>
        Assert.Throws<ArgumentException>(() => NodeMapping.ForSubgraph(3, 1, 3));

    [Fact]
    public void ForPermutationWrapsOrder()
    {
        var nm = NodeMapping.ForPermutation([2, 0, 1]);
        Assert.Equal(3, nm.MappedCount);
        Assert.False(nm.IsPartial);
        Assert.Equal(2, nm.Get(0));
        Assert.Equal(0, nm.Get(1));
        Assert.Equal(1, nm.Get(2));
    }

    [Fact]
    public void ForPermutationAcceptsAnImmutableArray()
    {
        var order = TopOrder.Of(TestGraphs.Of(3, 2, 0, 0, 1));
        var nm = NodeMapping.ForPermutation(order.AsSpan());
        Assert.Equal([2, 0, 1], new[] { nm.Get(0), nm.Get(1), nm.Get(2) });
    }

    [Fact]
    public void ForPermutationRejectsDuplicates()
    {
        var e = Assert.Throws<ArgumentException>(() => NodeMapping.ForPermutation([0, 1, 1]));
        Assert.Contains("1", e.Message);
    }

    [Fact]
    public void ForPermutationRejectsOutOfRangeValue()
    {
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => NodeMapping.ForPermutation([0, 3, 1]));
        Assert.Contains("3", e.Message);
    }

    [Fact]
    public void ForPermutationRejectsNegativeValue() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => NodeMapping.ForPermutation([0, -1, 1]));

    [Fact]
    public void InverseOfFullPermutationIsInjective()
    {
        var inv = NodeMapping.Of([2, 0, 1]).Inverted(3);
        Assert.Equal(1, inv.Get(0));
        Assert.Equal(2, inv.Get(1));
        Assert.Equal(0, inv.Get(2));
        Assert.False(inv.IsPartial);
        Assert.Equal(3, inv.MappedCount);
    }

    [Fact]
    public void InverseOfPartialMappingIsPartial()
    {
        var inv = NodeMapping.Of([5, -1, 3]).Inverted(7);
        Assert.Equal(2, inv.MappedCount);
        Assert.Equal(7, inv.IndexCount);
        Assert.True(inv.IsPartial);
        Assert.Equal(2, inv.Get(3));
        Assert.Equal(0, inv.Get(5));
        Assert.Equal(-1, inv.Get(0));
        Assert.Equal(-1, inv.Get(4));
        Assert.Equal(-1, inv.Get(6));
    }

    [Fact]
    public void InverseOfSubgraphCoversTheWholeOldGraph()
    {
        // Path with 5 nodes, subgraph {0, 1}: the inverse maps all 5 old nodes, 2..4 to -1.
        var oldToNew = NodeMapping.ForSubgraph(0, 1).Inverted(5);
        Assert.True(oldToNew.IsPartial);
        Assert.Equal(5, oldToNew.IndexCount);
        Assert.Equal(-1, oldToNew.Get(4));
        Assert.Equal([7, 8, 0, 0, 0], oldToNew.Gather<int>([7, 8]));
        var oldNames = new string?[5];
        oldToNew.Gather<string?>(["a", "b"], oldNames);
        Assert.Equal(new string?[] { "a", "b", null, null, null }, oldNames);
    }

    [Fact]
    public void InverseRejectsATargetOutsideTheTargetSpace()
    {
        var nm = NodeMapping.ForSubgraph(0, 4);
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => nm.Inverted(4));
        Assert.StartsWith("index 1 maps to 4, but targetCount is 4", e.Message, StringComparison.Ordinal);
        Assert.Equal("targetCount", e.ParamName);
        Assert.Equal(4, e.ActualValue);
        Assert.Equal(5, nm.Inverted(5).IndexCount);
    }

    [Fact]
    public void InverseRejectsANegativeTargetCount() =>
        Assert.Throws<ArgumentException>(() => NodeMapping.Of([]).Inverted(-1));

    [Fact]
    public void InverseRejectsATargetCountBeyondTheLargestArray()
    {
        // Rejected before the inverse is allocated, as no graph has that many nodes.
        var nm = NodeMapping.Of([]);
        var e = Assert.Throws<ArgumentException>(() => nm.Inverted(int.MaxValue));
        Assert.Contains("targetCount", e.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => nm.Inverted(Array.MaxLength + 1));
    }

    [Fact]
    public void InverseOfNonInjectiveThrows()
    {
        var e = Assert.Throws<ArgumentException>(() => NodeMapping.Of([1, 1, 2]).Inverted(3));
        Assert.Equal("cannot invert mapping: indices 0 and 1 both map to 1", e.Message);
    }

    [Fact]
    public void InverseOfEmptyIsEmpty()
    {
        var inv = NodeMapping.Of([]).Inverted(0);
        Assert.Equal(0, inv.MappedCount);
        Assert.Equal(0, inv.IndexCount);
    }

    [Fact]
    public void InverseOfAllMinusOneMapsNothing()
    {
        var inv = NodeMapping.Of([-1, -1, -1]).Inverted(2);
        Assert.Equal(0, inv.MappedCount);
        Assert.Equal(2, inv.IndexCount);
        Assert.True(inv.IsPartial);
    }

    [Fact]
    public void InverseRoundTripRestoresOriginal()
    {
        var roundtrip = NodeMapping.Of([2, 0, 1]).Inverted(3).Inverted(3);
        Assert.Equal(2, roundtrip.Get(0));
        Assert.Equal(0, roundtrip.Get(1));
        Assert.Equal(1, roundtrip.Get(2));
    }

    [Fact]
    public void DoubleInversionKeepsTheSourceSpace()
    {
        var roundtrip = NodeMapping.Of([-1, 0, -1]).Inverted(1).Inverted(3);
        Assert.Equal(3, roundtrip.IndexCount);
        Assert.Equal([0, 5, 0], roundtrip.Gather<int>([5]));
    }

    [Fact]
    public void ComposeChainsTwoMappings()
    {
        var composed = NodeMapping.Of([10, 20, 30]).Compose(NodeMapping.Of([1, 0, 2]));
        Assert.Equal(20, composed.Get(0));
        Assert.Equal(10, composed.Get(1));
        Assert.Equal(30, composed.Get(2));
        Assert.False(composed.IsPartial);
    }

    [Fact]
    public void ComposePropagatesUnmappedFromInner()
    {
        var composed = NodeMapping.Of([10, 20, 30]).Compose(NodeMapping.Of([1, -1, 2]));
        Assert.Equal(20, composed.Get(0));
        Assert.Equal(-1, composed.Get(1));
        Assert.Equal(30, composed.Get(2));
        Assert.True(composed.IsPartial);
    }

    [Fact]
    public void ComposePropagatesUnmappedFromOuter()
    {
        var composed = NodeMapping.Of([10, -1, 30]).Compose(NodeMapping.Of([1, 0, 2]));
        Assert.Equal(-1, composed.Get(0));
        Assert.Equal(10, composed.Get(1));
        Assert.Equal(30, composed.Get(2));
        Assert.True(composed.IsPartial);
    }

    [Fact]
    public void ComposeRejectsInnerValueOutOfOuterRange()
    {
        var outer = NodeMapping.Of([10, 20]);
        var inner = NodeMapping.Of([0, 5, 1]);
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => outer.Compose(inner));
        Assert.StartsWith("inner maps 1 to 5, outside the indices 0..1 of this mapping", e.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeReturnsNodeMapping() =>
        Assert.IsType<NodeMapping>(NodeMapping.Of([10, 20]).Compose(NodeMapping.Of([0, 1])));

    [Fact]
    public void ReindexEmptyGraphReturnsEmptyMapping() =>
        Assert.Equal(0, NodeMapping.Reindex(new GraphBuilder().Build()).MappedCount);

    [Fact]
    public void ReindexSingleNodeGraphReturnsIdentity()
    {
        var builder = new GraphBuilder();
        builder.AddNode();
        var nm = NodeMapping.Reindex(builder.Build());
        Assert.Equal(1, nm.MappedCount);
        Assert.Equal(0, nm.Get(0));
    }

    [Fact]
    public void ReindexIsPermutationOfAllNodes()
    {
        // 0 -> 1 -> 2, 0 -> 3
        var builder = new GraphBuilder();
        builder.AddNodes(4);
        builder.AddEdge(0, 1);
        builder.AddEdge(1, 2);
        builder.AddEdge(0, 3);
        var nm = NodeMapping.Reindex(builder.Build());
        Assert.Equal(4, nm.MappedCount);
        Assert.False(nm.IsPartial);
        var targets = new int[4];
        for (int i = 0; i < 4; i++)
        {
            targets[i] = nm.Get(i);
        }
        Assert.Equal(new[] { 0, 1, 2, 3 }, targets.Order().ToArray());
    }

    [Fact]
    public void ReindexPlacesDfsChildrenAdjacent()
    {
        // Path 0 -> 1 -> 2 -> 3: DFS from 0 visits 0, 1, 2, 3 in order.
        var builder = new GraphBuilder();
        builder.AddNodes(4);
        builder.AddEdge(0, 1);
        builder.AddEdge(1, 2);
        builder.AddEdge(2, 3);
        var nm = NodeMapping.Reindex(builder.Build());
        Assert.Equal(0, nm.Get(0));
        Assert.Equal(1, nm.Get(1));
        Assert.Equal(2, nm.Get(2));
        Assert.Equal(3, nm.Get(3));
    }

    // --- Gather ---

    [Fact]
    public void GatherMovesValuesToTheNewNumbering()
    {
        // new -> old: 0 -> 2, 1 -> -1 (unmapped), 2 -> 0
        var m = NodeMapping.Of([2, -1, 0]);
        Assert.Equal([12, 0, 10], m.Gather<int>([10, 11, 12]));
        Assert.Equal([12L, 0L, 10L], m.Gather<long>([10, 11, 12]));
        Assert.Equal([3.5, 0.0, 1.5], m.Gather<double>([1.5, 2.5, 3.5]));
        string[] newValues = ["x", "keep", "y"];
        m.Gather<string>(["a", "b", "c"], newValues);
        Assert.Equal(["c", "keep", "a"], newValues);
    }

    [Fact]
    public void GatherAlongASubgraph()
    {
        var newToOld = NodeMapping.ForSubgraph(1, 3);
        Assert.Equal([10, 30], newToOld.Gather<int>([0, 10, 20, 30]));
    }

    [Fact]
    public void GatherOfObjectsLeavesUnmappedEntriesAsTheyAre()
    {
        // From old to new along the subgraph {1, 3} of four nodes: the old nodes 0 and 2 are not
        // mapped. Objects go through the Gather that writes into the caller's array, as in Java;
        // the allocating one takes unmanaged value types only.
        var oldToNew = NodeMapping.ForSubgraph(1, 3).Inverted(4);
        Assert.True(oldToNew.IsPartial);
        string[] oldNames = ["zero", "one", "two", "three"];
        oldToNew.Gather<string>(["ONE", "THREE"], oldNames);
        Assert.Equal(["zero", "ONE", "two", "THREE"], oldNames);
    }

    [Fact]
    public void GatherRejectsWrongArrays()
    {
        var m = NodeMapping.Of([2, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => m.Gather<int>([1, 2]));
        Assert.Throws<ArgumentException>(() => m.Gather<string?>(["a", "b", "c"], new string?[3]));
    }

    [Fact]
    public void GatherOutsideTheValuesThrowsWithTheMessageOfJavasArrayAccess()
    {
        // Java reads values[2] of a two-element array and throws ArrayIndexOutOfBoundsException.
        var m = NodeMapping.Of([2, 0]);
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => m.Gather<long>([1L, 2L]));
        Assert.StartsWith("Index 2 out of bounds for length 2", e.Message, StringComparison.Ordinal);
        Assert.Equal("values", e.ParamName);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => m.Gather<string>(["a", "b"], new string[2]));
        Assert.StartsWith("Index 2 out of bounds for length 2", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GatherRejectsTheSameArrayForValuesAndNewValues()
    {
        var swap = NodeMapping.ForPermutation([1, 0]);
        string[] values = ["a", "b"];
        Assert.Throws<ArgumentException>(() => swap.Gather<string>(values, values));
        Assert.Equal(["a", "b"], values);
    }
}
