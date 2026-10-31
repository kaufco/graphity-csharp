namespace Graphity.Core.Tests;

/// <summary>
/// Cross-validates <see cref="Dominators"/>, <see cref="Graphs.DominatorTree"/> and
/// <see cref="Ancestry.Of(Dominators)"/> against <see cref="NaiveDominators"/>, which decides
/// dominance by removing a node and checking reachability. Inputs are control-flow shapes with a
/// single entry and arbitrary random graphs with up to three random entries, duplicates and the
/// empty list included.
/// </summary>
public sealed class DominatorsFuzzTests
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
    public void ImmediateDominatorsMatchTheOracle()
    {
        int nontrivial = 0;
        for (long seed = 0; seed < Iterations; seed++)
        {
            var c = CaseFor(seed);
            var oracle = new NaiveDominators(c.Graph, c.Entries);
            var dominators = Dominators.Of(BiAdjacentGraph.Of(c.Graph), c.Entries);
            for (int node = 0; node < c.Graph.NodeCount; node++)
            {
                int expected = oracle.ImmediateDominatorOf(node);
                if (expected >= 0 && expected != 0)
                {
                    nontrivial++;
                }
                int actual = dominators.ImmediateDominatorOf(node);
                Assert.True(actual == expected, $"idom({node}) = {actual}, expected {expected}, {c.Describe}");
                Assert.True(dominators.IsReachable(node) == oracle.Reachable[node], $"reachable({node}), {c.Describe}");
            }
        }
        // Guards against a vacuous run: sparse random graphs reach little from their entries.
        Assert.True(nontrivial > 3000, $"nodes with an immediate dominator other than node 0: {nontrivial}");
    }

    [Fact]
    public void DominatorTreeAndAncestryAnswerDominance()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var c = CaseFor(seed);
            int n = c.Graph.NodeCount;
            var oracle = new NaiveDominators(c.Graph, c.Entries);
            var dominators = Dominators.Of(BiAdjacentGraph.Of(c.Graph), c.Entries);

            var tree = Graphs.DominatorTree(dominators);
            int treeEdges = 0;
            for (int node = 0; node < n; node++)
            {
                int idom = dominators.ImmediateDominatorOf(node);
                if (idom >= 0)
                {
                    treeEdges++;
                    Assert.True(tree.HasOutNode(idom, node), $"tree edge to {node}, {c.Describe}");
                }
            }
            Assert.True(tree.EdgeCount == treeEdges, $"tree edges, {c.Describe}");

            var index = Ancestry.Of(dominators);
            var preorder = PreorderAncestry.Of(index);
            var rnd = new JavaRandom(seed);
            for (int q = 0; q < 400; q++)
            {
                int a = rnd.NextInt(n);
                int b = rnd.NextInt(n);
                bool expected = oracle.Reachable[b] ? oracle.Dominates[a][b] : a == b;
                Assert.True(index.IsAncestorOf(a, b) == expected, $"{a} dominates {b}, {c.Describe}");
                Assert.True(preorder.IsAncestorOf(a, b) == expected, $"{a} dominates {b} (preorder), {c.Describe}");
            }
        }
    }
}
