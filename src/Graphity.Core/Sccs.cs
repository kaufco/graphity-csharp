namespace Graphity;

/// <summary>
/// The SCCs (strongly connected components) of a graph, as <see cref="Components.SccsOf"/> returns
/// them.
/// </summary>
/// <remarks>
/// <para>
/// It is a <see cref="NodePartition"/> whose groups are the SCCs, numbered by SCC ID
/// <c>0..SccCount-1</c>. <see cref="SccOf"/> returns the SCC of a node and
/// <see cref="NodePartition.NodesOf"/> the nodes of an SCC. <see cref="ContainsCycle"/> distinguishes
/// the SCCs with a cycle from those of a single node without one.
/// <see cref="NodePartition.Condense"/> returns the DAG (directed acyclic graph) of the SCCs.
/// </para>
/// <para>
/// Every node is in exactly one SCC; a node that lies on no cycle is an SCC of its own. SCC IDs are
/// in reverse topological order: every edge between two SCCs goes from the higher SCC ID to the
/// lower one, so SCC 0 has no edges to other SCCs. The nodes of each SCC are in ascending order.
/// </para>
/// <para>Instances are immutable and safe to share between threads.</para>
/// <para>
/// <b>Memory:</b> 4 x (2 x NodeCount + SccCount) bytes; the graph is shared, not copied.
/// </para>
/// </remarks>
/// <example>
/// Here, <c>dependencies</c> are the dependencies between the packages of a program, and <c>names</c>
/// holds the names of the packages. The example groups the packages by SCC, in the order of the SCC
/// IDs. In this order, each package comes after the packages it uses, and the packages that use each
/// other come together:
/// <!-- example: Connectivity.cs#sccsOf -->
/// <code language="csharp">
/// // The SCCs in reverse topological order, each as the names of its packages.
/// Sccs sccs = Components.SccsOf(dependencies);
/// List&lt;List&lt;string>> buildOrder = [];
/// for (int scc = 0; scc &lt; sccs.SccCount; scc++)
/// {
///     buildOrder.Add([.. sccs.NodesOf(scc).Select(node => names[node])]);
/// }
/// // buildOrder: [[util], [cli], [json], [model, storage], [service], [ui], [app]]
/// </code>
/// </example>
public sealed class Sccs : NodePartition
{
    internal Sccs(Graph graph, int[] nodeToScc, int[] sccNodes, int[] sccStart)
        : base(graph, nodeToScc, sccNodes, sccStart)
    {
    }

    // Bits 0-30 of a lane of Tarjan's algorithm.
    private const int Value = int.MaxValue;

    // Bit 31 of a lane: visited in lane 0, on-stack in lane 1.
    private const int Flag = int.MinValue;

