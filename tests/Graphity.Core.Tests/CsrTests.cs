using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Graphity.Core.Tests;

/// <summary>
/// The C# <see cref="Csr"/> returns read-only spans over the graph's own arrays; "same array" is
/// checked as "same memory and length".
/// </summary>
public sealed class CsrTests
{
    private static readonly Graph TestGraph = TestGraphs.Of(4, 0, 1, 0, 2, 1, 2, 2, 0, 3, 3);

    private static bool SameMemory(ReadOnlySpan<int> a, ReadOnlySpan<int> b) =>
        a.Length == b.Length && Unsafe.AreSame(ref MemoryMarshal.GetReference(a), ref MemoryMarshal.GetReference(b));

    [Fact]
    public void OutArraysMatchTheEdgeIndexMethods()
    {
        ReadOnlySpan<int> start = Csr.OutAdjacencyStart(TestGraph);
        ReadOnlySpan<int> targets = Csr.OutAdjacency(TestGraph);

        Assert.Equal(TestGraph.NodeCount + 1, start.Length);
        Assert.Equal(TestGraph.EdgeCount, targets.Length);
        for (int v = 0; v < TestGraph.NodeCount; v++)
        {
            Assert.Equal(TestGraph.OutEdgeStart(v), start[v]);
            Assert.Equal(TestGraph.OutEdgeEnd(v), start[v + 1]);
        }
        for (int e = 0; e < TestGraph.EdgeCount; e++)
        {
            Assert.Equal(TestGraph.EdgeTarget(e), targets[e]);
        }
    }

    [Fact]
    public void InArraysMatchTheInNodes()
    {
        var graph = BiAdjacentGraph.Of(TestGraph);
        ReadOnlySpan<int> start = Csr.InAdjacencyStart(graph);
        ReadOnlySpan<int> sources = Csr.InAdjacency(graph);

        Assert.Equal(graph.NodeCount + 1, start.Length);
        Assert.Equal(graph.EdgeCount, sources.Length);
        for (int v = 0; v < graph.NodeCount; v++)
        {
            Assert.Equal(graph.InNodes(v).ToArray(), sources[start[v]..start[v + 1]].ToArray());
        }
    }

    [Fact]
    public void ReturnsTheGraphsOwnArraysWithoutCopying()
    {
        int[] outStart = [0, 1, 1, 2];
        int[] outAdj = [2, 0];
        int[] inStart = [0, 1, 1, 2];
        int[] inAdj = [2, 0];
        var graph = new BiAdjacentGraph(outStart, outAdj, inStart, inAdj);

        Assert.True(SameMemory(Csr.OutAdjacencyStart(graph), outStart));
        Assert.True(SameMemory(Csr.OutAdjacency(graph), outAdj));
        Assert.True(SameMemory(Csr.InAdjacencyStart(graph), inStart));
        Assert.True(SameMemory(Csr.InAdjacency(graph), inAdj));
    }

    [Fact]
    public void NodeWithoutEdgesInTheMiddleHasAnEmptyRange()
    {
        var graph = BiAdjacentGraph.Of(TestGraphs.Of(3, 0, 2, 2, 0));
        Assert.Equal([0, 1, 1, 2], Csr.OutAdjacencyStart(graph).ToArray());
        Assert.Equal([2, 0], Csr.OutAdjacency(graph).ToArray());
        Assert.Equal([0, 1, 1, 2], Csr.InAdjacencyStart(graph).ToArray());
        Assert.Equal([2, 0], Csr.InAdjacency(graph).ToArray());
    }

    [Fact]
    public void TransposedViewSwapsTheArrays()
    {
        var graph = BiAdjacentGraph.Of(TestGraph);
        var transposed = graph.Transposed();

        Assert.True(SameMemory(Csr.OutAdjacencyStart(transposed), Csr.InAdjacencyStart(graph)));
        Assert.True(SameMemory(Csr.OutAdjacency(transposed), Csr.InAdjacency(graph)));
        Assert.True(SameMemory(Csr.InAdjacencyStart(transposed), Csr.OutAdjacencyStart(graph)));
        Assert.True(SameMemory(Csr.InAdjacency(transposed), Csr.OutAdjacency(graph)));
    }

    [Fact]
    public void EmptyGraphHasOneOffset()
    {
        Assert.Equal([0], Csr.OutAdjacencyStart(TestGraphs.Empty()).ToArray());
        Assert.True(Csr.OutAdjacency(TestGraphs.Empty()).IsEmpty);
    }
}
