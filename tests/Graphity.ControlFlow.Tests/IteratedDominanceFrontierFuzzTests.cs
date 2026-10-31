namespace Graphity.ControlFlow.Tests;

/// <summary>
/// Cross-validates <see cref="IteratedDominanceFrontier"/> against the fixpoint over the dominance
/// frontiers that <see cref="NaiveDominators"/> derives from the definition of dominance.
/// </summary>
public sealed class IteratedDominanceFrontierFuzzTests
{
    private const int Iterations = 100;
    private const int QueriesPerGraph = 20;

    [Fact]
    public void FrontierOfMatchesTheFixpoint()
    {
        int nonEmpty = 0;
        for (long seed = 0; seed < Iterations; seed++)
        {
            Graph graph;
            int[] entries;
            string describe;
            if (seed % 2 == 0)
            {
                var cfg = ControlFlowGraphs.AnyControlFlowGraph(seed);
                graph = cfg.Graph;
                entries = [0];
                describe = cfg.Describe();
            }
            else
            {
                var labeled = RandomGraphs.AnyGraph(seed);
                graph = labeled.Graph;
                var r = new JavaRandom(seed);
                int first = r.NextInt(graph.NodeCount);
                entries = [first, r.NextInt(graph.NodeCount)];
                describe = $"{labeled.Describe()}, entries=[{string.Join(", ", entries)}]";
            }
            int n = graph.NodeCount;
            var oracle = new NaiveDominators(graph, entries);
            bool[][] frontiers = oracle.Frontiers();
            var idf = IteratedDominanceFrontier.Of(Dominators.Of(BiAdjacentGraph.Of(graph), entries));

            var rnd = new JavaRandom((seed * 13) + 7);
            for (int q = 0; q < QueriesPerGraph; q++)
            {
                var nodes = new List<int>();
                int count = 1 + rnd.NextInt(Math.Min(8, n));
                for (int i = 0; i < count; i++)
                {
                    nodes.Add(rnd.NextInt(n));
                }

                bool[] expected = oracle.IteratedFrontier(frontiers, nodes.ToArray());
                var expectedList = new List<int>();
                for (int m = 0; m < n; m++)
                {
                    if (expected[m])
                    {
                        expectedList.Add(m);
                    }
                }
                if (expectedList.Count > 0)
                {
                    nonEmpty++;
                }
                var actual = idf.FrontierOf(nodes.ToArray());
                Assert.True(actual.AsSpan().SequenceEqual(expectedList.ToArray()),
                    $"IDF([{string.Join(", ", nodes)}]) = {actual}, expected [{string.Join(", ", expectedList)}], {describe}");
            }
        }
        Assert.True(nonEmpty > 300, $"queries with a non-empty frontier: {nonEmpty}");
    }
}