    // The body of Components.SccsOf, here so that the algorithms that need SCCs do not call the
    // facade.
    //
    // Tarjan's algorithm with an explicit frame stack, so deep graphs cannot overflow the call
    // stack. An SCC closes only after every SCC it reaches, which puts the SCC IDs in reverse
    // topological order.
    //
    // The per-node state - index, lowlink, visited and on-stack - lives in two interleaved int lanes
    // per node, index | visited << 31 and lowlink | onStack << 31: index and lowlink are below
    // nodeCount, so 31 bits each suffice. Every node touch is then one cache line, and the layout
    // stays within 32-bit arithmetic. Two lanes per node cap the node count at
    // ArrayLimits.MaxNodeCountTwoInts; a depth-first path through every node then still fits into
    // the frame stack.
    //
    // sccStack holds the nodes not yet assigned to a completed SCC: a node can be finished in the
    // search and still wait there until the root of its SCC finishes.
    internal static Sccs Of(Graph graph)
    {
        int n = graph.NodeCount;
        ArrayLimits.CheckNodeCount(n, ArrayLimits.MaxNodeCountTwoInts, "Tarjan's algorithm");
        int[] outStart = graph.OutStart;
        int[] adj = graph.OutAdj;
        // Per node two interleaved lanes: index | visited << 31 and lowlink | onStack << 31 - one
        // cache line per node touch, and 32-bit arithmetic only.
        var lanes = new int[2 * n];
        var sccStack = new int[n];
        var nodeToScc = new int[n];
        var sccNodes = new int[n];
        var sccStart = new int[n + 1];
        int nextIndex = 0;
        int sccTop = 0;
        int sccCount = 0;
        int sccNodesPos = 0;
        // Each frame is two interleaved ints, the node and its resume position in the adjacency
        // array: both share a cache line, and the array grows on demand.
        int[] frames = DfsFrames.Initial(n);

        for (int root = 0; root < n; root++)
        {
            if ((lanes[2 * root] & Flag) != 0)
            {
                continue;
            }
            // Preorder of the root: index and lowlink, both flagged, and onto Tarjan's stack.
            lanes[2 * root] = nextIndex | Flag;
            lanes[2 * root + 1] = nextIndex | Flag;
            nextIndex++;
            sccStack[sccTop++] = root;
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
                    int v = adj[i++];
                    if ((lanes[2 * v] & Flag) == 0)
                    {
                        frames[2 * top + 1] = i; // resume here after v's subtree finishes
                        lanes[2 * v] = nextIndex | Flag;
                        lanes[2 * v + 1] = nextIndex | Flag;
                        nextIndex++;
                        sccStack[sccTop++] = v;
                        if (++top == frames.Length >> 1)
                        {
                            frames = DfsFrames.Grow(frames, n);
                        }
                        frames[2 * top] = v;
                        frames[2 * top + 1] = outStart[v];
                        descended = true;
                        break;
                    }
                    else if ((lanes[2 * v + 1] & Flag) != 0)
                    {
                        // lowlink[u] = min(lowlink[u], index[v]); same cache line as v's lane 0.
                        int idxV = lanes[2 * v] & Value;
                        int lowLane = lanes[2 * u + 1];
                        if (idxV < (lowLane & Value))
                        {
                            lanes[2 * u + 1] = (lowLane & Flag) | idxV;
                        }
                    }
                }
                if (descended)
                {
                    continue;
                }

                // Postorder: u is finished. Close its SCC if it is a root, then propagate its lowlink
                // to the parent, the node beneath it on the DFS stack.
                int lowU = lanes[2 * u + 1] & Value;
                if (lowU == (lanes[2 * u] & Value))
                {
                    int start = sccNodesPos;
                    sccStart[sccCount] = start;
                    int w;
                    do
                    {
                        w = sccStack[--sccTop];
                        lanes[2 * w + 1] &= Value;
                        nodeToScc[w] = sccCount;
                        sccNodes[sccNodesPos++] = w;
                    }
                    while (w != u);
                    sccNodes.AsSpan(start, sccNodesPos - start).Sort();
                    sccCount++;
                }
                if (--top >= 0)
                {
                    int parent = frames[2 * top];
                    int lowLane = lanes[2 * parent + 1];
                    if (lowU < (lowLane & Value))
                    {
                        lanes[2 * parent + 1] = (lowLane & Flag) | lowU;
                    }
                }
            }
        }
        sccStart[sccCount] = sccNodesPos;
        return new Sccs(graph, nodeToScc, sccNodes, sccStart[..(sccCount + 1)]);
    }

    /// <summary>Returns the SCC ID of <paramref name="node"/>. Equivalent to <c>Get(node)</c>.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The SCC ID.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int SccOf(int node) => Get(node);

    /// <summary>Gets the number of SCCs. Equivalent to <see cref="IndexPartition.GroupCount"/>.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public int SccCount => GroupCount;

    /// <summary>
    /// Returns whether SCC <paramref name="sccId"/> contains a cycle: whether it has more than one
    /// node, or a single node with a self-loop.
    /// </summary>
    /// <remarks>
    /// <b>Time:</b> O(1) for an SCC of more than one node, O(log OutDegree(v)) for an SCC of a
    /// single node <c>v</c>.
    /// </remarks>
    /// <param name="sccId">SCC ID in <c>0..SccCount-1</c>.</param>
    /// <returns><see langword="true"/> if the SCC contains a cycle.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sccId"/> is out of range.</exception>
    public bool ContainsCycle(int sccId)
    {
        Checks.Index(sccId, GroupCount);
        return MemberStart[sccId + 1] - MemberStart[sccId] > 1
            || SourceGraph.ContainsSelfLoop(Members[MemberStart[sccId]]);
    }

    /// <summary>
    /// Gets whether the graph is strongly connected: whether it has at most one SCC. Equivalent to
    /// <c>SccCount &lt;= 1</c>.
    /// </summary>
    /// <remarks>
    /// <para>A graph without nodes counts as strongly connected.</para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    public bool IsStronglyConnected => GroupCount <= 1;
}
