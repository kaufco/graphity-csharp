using System.Runtime.InteropServices;

namespace Graphity.Core.Tests;

public sealed class GraphOfTests
{
    [Fact]
    public void BuildsTheGraphTheArraysDescribe()
    {
        Graph graph = Graph.Of([0, 2, 2, 3], [1, 2, 0]);
        Assert.Equal(3, graph.NodeCount);
        Assert.Equal(3, graph.EdgeCount);
        Assert.Equal([1, 2], graph.OutNodes(0).ToArray());
        Assert.Empty(graph.OutNodes(1).ToArray());
        Assert.Equal([0], graph.OutNodes(2).ToArray());
        Assert.Equal(0, graph.EdgeTarget(2));
    }

    [Fact]
    public void EmptyGraph()
    {
        Graph graph = Graph.Of([0], []);
        Assert.Equal(0, graph.NodeCount);
        Assert.Equal(0, graph.EdgeCount);
    }

    [Fact]
    public void SelfLoopsAreAllowed()
    {
        Graph graph = Graph.Of([0, 2, 3], [0, 1, 1]);
        Assert.Equal([0, 1], graph.OutNodes(0).ToArray());
        Assert.Equal([1], graph.OutNodes(1).ToArray());
    }

    [Fact]
    public void CopiesBothArrays()
    {
        int[] start = [0, 1, 1];
        int[] targets = [1];
        Graph graph = Graph.Of(start, targets);

        start[1] = 0;
        targets[0] = 0;

        Assert.False(Csr.OutAdjacencyStart(graph).Overlaps(start));
        Assert.Equal([0, 1, 1], Csr.OutAdjacencyStart(graph).ToArray());
        Assert.False(Csr.OutAdjacency(graph).Overlaps(targets));
        Assert.Equal([1], Csr.OutAdjacency(graph).ToArray());
    }

    [Fact]
    public void RoundTripsTheArraysOfAnyGraph()
    {
        for (long seed = 0; seed < 20; seed++)
        {
            Graph original = RandomGraphs.Gnp(50, 0.08, seed);
            Graph copy = Graph.Of(Csr.OutAdjacencyStart(original), Csr.OutAdjacency(original));
            Assert.True(Csr.OutAdjacencyStart(copy).SequenceEqual(Csr.OutAdjacencyStart(original)));
            Assert.True(Csr.OutAdjacency(copy).SequenceEqual(Csr.OutAdjacency(original)));
        }
    }

    [Fact]
    public void RejectsEmptyOffsets()
    {
        var ex = Assert.Throws<ArgumentException>(() => Graph.Of([], []));
        Assert.Contains("empty", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsOffsetsNotStartingAtZero()
    {
        var ex = Assert.Throws<ArgumentException>(() => Graph.Of([1, 1], [0]));
        Assert.Contains("outAdjacencyStart[0]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsOffsetsNotEndingAtTheTargetCount()
    {
        var ex = Assert.Throws<ArgumentException>(() => Graph.Of([0, 1], [0, 0]));
        Assert.Contains("outAdjacency.Length", ex.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => Graph.Of([0, 2], [0]));
    }

    [Fact]
    public void RejectsDecreasingOffsetsBeforeReadingPastTheTargets()
    {
        var ex = Assert.Throws<ArgumentException>(() => Graph.Of([0, 5, 3], [0, 1, 2]));
        Assert.Contains("decreases", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsTargetsOutOfRange()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Graph.Of([0, 1, 1], [2]));
        Assert.Contains("out-node 2 of node 0", ex.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => Graph.Of([0, 1, 1], [-1]));
    }

    [Fact]
    public void RejectsUnsortedAdjacency()
    {
        var ex = Assert.Throws<ArgumentException>(() => Graph.Of([0, 2, 2], [1, 0]));
        Assert.Contains("strictly ascending", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsDuplicateTargets()
    {
        var ex = Assert.Throws<ArgumentException>(() => Graph.Of([0, 2, 2], [1, 1]));
        Assert.Contains("strictly ascending", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SortednessIsCheckedPerListNotAcrossLists()
    {
        Graph graph = Graph.Of([0, 1, 2], [1, 0]);
        Assert.Equal([0], graph.OutNodes(1).ToArray());
    }

    [Fact]
    public void RejectsSpansBeyondTheNodeAndEdgeLimitsBeforeCopying()
    {
        // No array exceeds the limits, but a span over other memory may. Only the lengths are read
        // before the check, so these spans over a single int are never read past it; without the
        // check, the copy would fail to allocate before it reads.
        int[] one = [0];
        var nodes = Assert.Throws<ArgumentException>(
            () => Graph.Of(MemoryMarshal.CreateReadOnlySpan(ref one[0], ArrayLimits.MaxArrayLength + 1), []));
        Assert.Equal($"a graph holds at most {ArrayLimits.MaxNodeCount} nodes and {ArrayLimits.MaxArrayLength}"
            + $" edges, but these arrays hold {ArrayLimits.MaxArrayLength} and 0", nodes.Message);
        var edges = Assert.Throws<ArgumentException>(
            () => Graph.Of([0], MemoryMarshal.CreateReadOnlySpan(ref one[0], ArrayLimits.MaxArrayLength + 1)));
        Assert.Contains($"hold 0 and {ArrayLimits.MaxArrayLength + 1}", edges.Message, StringComparison.Ordinal);
    }
}
