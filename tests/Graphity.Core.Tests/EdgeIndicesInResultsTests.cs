namespace Graphity.Core.Tests;

/// <summary>
/// Every result that lists edges names each by its edge index, and that index is the edge at the
/// same position: same source, same target.
/// </summary>
public sealed class EdgeIndicesInResultsTests
{
    private const int Iterations = 200;

    private static void AssertConsistent(Graph graph, Edges edges, string describe)
    {
        Assert.True(edges.EdgeIndices.Length == edges.Count, describe);
        for (int p = 0; p < edges.Count; p++)
        {
            int source = edges.SourceNodes[p];
            int target = edges.TargetNodes[p];
            int edgeIndex = edges.EdgeIndices[p];
            Assert.True(edgeIndex >= 0 && edgeIndex == graph.EdgeIndexOf(source, target), $"{describe}, position {p}");
        }
    }

    private static void AssertAscending(Edges edges, string describe)
    {
        for (int p = 1; p < edges.Count; p++)
        {
            Assert.True(edges.EdgeIndices[p] > edges.EdgeIndices[p - 1], describe);
        }
    }

    [Fact]
    public void EveryProducerReportsTheEdgeAtEachPosition()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyGraph(seed);
            Graph graph = labeled.Graph;
            var bi = BiAdjacentGraph.Of(graph);
            string describe = labeled.Describe();

            AssertConsistent(graph, BackEdges.Of(graph), "back edges, " + describe);

            var types = EdgeTypes.Of(graph);
            foreach (EdgeType type in Enum.GetValues<EdgeType>())
            {
                var edges = Edges.Of(types, type);
                AssertConsistent(graph, edges, type + ", " + describe);
                AssertAscending(edges, type + ", " + describe);
            }

            var bridges = Components.BiconnectivityOf(bi).Bridges;
            AssertConsistent(graph, bridges, "bridges, " + describe);
            AssertAscending(bridges, "bridges, " + describe);

            var strongBridges = Components.StrongConnectivityOf(bi).StrongBridges;
            AssertConsistent(graph, strongBridges, "strong bridges, " + describe);
            AssertAscending(strongBridges, "strong bridges, " + describe);
        }
    }

    [Fact]
    public void BackEdgeIndicesReachEdgeData()
    {
        // 0 -> 1 -> 2 -> 0, 2 -> 1: two back edges.
        Graph graph = TestGraphs.Of(3, 0, 1, 1, 2, 2, 0, 2, 1);
        double[] weights = [1.0, 2.0, 3.0, 4.0]; // by edge index: 0->1, 1->2, 2->0, 2->1
        var back = BackEdges.Of(graph);
        double total = 0;
        for (int p = 0; p < back.Count; p++)
        {
            total += weights[back.EdgeIndices[p]];
        }
        Assert.Equal(3.0 + 4.0, total);
    }

    [Fact]
    public void ABridgeStoredInBothDirectionsIsReportedFromTheLowerNode()
    {
        // 0 <-> 1 is a bridge stored twice; 1 -> 2 -> 3 -> 1 is a cycle.
        var graph = BiAdjacentGraph.Of(TestGraphs.Of(4, 0, 1, 1, 0, 1, 2, 2, 3, 3, 1));
        var bridges = Components.BiconnectivityOf(graph).Bridges;
        Assert.Equal(1, bridges.Count);
        Assert.Equal(0, bridges.SourceNodes[0]);
        Assert.Equal(1, bridges.TargetNodes[0]);
        Assert.Equal(graph.EdgeIndexOf(0, 1), bridges.EdgeIndices[0]);
    }

    [Fact]
    public void ABridgeStoredOnlyFromTheHigherNodeKeepsItsDirection()
    {
        // 2 -> 0 and 2 -> 1: both bridges, both stored from the higher node.
        var graph = BiAdjacentGraph.Of(TestGraphs.Of(3, 2, 0, 2, 1));
        var bridges = Components.BiconnectivityOf(graph).Bridges;
        Assert.Equal([2, 2], bridges.SourceNodes.ToArray());
        Assert.Equal([0, 1], bridges.TargetNodes.ToArray());
        Assert.Equal([0, 1], bridges.EdgeIndices.ToArray());
    }
}
