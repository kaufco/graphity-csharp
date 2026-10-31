using System.Collections.Immutable;

namespace Graphity.Core.Tests;

/// <summary>
/// Validates <see cref="TopOrder.Of"/> on random DAGs. Java cross-checks against JGraphT's
/// topological order iterator; any valid order is acceptable, so the check here is the topological
/// invariant itself, plus the naive cycle oracle confirming the input is a DAG.
/// </summary>
public sealed class TopOrderFuzzTests
{
    private const int Iterations = 100;
    private const long BaseSeed = 0xBEEFFACEL;

    [Fact]
    public void TopOrderIsValidOnRandomDags()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyDag(BaseSeed + i);
            var g = labeled.Graph;
            Assert.False(NaiveOracles.HasCycle(g), $"generator produced a cycle for {labeled.Describe()}");

            var order = TopOrder.Of(g);
            Assert.True(order.Length == g.NodeCount, $"topological order must cover all nodes for {labeled.Describe()}");
            AssertValidTopologicalOrder(labeled, g, order);
        }
    }

    private static void AssertValidTopologicalOrder(LabeledGraph labeled, Graph graph, ImmutableArray<int> order)
    {
        var position = new int[graph.NodeCount];
        Array.Fill(position, -1);
        for (int i = 0; i < order.Length; i++)
        {
            Assert.True(position[order[i]] < 0, $"node {order[i]} twice in the order for {labeled.Describe()}");
            position[order[i]] = i;
        }
        for (int u = 0; u < graph.NodeCount; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                Assert.True(position[u] < position[v], $"edge {u} -> {v} violates topological order for {labeled.Describe()}");
            }
        }
    }
}
