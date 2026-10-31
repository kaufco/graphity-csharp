namespace Graphity.TestUtils;

/// <summary>
/// Oracle for dominance, straight from the definition: <c>d</c> dominates <c>v</c> if <c>v</c> is
/// reachable from the entries, but no longer once <c>d</c> is removed. Shares no code and no idea
/// with the algorithms under test. O(NodeCount x (NodeCount + EdgeCount)) time and NodeCount² bits;
/// meant for graphs of a few hundred nodes.
/// </summary>
public sealed class NaiveDominators
{
    private readonly Graph _graph;

    /// <summary>Computes dominance and reachability of <paramref name="graph"/> from <paramref name="entries"/>.</summary>
    public NaiveDominators(Graph graph, ReadOnlySpan<int> entries)
    {
        _graph = graph;
        int n = graph.NodeCount;
        Reachable = Reach(graph, entries, -1);
        Dominates = new bool[n][];
        for (int d = 0; d < n; d++)
        {
            Dominates[d] = new bool[n];
            if (!Reachable[d])
            {
                continue;
            }
            bool[] without = Reach(graph, entries, d);
            for (int v = 0; v < n; v++)
            {
                Dominates[d][v] = Reachable[v] && !without[v];
            }
        }
    }

    /// <summary><c>Dominates[d][v]</c>: <c>d</c> dominates <c>v</c>. A node dominates itself.</summary>
    public bool[][] Dominates { get; }

    /// <summary>Reachability from the entries.</summary>
    public bool[] Reachable { get; }

    /// <summary>
    /// The strict dominator of <paramref name="v"/> that all other strict dominators dominate, or -1
    /// if there is none or <paramref name="v"/> is unreachable: the one with the most dominators.
    /// </summary>
    public int ImmediateDominatorOf(int v)
    {
        if (!Reachable[v])
        {
            return -1;
        }
        int n = _graph.NodeCount;
        int best = -1;
        int bestCount = -1;
        for (int d = 0; d < n; d++)
        {
            if (d == v || !Dominates[d][v])
            {
                continue;
            }
            int count = 0;
            for (int x = 0; x < n; x++)
            {
                if (Dominates[x][d])
                {
                    count++;
                }
            }
            if (count > bestCount)
            {
                best = d;
                bestCount = count;
            }
        }
        return best;
    }

    /// <summary>All dominance frontiers: <c>frontier[x][m]</c> if <c>x</c> dominates a predecessor of <c>m</c> and does not strictly dominate <c>m</c>.</summary>
    public bool[][] Frontiers()
    {
        int n = _graph.NodeCount;
        var frontier = new bool[n][];
        for (int x = 0; x < n; x++)
        {
            frontier[x] = new bool[n];
        }
        for (int x = 0; x < n; x++)
        {
            if (!Reachable[x])
            {
                continue;
            }
            for (int p = 0; p < n; p++)
            {
                if (!Dominates[x][p])
                {
                    continue;
                }
                foreach (int m in _graph.OutNodes(p))
                {
                    if (!(Dominates[x][m] && x != m))
                    {
                        frontier[x][m] = true;
                    }
                }
            }
        }
        return frontier;
    }

    /// <summary>The iterated dominance frontier of <paramref name="nodes"/>: the frontier of the set, then of the set joined with the result, until it no longer grows.</summary>
    public bool[] IteratedFrontier(bool[][] frontiers, ReadOnlySpan<int> nodes)
    {
        int n = _graph.NodeCount;
        var source = new bool[n];
        foreach (int node in nodes)
        {
            source[node] = true;
        }
        var result = new bool[n];
        bool grew = true;
        while (grew)
        {
            grew = false;
            for (int x = 0; x < n; x++)
            {
                if (!source[x] && !result[x])
                {
                    continue;
                }
                for (int m = 0; m < n; m++)
                {
                    if (frontiers[x][m] && !result[m])
                    {
                        result[m] = true;
                        grew = true;
                    }
                }
            }
        }
        return result;
    }

    /// <summary>Breadth-first reachability from the entries, with <paramref name="removed"/> taken out of the graph.</summary>
    private static bool[] Reach(Graph graph, ReadOnlySpan<int> entries, int removed)
    {
        int n = graph.NodeCount;
        var seen = new bool[n];
        var queue = new int[n];
        int head = 0;
        int tail = 0;
        foreach (int e in entries)
        {
            if (e != removed && !seen[e])
            {
                seen[e] = true;
                queue[tail++] = e;
            }
        }
        while (head < tail)
        {
            int u = queue[head++];
            foreach (int v in graph.OutNodes(u))
            {
                if (v != removed && !seen[v])
                {
                    seen[v] = true;
                    queue[tail++] = v;
                }
            }
        }
        return seen;
    }
}
