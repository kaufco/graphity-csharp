using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Graphity;

/// <summary>
/// The distances of the nodes of a graph from one or several sources, with a shortest path to every
/// node reached.
/// </summary>
/// <remarks>
/// <para>
/// The sources are the nodes where the search starts. The distance of a node is the number of edges
/// on a shortest path to it from the nearest source. A source has distance 0, and a node that no
/// source reaches has distance -1. Edges are followed in their direction. For distances against their
/// direction, pass the transposed graph: <c>BiAdjacentGraph.Of(graph).Transposed()</c>.
/// </para>
/// <para>
/// Uses a <see href="https://en.wikipedia.org/wiki/Breadth-first_search">breadth-first search</see> that takes the sources in list order and the out-nodes of each node
/// in ascending order. For the reachable nodes alone,
/// <see cref="Graphs.ReachableFrom(Graph, ReadOnlySpan{int})"/> needs less memory.
/// </para>
/// <para>Instances are immutable and safe to share between threads.</para>
/// <para>
/// <b>Memory:</b> 8 x NodeCount bytes: the distance of each node and the node before it on its
/// shortest path.
/// </para>
/// </remarks>
/// <example>
/// Here, <c>calls</c> are the calls between the methods of a program, and the search finds a shortest
/// chain of calls from <c>main</c> to <c>parse</c>:
/// <!-- example: Traversals.cs#pathTo -->
/// <code language="csharp">
/// BfsDistances fromMain = BfsDistances.Of(calls, main);
/// int depth = fromMain.DistanceOf(parse);
/// ImmutableArray&lt;int> chain = fromMain.PathTo(parse);
/// // depth: 3
/// // chain: [0, 2, 3, 6] - main -> run -> load -> parse
/// </code>
/// </example>
public sealed class BfsDistances
{
    // The distance of each node from the nearest source, -1 if no source reaches it.
    private readonly int[] _distance;

    // The node before each node on its shortest path, -1 for a source and a node not reached.
    private readonly int[] _predecessor;

    private BfsDistances(int[] distance, int[] predecessor, int maxDistance)
    {
        _distance = distance;
        _predecessor = predecessor;
        MaxDistance = maxDistance;
    }

    /// <summary>
    /// Returns the distances of the nodes of <paramref name="graph"/> from <paramref name="source"/>.
    /// Equivalent to <c>Of(graph, [source])</c>.
    /// </summary>
    /// <remarks>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> as for <see cref="Of(Graph, ReadOnlySpan{int})"/>.
    /// </remarks>
    /// <param name="graph">The graph to search.</param>
    /// <param name="source">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The distances.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="source"/> is out of range.</exception>
    public static BfsDistances Of(Graph graph, int source)
    {
        ArgumentNullException.ThrowIfNull(graph);
        Checks.Index(source, graph.NodeCount);
        return Of(graph, [source]);
    }

    /// <summary>
    /// Returns the distances of the nodes of <paramref name="graph"/> from the nearest of
    /// <paramref name="sources"/>.
    /// </summary>
    /// <remarks>
    /// <para>A source given twice counts once, and with no sources, no node is reachable.</para>
    /// <para>
    /// <b>Time:</b> O(sources.Length + NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 8 x NodeCount bytes for the result; while it runs, 4 x NodeCount bytes more.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to search.</param>
    /// <param name="sources">Node IDs, each in <c>0..NodeCount-1</c>.</param>
    /// <returns>The distances.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A source is out of range.</exception>
    public static BfsDistances Of(Graph graph, ReadOnlySpan<int> sources)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        Checks.Nodes(sources, n);
        int[] outStart = graph.OutStart;
        int[] adj = graph.OutAdj;
        var distance = new int[n];
        var predecessor = new int[n];
        Array.Fill(distance, -1);
        Array.Fill(predecessor, -1);
        var queue = new int[n];
        int tail = 0;
        foreach (int s in sources)
        {
            if (distance[s] < 0)
            {
                distance[s] = 0;
                queue[tail++] = s;
            }
        }
        for (int head = 0; head < tail; head++)
        {
            int u = queue[head];
            int next = distance[u] + 1;
            for (int e = outStart[u], end = outStart[u + 1]; e < end; e++)
            {
                int v = adj[e];
                if (distance[v] < 0)
                {
                    distance[v] = next;
                    predecessor[v] = u;
                    queue[tail++] = v;
                }
            }
        }
        int maxDistance = tail == 0 ? -1 : distance[queue[tail - 1]];
        return new BfsDistances(distance, predecessor, maxDistance);
    }

    /// <summary>
    /// Returns the distance of <paramref name="node"/>, or -1 if no source reaches it. The distance
    /// is the number of edges on a shortest path to <paramref name="node"/> from the nearest source.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The distance, or -1.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int DistanceOf(int node)
    {
        Checks.Index(node, _distance.Length);
        return _distance[node];
    }

    /// <summary>
    /// Returns whether a source reaches <paramref name="node"/>. Equivalent to
    /// <c>DistanceOf(node) &gt;= 0</c>.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns><see langword="true"/> if a source reaches <paramref name="node"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public bool IsReachable(int node)
    {
        Checks.Index(node, _distance.Length);
        return _distance[node] >= 0;
    }

    /// <summary>
    /// Returns a shortest path to <paramref name="node"/> from the nearest source, or an empty list
    /// if no source reaches <paramref name="node"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The path has <c>DistanceOf(node) + 1</c> nodes, with the source first and
    /// <paramref name="node"/> last. Of several shortest paths, this method returns the one that the
    /// search finds first.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(DistanceOf(node)).<br/>
    /// <b>Memory:</b> 4 x (DistanceOf(node) + 1) bytes for the result.
    /// </para>
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The node IDs of the path, or an empty list.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public ImmutableArray<int> PathTo(int node)
    {
        Checks.Index(node, _distance.Length);
        int d = _distance[node];
        if (d < 0)
        {
            return [];
        }
        var path = new int[d + 1];
        for (int i = d, v = node; i >= 0; i--, v = _predecessor[v])
        {
            path[i] = v;
        }
        return ImmutableCollectionsMarshal.AsImmutableArray(path);
    }

    /// <summary>
    /// Gets the largest distance of a node that a source reaches, or -1 if there are no sources.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public int MaxDistance { get; }
}
