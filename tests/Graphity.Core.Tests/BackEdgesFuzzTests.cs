namespace Graphity.Core.Tests;

/// <summary>
/// Cross-validates <see cref="BackEdges.Of"/> against the independent <see cref="NaiveEdgeClassifier"/>
/// and against the invariant that removing all back edges yields a DAG.
/// </summary>
public sealed class BackEdgesFuzzTests
{
    private const int Iterations = 100;
    private const long BaseSeed = 0xBEEF0BEEFL;

    [Fact]
    public void BackEdgesMatchNaiveClassifierOnRandomGraphs()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            Assert.True(NaiveBackEdgePairs(g).SetEquals(Pairs(BackEdges.Of(g))),
                $"back edges mismatch vs naive oracle for {labeled.Describe()}");
        }
    }

    [Fact]
    public void RemovingBackEdgesYieldsDagOnRandomGraphs()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            var reduced = RemoveBackEdges(g, BackEdges.Of(g));
            Assert.True(Graphs.IsDag(reduced), $"removing back edges must yield a DAG for {labeled.Describe()}");
        }
    }

    [Fact]
    public void BackEdgeCountZeroIffDag()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            bool isDag = Graphs.IsDag(g);
            bool noBackEdges = BackEdges.Of(g).Count == 0;
            Assert.True(noBackEdges == isDag, $"no back edges must be equivalent to IsDag for {labeled.Describe()}");
        }
    }

    private static HashSet<long> NaiveBackEdgePairs(Graph graph)
    {
        var types = NaiveEdgeClassifier.Classify(graph);
        var pairs = new HashSet<long>();
        for (int u = 0; u < graph.NodeCount; u++)
        {
            for (int ei = graph.OutEdgeStart(u); ei < graph.OutEdgeEnd(u); ei++)
            {
                if (types[ei] == EdgeType.Back)
                {
                    pairs.Add(Pair(u, graph.EdgeTarget(ei)));
                }
            }
        }
        return pairs;
    }

    private static HashSet<long> Pairs(Edges edges)
    {
        var pairs = new HashSet<long>();
        for (int i = 0; i < edges.Count; i++)
        {
            pairs.Add(Pair(edges.SourceNodes[i], edges.TargetNodes[i]));
        }
        return pairs;
    }

    private static long Pair(int src, int tgt) => ((long)src << 32) | (uint)tgt;

    private static Graph RemoveBackEdges(Graph graph, Edges backEdges)
    {
        var backSet = Pairs(backEdges);
        var builder = new GraphBuilder();
        builder.AddNodes(graph.NodeCount);
        for (int u = 0; u < graph.NodeCount; u++)
        {
            for (int ei = graph.OutEdgeStart(u); ei < graph.OutEdgeEnd(u); ei++)
            {
                int v = graph.EdgeTarget(ei);
                if (!backSet.Contains(Pair(u, v)))
                {
                    builder.AddEdge(u, v);
                }
            }
        }
        return builder.Build();
    }
}
