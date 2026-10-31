using System.Globalization;

namespace Graphity.TestUtils;

/// <summary>A generator invocation tagged with its recipe, for reproducible failure messages.</summary>
public sealed record LabeledGraph(string Generator, int NodeCount, long Seed, Graph Graph)
{
    /// <summary>The recipe: generator, node count and seed.</summary>
    public string Describe() => $"gen={Generator}, n={NodeCount}, seed={Seed}";
}

/// <summary>
/// Seed-based random graph generators for fuzzing and cross-validation tests. Every generator is
/// deterministic given its seed and draws from <see cref="JavaRandom"/>, so it yields the same graph
/// as the Java test utilities for the same seed. Self-loops are allowed; parallel edges are merged
/// by the builder.
/// </summary>
public static class RandomGraphs
{
    /// <summary>Erdős–Rényi G(n, p): each of the n x n directed pairs with probability <paramref name="edgeProb"/>.</summary>
    public static Graph Gnp(int n, double edgeProb, long seed)
    {
        var rnd = new JavaRandom(seed);
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int u = 0; u < n; u++)
        {
            for (int v = 0; v < n; v++)
            {
                if (rnd.NextDouble() < edgeProb)
                {
                    b.AddEdge(u, v);
                }
            }
        }
        return b.Build();
    }

    /// <summary>Erdős–Rényi G(n, m): exactly <paramref name="m"/> random edges, with replacement.</summary>
    public static Graph Gnm(int n, int m, long seed)
    {
        if (n == 0)
        {
            return new GraphBuilder().Build();
        }
        var rnd = new JavaRandom(seed);
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int i = 0; i < m; i++)
        {
            b.AddEdge(rnd.NextInt(n), rnd.NextInt(n));
        }
        return b.Build();
    }

    /// <summary>Random out-tree rooted at 0: every node i &gt; 0 gets a random parent in <c>[0, i)</c>.</summary>
    public static Graph RandomTree(int n, long seed)
    {
        if (n == 0)
        {
            return new GraphBuilder().Build();
        }
        var rnd = new JavaRandom(seed);
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int i = 1; i < n; i++)
        {
            b.AddEdge(rnd.NextInt(i), i);
        }
        return b.Build();
    }

    /// <summary>Random forest of out-trees: every node i &gt; 0 starts a tree with probability <paramref name="rootProb"/>, else gets a random parent in <c>[0, i)</c>.</summary>
    public static Graph RandomForest(int n, double rootProb, long seed)
    {
        if (n == 0)
        {
            return new GraphBuilder().Build();
        }
        var rnd = new JavaRandom(seed);
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int i = 1; i < n; i++)
        {
            if (rnd.NextDouble() >= rootProb)
            {
                b.AddEdge(rnd.NextInt(i), i);
            }
        }
        return b.Build();
    }

    /// <summary>Forest of long chains: every node's parent is its predecessor, except where a new tree starts.</summary>
    public static Graph ChainForest(int n, double rootProb, long seed)
    {
        if (n == 0)
        {
            return new GraphBuilder().Build();
        }
        var rnd = new JavaRandom(seed);
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int i = 1; i < n; i++)
        {
            if (rnd.NextDouble() >= rootProb)
            {
                b.AddEdge(i - 1, i);
            }
        }
        return b.Build();
    }

    /// <summary>Random DAG: each forward pair <c>u &lt; v</c> with probability <paramref name="edgeProb"/>.</summary>
    public static Graph RandomDag(int n, double edgeProb, long seed)
    {
        var rnd = new JavaRandom(seed);
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int u = 0; u < n; u++)
        {
            for (int v = u + 1; v < n; v++)
            {
                if (rnd.NextDouble() < edgeProb)
                {
                    b.AddEdge(u, v);
                }
            }
        }
        return b.Build();
    }

    /// <summary>Random bipartite graph: two random classes, each cross pair joined with probability <paramref name="edgeProb"/> in a random direction.</summary>
    public static Graph RandomBipartite(int n, double edgeProb, long seed)
    {
        var rnd = new JavaRandom(seed);
        var b = new GraphBuilder();
        b.AddNodes(n);
        var secondClass = new bool[n];
        for (int i = 0; i < n; i++)
        {
            secondClass[i] = rnd.NextBoolean();
        }
        for (int u = 0; u < n; u++)
        {
            for (int v = u + 1; v < n; v++)
            {
                if (secondClass[u] != secondClass[v] && rnd.NextDouble() < edgeProb)
                {
                    if (rnd.NextBoolean())
                    {
                        b.AddEdge(u, v);
                    }
                    else
                    {
                        b.AddEdge(v, u);
                    }
                }
            }
        }
        return b.Build();
    }

    /// <summary>A copy of <paramref name="graph"/> with <paramref name="extraEdges"/> random edges added (duplicates, reversals and self-loops possible).</summary>
    public static Graph WithExtraEdges(Graph graph, int extraEdges, long seed)
    {
        int n = graph.NodeCount;
        var rnd = new JavaRandom(seed);
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int u = 0; u < n; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                b.AddEdge(u, v);
            }
        }
        for (int i = 0; i < extraEdges && n > 0; i++)
        {
            b.AddEdge(rnd.NextInt(n), rnd.NextInt(n));
        }
        return b.Build();
    }

    /// <summary>A copy of <paramref name="graph"/> with every edge stored in both directions.</summary>
    public static Graph Symmetrized(Graph graph)
    {
        var b = new GraphBuilder();
        b.AddNodes(graph.NodeCount);
        for (int u = 0; u < graph.NodeCount; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                b.AddEdge(u, v);
                b.AddEdge(v, u);
            }
        }
        return b.Build();
    }

    /// <summary>Node count uniformly in [10, 500].</summary>
    private static int PickNodeCount(JavaRandom rnd) => 10 + rnd.NextInt(500 - 10 + 1);

    /// <summary>Edge density favoring sparse graphs, in (0, 0.1].</summary>
    private static double PickEdgeProb(JavaRandom rnd)
    {
        double[] choices = [0.001, 0.002, 0.005, 0.01, 0.05, 0.1];
        return choices[rnd.NextInt(choices.Length)];
    }

    private static string F(double value, int digits) => value.ToString("F" + digits, CultureInfo.InvariantCulture);

    /// <summary>A graph from any of the four base generators, with random size and density.</summary>
    public static LabeledGraph AnyGraph(long seed)
    {
        var rnd = new JavaRandom(seed);
        int n = PickNodeCount(rnd);
        double p = PickEdgeProb(rnd);
        long innerSeed = rnd.NextLong();
        int choice = rnd.NextInt(4);
        switch (choice)
        {
            case 0:
                return new LabeledGraph($"gnp(p={F(p, 3)})", n, seed, Gnp(n, p, innerSeed));
            case 1:
                // Java's Math.round: half up.
                int m = (int)Math.Min((long)Math.Floor((p * (double)n * n) + 0.5), int.MaxValue);
                return new LabeledGraph($"gnm(m={m})", n, seed, Gnm(n, m, innerSeed));
            case 2:
                return new LabeledGraph("randomTree", n, seed, RandomTree(n, innerSeed));
            default:
                return new LabeledGraph($"randomDag(p={F(p, 3)})", n, seed, RandomDag(n, p, innerSeed));
        }
    }

    /// <summary>A random forest of out-trees, bushy or chain-like, one tree or several.</summary>
    public static LabeledGraph AnyForest(long seed)
    {
        var rnd = new JavaRandom(seed);
        int n = PickNodeCount(rnd);
        long innerSeed = rnd.NextLong();
        double rootProb = new[] { 0.0, 0.01, 0.1 }[rnd.NextInt(3)];
        int choice = rnd.NextInt(3);
        return choice switch
        {
            0 => new LabeledGraph("randomTree", n, seed, RandomTree(n, innerSeed)),
            1 => new LabeledGraph($"randomForest(rootProb={F(rootProb, 2)})", n, seed, RandomForest(n, rootProb, innerSeed)),
            _ => new LabeledGraph($"chainForest(rootProb={F(rootProb, 2)})", n, seed, ChainForest(n, rootProb, innerSeed)),
        };
    }

    /// <summary>
    /// A graph from the shapes on which the undirected predicates discriminate: bipartite graphs and
    /// graphs one edge away, trees and forests stored in one or both directions, and each with a few
    /// random edges added.
    /// </summary>
    public static LabeledGraph AnyUndirectedShape(long seed)
    {
        var rnd = new JavaRandom(seed);
        int n = PickNodeCount(rnd);
        double p = PickEdgeProb(rnd);
        long innerSeed = rnd.NextLong();
        int choice = rnd.NextInt(9);
        return choice switch
        {
            0 => new LabeledGraph($"randomBipartite(p={F(p, 3)})", n, seed, RandomBipartite(n, p, innerSeed)),
            1 => new LabeledGraph($"randomBipartite(p={F(p, 3)})+3", n, seed,
                WithExtraEdges(RandomBipartite(n, p, innerSeed), 3, innerSeed)),
            2 => new LabeledGraph("randomTree", n, seed, RandomTree(n, innerSeed)),
            3 => new LabeledGraph("randomTree+1", n, seed, WithExtraEdges(RandomTree(n, innerSeed), 1, innerSeed)),
            4 => new LabeledGraph("symmetrized(randomTree)", n, seed, Symmetrized(RandomTree(n, innerSeed))),
            5 => new LabeledGraph("randomForest(rootProb=0.05)", n, seed, RandomForest(n, 0.05, innerSeed)),
            6 => new LabeledGraph("symmetrized(randomForest(rootProb=0.05))", n, seed,
                Symmetrized(RandomForest(n, 0.05, innerSeed))),
            7 => new LabeledGraph("gnm(m=n)", n, seed, Gnm(n, n, innerSeed)),
            // A symmetrically stored graph that does hold an undirected cycle.
            _ => new LabeledGraph("symmetrized(randomTree+1)", n, seed,
                Symmetrized(WithExtraEdges(RandomTree(n, innerSeed), 1, innerSeed))),
        };
    }

    /// <summary>A random DAG from <see cref="RandomTree"/> or <see cref="RandomDag"/>.</summary>
    public static LabeledGraph AnyDag(long seed)
    {
        var rnd = new JavaRandom(seed);
        int n = PickNodeCount(rnd);
        double p = PickEdgeProb(rnd);
        long innerSeed = rnd.NextLong();
        int choice = rnd.NextInt(2);
        return choice == 0
            ? new LabeledGraph("randomTree", n, seed, RandomTree(n, innerSeed))
            : new LabeledGraph($"randomDag(p={F(p, 3)})", n, seed, RandomDag(n, p, innerSeed));
    }
}
