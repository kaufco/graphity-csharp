namespace Graphity.ControlFlow.Tests;

/// <summary>
/// Cross-validates <see cref="ControlFlow.DominanceFrontiers"/> against <see cref="NaiveDominators"/>,
/// which decides dominance by removing a node and checking reachability. Inputs are control-flow
/// shapes with a single entry and arbitrary random graphs with up to three random entries,
/// duplicates and the empty list included.
/// </summary>
public sealed class ControlFlowFuzzTests
{
    private const int Iterations = 120;

    private sealed record Case(string Describe, Graph Graph, int[] Entries);

    private static Case CaseFor(long seed)
    {
        if (seed % 2 == 0)
        {
            var cfg = ControlFlowGraphs.AnyControlFlowGraph(seed);
            return new Case(cfg.Describe(), cfg.Graph, [0]);
        }
        var labeled = RandomGraphs.AnyGraph(seed);
        int n = labeled.Graph.NodeCount;
        var rnd = new JavaRandom((seed * 17) + 5);
        var entries = new List<int>();
        int count = rnd.NextInt(10) == 0 ? 0 : 1 + rnd.NextInt(3);
        for (int i = 0; i < count; i++)
        {
            entries.Add(rnd.NextInt(n));
        }
        return new Case($"{labeled.Describe()}, entries=[{string.Join(", ", entries)}]", labeled.Graph, [.. entries]);
    }

    [Fact]
    public void DominanceFrontiersMatchTheDefinition()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var c = CaseFor(seed);
            int n = c.Graph.NodeCount;
            bool[][] expected = new NaiveDominators(c.Graph, c.Entries).Frontiers();
            var df = ControlFlow.DominanceFrontiers(Dominators.Of(BiAdjacentGraph.Of(c.Graph), c.Entries));
            for (int x = 0; x < n; x++)
            {
                for (int m = 0; m < n; m++)
                {
                    Assert.True(df.HasOutNode(x, m) == expected[x][m], $"{m} in DF({x}), {c.Describe}");
                }
            }
        }
    }
}
