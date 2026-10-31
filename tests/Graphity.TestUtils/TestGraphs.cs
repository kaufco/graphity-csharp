namespace Graphity.TestUtils;

/// <summary>Deterministic mini-graphs for unit tests: small, named, hand-checkable topologies.</summary>
public static class TestGraphs
{
    /// <summary>The graph with no nodes and no edges.</summary>
    public static Graph Empty() => new GraphBuilder().Build();

    /// <summary><paramref name="n"/> isolated nodes.</summary>
    public static Graph Isolated(int n)
    {
        var b = new GraphBuilder();
        b.AddNodes(n);
        return b.Build();
    }

    /// <summary>Directed path <c>0 -> 1 -> ... -> n-1</c>.</summary>
    public static Graph Path(int n)
    {
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int i = 0; i + 1 < n; i++)
        {
            b.AddEdge(i, i + 1);
        }
        return b.Build();
    }

    /// <summary>Directed cycle <c>0 -> 1 -> ... -> n-1 -> 0</c>; a self-loop for <c>n == 1</c>.</summary>
    public static Graph Cycle(int n)
    {
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int i = 0; i < n; i++)
        {
            b.AddEdge(i, (i + 1) % n);
        }
        return b.Build();
    }

    /// <summary>Complete directed graph without self-loops: <c>n * (n-1)</c> edges.</summary>
    public static Graph Clique(int n)
    {
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int u = 0; u < n; u++)
        {
            for (int v = 0; v < n; v++)
            {
                if (u != v)
                {
                    b.AddEdge(u, v);
                }
            }
        }
        return b.Build();
    }

    /// <summary>Out-star: <c>0 -> 1, 0 -> 2, ..., 0 -> n-1</c>.</summary>
    public static Graph OutStar(int n)
    {
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int i = 1; i < n; i++)
        {
            b.AddEdge(0, i);
        }
        return b.Build();
    }

    /// <summary>In-star: <c>1 -> 0, 2 -> 0, ..., n-1 -> 0</c>.</summary>
    public static Graph InStar(int n)
    {
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int i = 1; i < n; i++)
        {
            b.AddEdge(i, 0);
        }
        return b.Build();
    }

    /// <summary>
    /// A graph with <paramref name="n"/> nodes from a flat list of edge endpoints:
    /// <c>Of(3, 0, 1, 1, 2)</c> yields <c>0 -> 1</c> and <c>1 -> 2</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The endpoint list has odd length.</exception>
    public static Graph Of(int n, params ReadOnlySpan<int> edges)
    {
        if ((edges.Length & 1) != 0)
        {
            throw new ArgumentException("edges must come in (source, target) pairs", nameof(edges));
        }
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int i = 0; i < edges.Length; i += 2)
        {
            b.AddEdge(edges[i], edges[i + 1]);
        }
        return b.Build();
    }
}
