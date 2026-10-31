using System.Runtime.InteropServices;

namespace Graphity;

/// <summary>
/// The type of every edge of a graph in a depth-first search: tree, back, forward or cross edge,
/// as <see cref="EdgeType"/> defines them.
/// </summary>
/// <remarks>
/// <para>
/// The types depend on the search. Uses a
/// <see href="https://en.wikipedia.org/wiki/Depth-first_search#Output_of_a_depth-first_search">depth-first search</see>,
/// without recursion. The search starts at the nodes in ascending order and takes the out-nodes of
/// each node in ascending order, as <see cref="DfsTraverser.Run()"/> does. <see cref="TypeOf"/>
/// returns the type of one edge, and <see cref="Edges.Of"/> collects the edges of one type.
/// </para>
/// <para>
/// A graph is a DAG (directed acyclic graph) exactly if it has no back edge. For the back edges
/// alone, <see cref="BackEdges.Of"/> saves the classification of the other edges.
/// </para>
/// <para>Instances are immutable and safe to share between threads.</para>
/// <para>
/// <b>Memory:</b> 1 byte per edge; an instance also keeps a reference to its graph.
/// </para>
/// </remarks>
/// <example>
/// Here, <c>cfg</c> is the control flow of a method with a loop and a branch: the edge
/// <c>next -> header</c> closes the loop, and <c>body -> next</c> skips <c>then</c>:
/// <!-- example: Traversals.cs#edgeTypes -->
/// <code language="csharp">
/// EdgeTypes types = EdgeTypes.Of(cfg);
/// EdgeType loopEdge = types.TypeOf(cfg.EdgeIndexOf(next, header));
/// Edges forwardEdges = Edges.Of(types, EdgeType.Forward);
/// // loopEdge: Back
/// // forwardEdges: body -> next
/// </code>
/// </example>
public sealed class EdgeTypes
{
    private const int Visited = 0b01;
    private const int OnStack = 0b10;

    // The graph, for the sources and targets in Edges.Of.
    internal readonly Graph Graph;

    // The type of each edge, by edge index.
    internal readonly EdgeType[] Types;

    private EdgeTypes(Graph graph, EdgeType[] types)
    {
        Graph = graph;
        Types = types;
    }

    /// <summary>
    /// Returns the type of every edge of <paramref name="graph"/>, from one depth-first search.
    /// </summary>
    /// <remarks>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> EdgeCount bytes for the result; while it runs, 4 bytes and 2 bits per node, and
    /// up to 8 x NodeCount bytes for the stack, which grows with the deepest path.
    /// </remarks>
    /// <param name="graph">The graph to classify.</param>
    /// <returns>The edge types.</returns>
    /// <exception cref="ArgumentException">A depth-first path is longer than 2^30 - 29 nodes.</exception>
    public static EdgeTypes Of(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (graph.EdgeCount == 0)
        {
            return new EdgeTypes(graph, []);
        }
        return new EdgeTypes(graph, Classify(graph));
    }

    /// <summary>Returns the type of edge <paramref name="edgeIndex"/>.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="edgeIndex">Edge index in <c>0..EdgeCount-1</c>.</param>
    /// <returns>The edge type.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edgeIndex"/> is out of range.</exception>
    public EdgeType TypeOf(int edgeIndex)
    {
        Checks.Index(edgeIndex, Types.Length);
        return Types[edgeIndex];
    }

    // The types as bytes, for the conformance tests.
    internal ReadOnlySpan<byte> AsBytes() => MemoryMarshal.AsBytes(Types.AsSpan());

    // The depth-first search without recursion. It classifies the edge u -> v by what it finds at v:
    // unvisited is a tree edge, on the path a back edge, and a node already left a forward edge if u
    // was discovered before v (v is a descendant of u), else a cross edge. Both flags of a node share
    // one int, 16 nodes per int (bit 0 visited, bit 1 on the path); the discovery times stay in disc.
    private static EdgeType[] Classify(Graph graph)
    {
        int n = graph.NodeCount;
        int[] outStart = graph.OutStart;
        int[] adj = graph.OutAdj;
        var state = new int[(n >> 4) + 1];
        var disc = new int[n];
        var types = new EdgeType[adj.Length];
        // Each frame is two interleaved ints, the node and its resume position in the adjacency
        // array: both share a cache line, and the array grows on demand.
        int[] frames = DfsFrames.Initial(n);
        int time = 0;

        for (int root = 0; root < n; root++)
        {
            int rShift = (root & 0xF) << 1;
            if (((state[root >> 4] >>> rShift) & Visited) != 0)
            {
                continue;
            }
            state[root >> 4] |= (Visited | OnStack) << rShift;
            disc[root] = ++time;
            frames[0] = root;
            frames[1] = outStart[root];
            int top = 0;

            while (top >= 0)
            {
                int u = frames[2 * top];
                int end = outStart[u + 1];
                // Tight neighbor scan: i and end stay in registers; the frame stack is touched only
                // when the search descends.
                int i = frames[2 * top + 1];
                bool descended = false;
                while (i < end)
                {
                    int e = i++;
                    int v = adj[e];
                    int shift = (v & 0xF) << 1;
                    int flags = state[v >> 4] >>> shift;
                    if ((flags & Visited) == 0)
                    {
                        types[e] = EdgeType.Tree;
                        frames[2 * top + 1] = i;
                        state[v >> 4] |= (Visited | OnStack) << shift;
                        disc[v] = ++time;
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
                        types[e] = EdgeType.Back;
                    }
                    else if (disc[u] < disc[v])
                    {
                        types[e] = EdgeType.Forward;
                    }
                    else
                    {
                        types[e] = EdgeType.Cross;
                    }
                }
                if (descended)
                {
                    continue;
                }
                state[u >> 4] &= ~(OnStack << ((u & 0xF) << 1));
                top--;
            }
        }
        return types;
    }
}
