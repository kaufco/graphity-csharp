namespace Graphity;

/// <summary>
/// The back edges of a graph: the edges that a depth-first search finds pointing back to a node on
/// its current path, self-loops included.
/// </summary>
/// <remarks>
/// <para>
/// Every cycle contains a back edge, so a graph without back edges is a DAG (directed acyclic
/// graph), and removing the back edges of a graph leaves a DAG. They form a feedback arc set,
/// though not in general a smallest one.
/// </para>
/// <para>
/// Uses the <see href="https://en.wikipedia.org/wiki/Depth-first_search#Output_of_a_depth-first_search">depth-first
/// search</see> of <see cref="EdgeTypes.Of"/>, without recursion:
/// <see cref="Of"/> finds the edges that <see cref="EdgeTypes"/> classifies as
/// <see cref="EdgeType.Back"/>, without classifying the others.
/// </para>
/// </remarks>
public static class BackEdges
{
    private const int Visited = 0b01;
    private const int OnStack = 0b10;

    /// <summary>
    /// Returns the back edges of <paramref name="graph"/>, in the order in which the search finds
    /// them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike the other results that list edges, they do not come in edge index order. For that
    /// order, use <c>Edges.Of(EdgeTypes.Of(graph), EdgeType.Back)</c>: the same edges, at the cost of
    /// classifying every edge first.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 12 bytes per back edge for the result; while it runs, up to 18 bytes more per
    /// back edge, 2 bits per node and up to 8 x NodeCount bytes for the stack, which grows with the
    /// deepest path.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to search.</param>
    /// <returns>The back edges with their edge indices.</returns>
    /// <exception cref="ArgumentException">A depth-first path is longer than 2^30 - 29 nodes.</exception>
    public static Edges Of(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        int[] outStart = graph.OutStart;
        int[] adj = graph.OutAdj;
        // Visited and on-stack flags packed 2 bits per node, 16 nodes per int: one load yields both
        // flags of a target.
        var state = new int[(n >> 4) + 1];
        var sources = new IntArrayList();
        var targets = new IntArrayList();
        var edgeIndices = new IntArrayList();
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
                    else if ((flags & OnStack) != 0)
                    {
                        sources.Add(u);
                        targets.Add(v);
                        edgeIndices.Add(i - 1);
                    }
                }
                if (descended)
                {
                    continue;
                }
                state[u >> 4] &= ~(OnStack << ((u & 0xF) << 1)); // postorder: leave the DFS stack
                top--;
            }
        }
        if (sources.IsEmpty)
        {
            return Edges.Empty;
        }
        return Edges.Wrap(sources.ToArray(), targets.ToArray(), edgeIndices.ToArray());
    }
}
