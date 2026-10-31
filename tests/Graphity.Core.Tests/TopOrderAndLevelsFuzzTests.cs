namespace Graphity.Core.Tests;

/// <summary>
/// Fuzz-tests <see cref="TopOrderAndLevels.Of"/> on random DAGs; the levels are computed
/// independently (Kahn via the maximum over the predecessors) and compared.
/// </summary>
public sealed class TopOrderAndLevelsFuzzTests
{
    private const int Iterations = 100;
    private const long BaseSeed = 0xDECAFBADL;

    [Fact]
    public void TopOrderAndLevelsAgreeWithReferenceOnDags()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyDag(BaseSeed + i);
            var g = labeled.Graph;
            var tol = TopOrderAndLevels.Of(g);

            Assert.True(tol.Order.Length == g.NodeCount, $"order size mismatch for {labeled.Describe()}");
            AssertTopologicalInvariant(labeled, g, tol);
            AssertLevelInvariant(labeled, g, tol);

            int[] reference = ReferenceLevels(g);
            for (int v = 0; v < g.NodeCount; v++)
            {
                Assert.True(tol.LevelOf(v) == reference[v], $"level({v}) mismatch for {labeled.Describe()}");
            }
        }
    }

    private static void AssertTopologicalInvariant(LabeledGraph labeled, Graph graph, TopOrderAndLevels tol)
    {
        var position = new int[graph.NodeCount];
        for (int i = 0; i < tol.Order.Length; i++)
        {
            position[tol.Order[i]] = i;
        }
        for (int u = 0; u < graph.NodeCount; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                Assert.True(position[u] < position[v], $"edge {u} -> {v} violates topological order for {labeled.Describe()}");
            }
        }
    }

    private static void AssertLevelInvariant(LabeledGraph labeled, Graph graph, TopOrderAndLevels tol)
    {
        for (int u = 0; u < graph.NodeCount; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                Assert.True(tol.LevelOf(v) > tol.LevelOf(u),
                    $"edge {u} (level {tol.LevelOf(u)}) -> {v} (level {tol.LevelOf(v)}) violates level invariant for {labeled.Describe()}");
            }
        }
    }

    /// <summary>Independent Kahn: level(v) = max(level(predecessors)) + 1, or 0 without predecessors.</summary>
    private static int[] ReferenceLevels(Graph graph)
    {
        int n = graph.NodeCount;
        var inDegree = new int[n];
        var predMaxLevel = new int[n];
        var levels = new int[n];
        var queue = new int[n];
        int head = 0;
        int tail = 0;
        for (int u = 0; u < n; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                inDegree[v]++;
            }
        }
        for (int v = 0; v < n; v++)
        {
            if (inDegree[v] == 0)
            {
                queue[tail++] = v;
                levels[v] = 0;
            }
            else
            {
                predMaxLevel[v] = -1; // no predecessor seen yet
            }
        }
        while (head < tail)
        {
            int u = queue[head++];
            foreach (int v in graph.OutNodes(u))
            {
                if (levels[u] > predMaxLevel[v])
                {
                    predMaxLevel[v] = levels[u];
                }
                if (--inDegree[v] == 0)
                {
                    levels[v] = predMaxLevel[v] + 1;
                    queue[tail++] = v;
                }
            }
        }
        return levels;
    }
}
