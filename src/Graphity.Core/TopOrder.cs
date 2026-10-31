using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Graphity;

/// <summary>
/// The topological order of a DAG (directed acyclic graph), in which every node comes before its
/// out-nodes.
/// </summary>
/// <remarks>
/// <para>
/// Uses a <see href="https://en.wikipedia.org/wiki/Topological_sorting#Depth-first_search">depth-first search</see>, without
/// recursion: <see cref="Of(Graph)"/> returns its reverse postorder.
/// The search starts at the nodes in ascending order and takes the out-nodes of each node in
/// ascending order, as <see cref="DfsTraverser.Run()"/> does, so a graph always gets the same
/// order. <see cref="TopOrderAndLevels"/> returns another topological order, together with a level
/// per node.
/// </para>
/// <para>
/// A graph with a cycle has no topological order, and <see cref="Of(Graph)"/> rejects it;
/// <see cref="Graphs.IsDag"/> tests for a cycle, and <see cref="Graphs.FindCycle"/> returns one. On
/// a graph with cycles, <see cref="Components.SccsOf"/> puts each cycle into one SCC (strongly
/// connected component) and numbers the SCCs in reverse topological order.
/// </para>
/// </remarks>
public static class TopOrder
{
    private const int Visited = 0b01;
    private const int OnStack = 0b10;

    /// <summary>
    /// Returns the nodes of <paramref name="graph"/> in topological order: for every edge
    /// <c>u -> v</c>, <c>u</c> comes before <c>v</c>.
    /// </summary>
    /// <remarks>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 4 x NodeCount bytes for the result; while it runs, 2 bits per node and up to
    /// 8 x NodeCount bytes for the stack, which grows with the deepest path.
    /// </remarks>
    /// <param name="graph">The DAG to sort.</param>
    /// <returns>The node IDs in topological order.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="graph"/> has a cycle, or a depth-first path is longer than 2^30 - 29 nodes.
    /// </exception>
    public static ImmutableArray<int> Of(Graph graph) => Of(graph, nameof(graph));

    // Of(Graph) for a caller that takes the graph as paramName, the parameter a cycle is reported
    // against. The depth-first search without recursion writes each node into order from the back
    // when it leaves it, and stops at the first edge to a node on its path, which closes a cycle.
    internal static ImmutableArray<int> Of(Graph graph, string paramName)
    {
        ArgumentNullException.ThrowIfNull(graph, paramName);
        int n = graph.NodeCount;
        int[] outStart = graph.OutStart;
        int[] adj = graph.OutAdj;
        // Visited and on-stack flags packed 2 bits per node, 16 nodes per int.
        var state = new int[(n >> 4) + 1];
        var order = new int[n];
        int orderPos = n;
        int[] frames = DfsFrames.Initial(n);

        for (int root = 0; root < n; root++)
        {
            int rShift = (root & 0xF) << 1;
            if (((state[root >> 4] >>> rShift) & Visited) != 0)
            {
                continue;
            }
            state[root >> 4] |= (Visited | OnStack) << rShift;
            frames[0] = root;
            frames[1] = outStart[root];
            int top = 0;

            while (top >= 0)
            {
                int u = frames[2 * top];
                int end = outStart[u + 1];
                int i = frames[2 * top + 1];
                bool descended = false;
                while (i < end)
                {
                    int v = adj[i++];
                    int shift = (v & 0xF) << 1;
                    int flags = state[v >> 4] >>> shift;
                    if ((flags & OnStack) != 0)
                    {
                        throw new ArgumentException("graph contains a cycle", paramName);
                    }
                    if ((flags & Visited) == 0)
                    {
                        frames[2 * top + 1] = i;
                        state[v >> 4] |= (Visited | OnStack) << shift;
                        if (++top == frames.Length >> 1)
                        {
                            frames = DfsFrames.Grow(frames, n);
                        }
                        frames[2 * top] = v;
                        frames[2 * top + 1] = outStart[v];
                        descended = true;
                        break;
                    }
                }
                if (descended)
                {
                    continue;
                }
                state[u >> 4] &= ~(OnStack << ((u & 0xF) << 1));
                order[--orderPos] = u;
                top--;
            }
        }
        return ImmutableCollectionsMarshal.AsImmutableArray(order);
    }
}
