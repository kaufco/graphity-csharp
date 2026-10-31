namespace Graphity.Core.Tests;

/// <summary>
/// EdgeMapping inherits Get, IsMapped, MappedCount, IsPartial, IndexCount and the inverse and compose helpers from
/// IndexMapping, all exercised through NodeMappingTests. Here: the EdgeMapping-specific result types
/// of <see cref="EdgeMapping.Inverted"/> and <see cref="EdgeMapping.Compose"/>, and Gather. The
/// integration with <see cref="Graphs.EdgeMapping"/> is covered in GraphsTests.
/// </summary>
public sealed class EdgeMappingTests
{
    [Fact]
    public void InverseReturnsEdgeMapping()
    {
        var em = new EdgeMapping([2, 0, 1], 3);
        EdgeMapping inv = em.Inverted(3);
        Assert.IsType<EdgeMapping>(inv);
        Assert.Equal(1, inv.Get(0));
        Assert.Equal(2, inv.Get(1));
        Assert.Equal(0, inv.Get(2));
    }

    [Fact]
    public void ComposeReturnsEdgeMapping()
    {
        var outer = new EdgeMapping([10, 20, 30], 3);
        var inner = new EdgeMapping([1, 0, 2], 3);
        EdgeMapping composed = outer.Compose(inner);
        Assert.IsType<EdgeMapping>(composed);
        Assert.Equal(20, composed.Get(0));
        Assert.Equal(10, composed.Get(1));
        Assert.Equal(30, composed.Get(2));
    }

    [Fact]
    public void InverseOfMappingWithHolesIsPartial()
    {
        // Source 1 has no target, so one of the three targets has no source in the inverse.
        var em = new EdgeMapping([2, -1, 0], 2);
        var inv = em.Inverted(3);
        Assert.True(inv.IsPartial);
        Assert.Equal(-1, inv.Get(1));
    }

    [Fact]
    public void InverseCoversTheWholeTargetSpace()
    {
        // Path 0 -> 1 -> 2 -> 3 restricted to nodes {0, 1, 2}: the old edge 2 has no new edge.
        var graph = TestGraphs.Path(4);
        var keep = NodeMapping.ForSubgraph(0, 1, 2);
        var newToOld = Graphs.EdgeMapping(Graphs.Remapped(graph, keep), graph, keep);
        var oldToNew = newToOld.Inverted(graph.EdgeCount);
        Assert.True(oldToNew.IsPartial);
        Assert.Equal(3, oldToNew.IndexCount);
        Assert.Equal([0.5, 1.5, 0.0], oldToNew.Gather<double>([0.5, 1.5]));
        Assert.Throws<ArgumentOutOfRangeException>(() => newToOld.Inverted(1));
    }

    [Fact]
    public void InverseRejectsATargetCountBeyondTheLargestArray()
    {
        var em = new EdgeMapping([], 0);
        var ex = Assert.Throws<ArgumentException>(() => em.Inverted(Array.MaxLength + 1));
        Assert.Contains("targetCount", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeWithHolesPropagatesPartiality()
    {
        var outer = new EdgeMapping([10, 20, 30], 3);
        var inner = new EdgeMapping([1, -1, 2], 2);
        var composed = outer.Compose(inner);
        Assert.True(composed.IsPartial);
        Assert.Equal(-1, composed.Get(1));
    }

    [Fact]
    public void GatherMovesEdgeValuesToTheNewNumbering()
    {
        var em = new EdgeMapping([2, -1, 1], 2);
        Assert.Equal([7, 0, 6], em.Gather<int>([5, 6, 7]));
        Assert.Equal([7L, 0L, 6L], em.Gather<long>([5, 6, 7]));
        Assert.Equal([2.5, 0.0, 1.5], em.Gather<double>([0.5, 1.5, 2.5]));
        Assert.Throws<ArgumentOutOfRangeException>(() => em.Gather<int>([5, 6]));
        Assert.Throws<ArgumentException>(() => em.Gather<int?>(new int?[3], new int?[2]));
        var same = new int?[3];
        Assert.Throws<ArgumentException>(() => em.Gather<int?>(same, same));
        var newValues = new int?[3];
        em.Gather<int?>([5, 6, 7], newValues);
        Assert.Equal([7, null, 6], newValues);
    }

    [Fact]
    public void GatherOfObjectsLeavesAnEdgeWithoutCounterpartAsItIs()
    {
        // The new graph has 0 -> 1 and 1 -> 0, the old graph only 0 -> 1: the new edge 1 -> 0
        // (edge index 1) has no counterpart. Objects go through the Gather that writes into the
        // caller's array, as in Java; the allocating one takes unmanaged value types only.
        Graph oldGraph = TestGraphs.Of(2, 0, 1);
        Graph newGraph = TestGraphs.Of(2, 0, 1, 1, 0);
        EdgeMapping newToOld = Graphs.EdgeMapping(newGraph, oldGraph, NodeMapping.ForPermutation([0, 1]));
        Assert.True(newToOld.IsPartial);
        Assert.Equal(-1, newToOld.Get(1));
        string[] newNames = ["before", "before"];
        newToOld.Gather<string>(["call"], newNames);
        Assert.Equal(["call", "before"], newNames);
    }
}
