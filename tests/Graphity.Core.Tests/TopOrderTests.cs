using System.Collections.Immutable;

namespace Graphity.Core.Tests;

/// <remarks>
/// Java's <c>ofReturnsUnmodifiableList</c> (an <c>UnsupportedOperationException</c> on <c>set</c>) has
/// no counterpart: the result is an <see cref="ImmutableArray{T}"/>, which has no mutator.
/// </remarks>
public sealed class TopOrderTests
{
    [Fact]
    public void OfEmptyGraphIsEmpty() => Assert.True(TopOrder.Of(TestGraphs.Empty()).IsEmpty);

    [Fact]
    public void OfIsolatedNodesContainsAllNodes()
    {
        var order = TopOrder.Of(TestGraphs.Isolated(3));
        Assert.Equal(3, order.Length);
        Assert.Equal(new[] { 0, 1, 2 }, order.Order().ToArray());
    }

    [Fact]
    public void OfPathReturnsNodesInForwardOrder() =>
        Assert.Equal([0, 1, 2, 3, 4], TopOrder.Of(TestGraphs.Path(5)).ToArray());

    [Fact]
    public void OfDiamondDagPlacesSourceBeforeAllSuccessors()
    {
        // 0 -> 1, 0 -> 2, 1 -> 3, 2 -> 3.
        var g = TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3);
        var order = TopOrder.Of(g);
        AssertInvariantSourceBeforeTarget(g, order);
        Assert.Equal(0, order[0]);
        Assert.Equal(3, order[3]);
    }

    [Fact]
    public void OfBinaryTreePlacesRootFirst()
    {
        // 0 -> 1, 0 -> 2; 1 -> 3, 1 -> 4; 2 -> 5, 2 -> 6.
        var g = TestGraphs.Of(7, 0, 1, 0, 2, 1, 3, 1, 4, 2, 5, 2, 6);
        var order = TopOrder.Of(g);
        Assert.Equal(0, order[0]);
        AssertInvariantSourceBeforeTarget(g, order);
    }

    [Fact]
    public void OfDisconnectedDagsIncludesAllNodes()
    {
        // {0 -> 1} and {2 -> 3 -> 4}.
        var g = TestGraphs.Of(5, 0, 1, 2, 3, 3, 4);
        var order = TopOrder.Of(g);
        Assert.Equal(5, order.Length);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, order.Order().ToArray());
        AssertInvariantSourceBeforeTarget(g, order);
    }

    [Fact]
    public void OfGraphWithCycleThrows()
    {
        var e = Assert.Throws<ArgumentException>(() => TopOrder.Of(TestGraphs.Cycle(3)));
        Assert.StartsWith("graph contains a cycle", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OfGraphWithSelfLoopThrows() =>
        Assert.Throws<ArgumentException>(() => TopOrder.Of(TestGraphs.Of(2, 0, 0, 0, 1)));

    [Fact]
    public void OfGraphWithCycleInSubcomponentThrows()
    {
        // DAG component + cycle component.
        var g = TestGraphs.Of(5,
            0, 1,
            2, 3, 3, 4, 4, 2);
        Assert.Throws<ArgumentException>(() => TopOrder.Of(g));
    }

    /// <summary>For every edge u -> v, u precedes v in the order.</summary>
    private static void AssertInvariantSourceBeforeTarget(Graph graph, ImmutableArray<int> order)
    {
        var position = new int[graph.NodeCount];
        for (int i = 0; i < order.Length; i++)
        {
            position[order[i]] = i;
        }
        for (int u = 0; u < graph.NodeCount; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                Assert.True(position[u] < position[v],
                    $"edge {u} -> {v}: position[{u}]={position[u]} must precede position[{v}]={position[v]}");
            }
        }
    }
}
