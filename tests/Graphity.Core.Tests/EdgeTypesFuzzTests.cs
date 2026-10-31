namespace Graphity.Core.Tests;

/// <summary>Cross-validates <see cref="EdgeTypes.Of"/> against the independent <see cref="NaiveEdgeClassifier"/> on random graphs.</summary>
public sealed class EdgeTypesFuzzTests
{
    private const int Iterations = 100;
    private const long BaseSeed = 0xFEEDFACEL;

    [Fact]
    public void EdgeTypesMatchNaiveClassifier()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            var theirs = EdgeTypes.Of(g);
            var expected = NaiveEdgeClassifier.Classify(g);
            for (int ei = 0; ei < g.EdgeCount; ei++)
            {
                Assert.True(theirs.TypeOf(ei) == expected[ei],
                    $"edge {ei} (source={g.EdgeSource(ei)}, target={g.EdgeTarget(ei)}) classification mismatch for {labeled.Describe()}");
            }
        }
    }

    [Fact]
    public void TypeCountsSumToEdgeCount()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            var et = EdgeTypes.Of(g);
            int total = 0;
            foreach (EdgeType t in Enum.GetValues<EdgeType>())
            {
                total += Edges.Of(et, t).Count;
            }
            Assert.True(total == g.EdgeCount, $"sum of type counts must equal edge count for {labeled.Describe()}");
        }
    }

    [Fact]
    public void EdgesOfAgreeWithTypeOf()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            var et = EdgeTypes.Of(g);
            var byType = new Dictionary<EdgeType, HashSet<long>>();
            foreach (EdgeType t in Enum.GetValues<EdgeType>())
            {
                byType[t] = [];
            }
            for (int u = 0; u < g.NodeCount; u++)
            {
                for (int ei = g.OutEdgeStart(u); ei < g.OutEdgeEnd(u); ei++)
                {
                    byType[et.TypeOf(ei)].Add(Pair(u, g.EdgeTarget(ei)));
                }
            }
            foreach (EdgeType t in Enum.GetValues<EdgeType>())
            {
                var edges = Edges.Of(et, t);
                var collected = new HashSet<long>();
                for (int k = 0; k < edges.Count; k++)
                {
                    collected.Add(Pair(edges.SourceNodes[k], edges.TargetNodes[k]));
                }
                Assert.True(collected.SetEquals(byType[t]), $"Edges.Of({t}) mismatches TypeOf scan for {labeled.Describe()}");
            }
        }
    }

    [Fact]
    public void BackEdgesSubsetMatchesBackEdgesClass()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            var fromEdgeTypes = Edges.Of(EdgeTypes.Of(g), EdgeType.Back);
            var fromBackEdges = BackEdges.Of(g);
            Assert.True(ToPairSet(fromEdgeTypes).SetEquals(ToPairSet(fromBackEdges)),
                $"EdgeTypes back edges must match BackEdges.Of for {labeled.Describe()}");
        }
    }

    private static HashSet<long> ToPairSet(Edges edges)
    {
        var set = new HashSet<long>();
        for (int i = 0; i < edges.Count; i++)
        {
            set.Add(Pair(edges.SourceNodes[i], edges.TargetNodes[i]));
        }
        return set;
    }

    private static long Pair(int src, int tgt) => ((long)src << 32) | (uint)tgt;
}
