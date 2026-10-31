namespace Graphity.TestUtils;

/// <summary>A generated control-flow graph with its recipe, for reproducible failure messages.</summary>
public sealed record LabeledCfg(string Generator, int NodeCount, long Seed, Graph Graph)
{
    /// <summary>The recipe: generator, node count and seed.</summary>
    public string Describe() => $"gen={Generator}, n={NodeCount}, seed={Seed}";
}

/// <summary>
/// Generators for graphs shaped like control flow, for the fuzz tests of the control-flow analyses.
/// In every graph, node 0 is the entry and every node is reachable from it. The shapes: structured
/// code with nested loops, the same with jumps that make it irreducible, ladders on which dominance
/// frontiers grow quadratically, deep chains, and the fan behind a chain. Seeded through
/// <see cref="JavaRandom"/>, so they match the Java test utilities.
/// </summary>
public static class ControlFlowGraphs
{
    /// <summary>
    /// Structured control flow, reducible throughout: starting from the edge <c>0 -> 1</c>, a random
    /// edge is replaced by a sequence, an if-then, an if-then-else, a while loop or a do-while loop,
    /// until the graph has at least <paramref name="targetNodes"/> nodes.
    /// </summary>
    public static Graph Structured(int targetNodes, long seed)
    {
        var rnd = new JavaRandom(seed);
        var sources = new List<int> { 0 };
        var targets = new List<int> { 1 };
        int n = 2;
        while (n < targetNodes)
        {
            int e = rnd.NextInt(sources.Count);
            int a = sources[e];
            int b = targets[e];
            int last = sources.Count - 1;
            sources[e] = sources[last];
            targets[e] = targets[last];
            sources.RemoveAt(last);
            targets.RemoveAt(last);
            switch (rnd.NextInt(5))
            {
                case 0:
                    {
                        // sequence
                        int x = n++;
                        Add(sources, targets, a, x, x, b);
                        break;
                    }
                case 1:
                    {
                        // if-then
                        int cond = n++;
                        int then = n++;
                        Add(sources, targets, a, cond, cond, then, cond, b, then, b);
                        break;
                    }
                case 2:
                    {
                        // if-then-else
                        int cond = n++;
                        int then = n++;
                        int otherwise = n++;
                        Add(sources, targets, a, cond, cond, then, cond, otherwise, then, b, otherwise, b);
                        break;
                    }
                case 3:
                    {
                        // while: the header tests, the body returns to the header
                        int header = n++;
                        int body = n++;
                        Add(sources, targets, a, header, header, body, body, header, header, b);
                        break;
                    }
                default:
                    {
                        // do-while: body first, then the test
                        int body = n++;
                        int cond = n++;
                        Add(sources, targets, a, body, body, cond, cond, body, cond, b);
                        break;
                    }
            }
        }
        var builder = new GraphBuilder();
        builder.AddNodes(n);
        for (int i = 0; i < sources.Count; i++)
        {
            builder.AddEdge(sources[i], targets[i]);
        }
        return builder.Build();
    }

    /// <summary>
    /// <see cref="Structured"/> with <paramref name="jumps"/> random extra edges between non-entry
    /// nodes, which mostly enter a loop elsewhere than at its header and make the graph irreducible.
    /// </summary>
    public static Graph Irreducible(int targetNodes, int jumps, long seed)
    {
        var baseGraph = Structured(targetNodes, seed);
        int n = baseGraph.NodeCount;
        var rnd = new JavaRandom(seed ^ unchecked((long)0x9E3779B97F4A7C15UL));
        var b = CopyOf(baseGraph);
        for (int i = 0; i < jumps; i++)
        {
            b.AddEdge(1 + rnd.NextInt(n - 1), 1 + rnd.NextInt(n - 1));
        }
        return b.Build();
    }

    /// <summary>
    /// A ladder of <paramref name="rungs"/> branches: rails <c>a_0 -> a_1 -> ...</c> and
    /// <c>b_0 -> b_1 -> ...</c> with a rung <c>a_i -> b_i</c> at every step; both rails end in a
    /// common exit. Node 0 is <c>a_0</c>; <c>a_i = i</c>, <c>b_i = rungs + i</c>, exit = 2 x rungs.
    /// </summary>
    public static Graph Ladder(int rungs)
    {
        int exit = 2 * rungs;
        var b = new GraphBuilder();
        b.AddNodes((2 * rungs) + 1);
        for (int i = 0; i < rungs; i++)
        {
            b.AddEdge(i, rungs + i);
            if (i + 1 < rungs)
            {
                b.AddEdge(i, i + 1);
                b.AddEdge(rungs + i, rungs + i + 1);
            }
        }
        if (rungs > 0)
        {
            b.AddEdge(rungs - 1, exit);
            b.AddEdge((2 * rungs) - 1, exit);
        }
        return b.Build();
    }

    /// <summary>A straight-line chain <c>0 -> 1 -> ... -> n-1</c>: the deepest possible dominator tree.</summary>
    public static Graph Chain(int n)
    {
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int i = 0; i + 1 < n; i++)
        {
            b.AddEdge(i, i + 1);
        }
        return b.Build();
    }

    /// <summary>
    /// A fan behind a chain: <c>0 -> 1 -> ... -> chainLength</c>, then an edge from the end of the
    /// chain and one from the entry to each of <paramref name="fanWidth"/> further nodes, whose
    /// immediate dominator is the entry.
    /// </summary>
    public static Graph FanBehindChain(int chainLength, int fanWidth)
    {
        var b = new GraphBuilder();
        b.AddNodes(1 + chainLength + fanWidth);
        for (int i = 0; i < chainLength; i++)
        {
            b.AddEdge(i, i + 1);
        }
        for (int f = 0; f < fanWidth; f++)
        {
            int fan = 1 + chainLength + f;
            b.AddEdge(chainLength, fan);
            b.AddEdge(0, fan);
        }
        return b.Build();
    }

    /// <summary>
    /// A control-flow graph of a random shape and size: mostly structured and irreducible ones, with
    /// ladders, chains and fans mixed in; small enough for the quadratic oracles.
    /// </summary>
    public static LabeledCfg AnyControlFlowGraph(long seed)
    {
        var rnd = new JavaRandom(seed);
        int n = 4 + rnd.NextInt(200);
        long innerSeed = rnd.NextLong();
        int choice = rnd.NextInt(8);
        switch (choice)
        {
            case 0:
            case 1:
            case 2:
                return new LabeledCfg("structured", n, seed, Structured(n, innerSeed));
            case 3:
            case 4:
                int jumps = 1 + rnd.NextInt(Math.Max(1, n / 10));
                return new LabeledCfg($"irreducible(jumps={jumps})", n, seed, Irreducible(n, jumps, innerSeed));
            case 5:
                return new LabeledCfg("ladder", n, seed, Ladder(n / 2));
            case 6:
                return new LabeledCfg("chain", n, seed, Chain(n));
            default:
                return new LabeledCfg("fanBehindChain", n, seed, FanBehindChain(n / 2, n - (n / 2) - 1));
        }
    }

    private static void Add(List<int> sources, List<int> targets, params ReadOnlySpan<int> pairs)
    {
        for (int i = 0; i < pairs.Length; i += 2)
        {
            sources.Add(pairs[i]);
            targets.Add(pairs[i + 1]);
        }
    }

    private static GraphBuilder CopyOf(Graph graph)
    {
        var b = new GraphBuilder();
        b.AddNodes(graph.NodeCount);
        for (int u = 0; u < graph.NodeCount; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                b.AddEdge(u, v);
            }
        }
        return b;
    }
}
