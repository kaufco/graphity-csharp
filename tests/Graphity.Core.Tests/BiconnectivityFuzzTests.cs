namespace Graphity.Core.Tests;

/// <summary>
/// Cross-validates <see cref="Components.BiconnectivityOf"/> against the naive oracles of
/// <see cref="NaiveOracles"/> (removal tests and the local block relation, in place of JGraphT's
/// <c>BiconnectivityInspector</c>) on the same graph read as undirected, and against invariants
/// that hold independently of any implementation.
/// </summary>
/// <remarks>
/// Every graph is checked twice: once as generated, where an undirected edge is usually stored in
/// one direction only, and once symmetrized, where both directions are present.
/// </remarks>
public sealed class BiconnectivityFuzzTests
{
    private const int Iterations = 100;

    private static Biconnectivity BiconnectivityOf(Graph graph) => Components.BiconnectivityOf(BiAdjacentGraph.Of(graph));

    [Fact]
    public void BridgesMatchTheOracle()
    {
        ForEachGraph((description, graph) =>
        {
            var expected = NaiveOracles.Bridges(graph);
            var actual = BridgeKeys(BiconnectivityOf(graph));
            Assert.True(expected.SetEquals(actual), $"bridges for {description}");
        });
    }

    [Fact]
    public void ArticulationPointsMatchTheOracle()
    {
        ForEachGraph((description, graph) =>
        {
            var expected = NaiveOracles.CutPoints(graph);
            var actual = new SortedSet<int>(BiconnectivityOf(graph).ArticulationPoints.ToArray());
            Assert.True(expected.SetEquals(actual), $"articulation points for {description}");
        });
    }

    [Fact]
    public void BlocksMatchTheOracle()
    {
        ForEachGraph((description, graph) =>
        {
            var expected = NaiveOracles.Blocks(graph);
            var blocks = BiconnectivityOf(graph).Blocks;
            var actual = new HashSet<string>();
            for (int block = 0; block < blocks.GroupCount; block++)
            {
                var edges = new SortedSet<long>();
                foreach (int edgeIndex in blocks.EdgesOf(block))
                {
                    edges.Add(NaiveOracles.PairKey(graph.EdgeSource(edgeIndex), graph.EdgeTarget(edgeIndex)));
                }
                actual.Add(string.Join(",", edges));
            }
            Assert.True(expected.SetEquals(actual), $"blocks for {description}");
        });
    }

    [Fact]
    public void BlocksCoverEveryNonSelfLoopEdgeExactlyOnce()
    {
        ForEachGraph((description, graph) =>
        {
            var blocks = BiconnectivityOf(graph).Blocks;
            int selfLoops = 0;
            for (int edgeIndex = 0; edgeIndex < graph.EdgeCount; edgeIndex++)
            {
                bool selfLoop = graph.EdgeSource(edgeIndex) == graph.EdgeTarget(edgeIndex);
                if (selfLoop)
                {
                    selfLoops++;
                    Assert.True(blocks.Get(edgeIndex) == -1, $"self-loop {edgeIndex} is unmapped for {description}");
                }
                else
                {
                    int block = blocks.Get(edgeIndex);
                    Assert.True(block >= 0 && block < blocks.GroupCount, $"edge {edgeIndex} belongs to a block for {description}");
                }
            }
            Assert.True(blocks.MappedCount == graph.EdgeCount - selfLoops, $"mapped edge count for {description}");
            Assert.True(blocks.IsPartial == (selfLoops > 0), $"partial exactly with self-loops for {description}");

            int members = 0;
            for (int block = 0; block < blocks.GroupCount; block++)
            {
                members += blocks.SizeOf(block);
            }
            Assert.True(members == blocks.MappedCount, $"block members sum for {description}");
        });
    }

    [Fact]
    public void ReciprocalPairsShareABlockAndBridgesStandAlone()
    {
        ForEachGraph((description, graph) =>
        {
            var biconnectivity = BiconnectivityOf(graph);
            var blocks = biconnectivity.Blocks;
            var biAdjacent = BiAdjacentGraph.Of(graph);

            for (int edgeIndex = 0; edgeIndex < graph.EdgeCount; edgeIndex++)
            {
                int source = graph.EdgeSource(edgeIndex);
                int target = graph.EdgeTarget(edgeIndex);
                if (source == target || !biAdjacent.HasOutNode(target, source))
                {
                    continue;
                }
                int reverse = EdgeIndexOf(graph, target, source);
                Assert.True(blocks.Get(reverse) == blocks.Get(edgeIndex),
                    $"reciprocal pair {edgeIndex}/{reverse} for {description}");
            }

            var bridges = biconnectivity.Bridges;
            for (int p = 0; p < bridges.Count; p++)
            {
                int source = bridges.SourceNodes[p];
                int target = bridges.TargetNodes[p];
                int edgeIndex = bridges.EdgeIndices[p];
                Assert.True(graph.EdgeIndexOf(source, target) == edgeIndex, $"bridge edge index for {description}");
                if (source > target)
                {
                    Assert.False(graph.HasOutNode(target, source),
                        $"a bridge stored both ways comes from the lower node, {description}");
                }
                int block = blocks.Get(edgeIndex);
                foreach (int other in blocks.EdgesOf(block))
                {
                    Assert.True(
                        NaiveOracles.PairKey(graph.EdgeSource(other), graph.EdgeTarget(other)) == NaiveOracles.PairKey(source, target),
                        $"bridge block holds no other edge for {description}");
                }
            }
        });
    }

    /// <summary>
    /// Guards the corpus itself: a run in which every graph happens to have no bridge, no
    /// articulation point and no block larger than one edge would agree with any oracle.
    /// </summary>
    [Fact]
    public void TheCorpusExercisesAllThreeResults()
    {
        int[] totals = new int[3];
        ForEachGraph((_, graph) =>
        {
            var biconnectivity = BiconnectivityOf(graph);
            totals[0] += biconnectivity.Bridges.Count;
            totals[1] += biconnectivity.ArticulationPoints.Count;
            var blocks = biconnectivity.Blocks;
            for (int block = 0; block < blocks.GroupCount; block++)
            {
                if (blocks.SizeOf(block) > 2)
                {
                    totals[2]++;
                }
            }
        });
        Assert.True(totals[0] > 100, $"bridges in the corpus: {totals[0]}");
        Assert.True(totals[1] > 100, $"articulation points in the corpus: {totals[1]}");
        Assert.True(totals[2] > 100, $"blocks of more than one undirected edge: {totals[2]}");
    }

    /// <summary>Runs the check on each random graph, once as generated and once with both directions.</summary>
    private static void ForEachGraph(Action<string, Graph> check)
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyGraph(seed);
            check(labeled.Describe(), labeled.Graph);
            check(labeled.Describe() + ", symmetrized", RandomGraphs.Symmetrized(labeled.Graph));
        }
    }

    private static HashSet<long> BridgeKeys(Biconnectivity biconnectivity)
    {
        var keys = new HashSet<long>();
        var bridges = biconnectivity.Bridges;
        for (int p = 0; p < bridges.Count; p++)
        {
            keys.Add(NaiveOracles.PairKey(bridges.SourceNodes[p], bridges.TargetNodes[p]));
        }
        return keys;
    }

    /// <summary>Edge index of <c>source → target</c> via binary search, or -1.</summary>
    private static int EdgeIndexOf(Graph graph, int source, int target)
    {
        int position = graph.OutNodes(source).IndexOf(target);
        return position < 0 ? -1 : graph.OutEdgeStart(source) + position;
    }
}
