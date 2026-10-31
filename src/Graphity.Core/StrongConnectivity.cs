namespace Graphity;

/// <summary>
/// The strong bridges and strong articulation points of a graph, as
/// <see cref="Components.StrongConnectivityOf"/> returns them.
/// </summary>
/// <remarks>
/// <para>
/// A strong bridge is an edge whose removal increases the number of SCCs (strongly connected
/// components), and a strong articulation point is a node whose removal does the same. Strong
/// bridges lie inside SCCs of two or more nodes. Strong articulation points lie inside SCCs of
/// three or more nodes, since without one node of an SCC of two, the other still forms an SCC. So a
/// graph without cycles has neither, and a self-loop is never a strong bridge.
/// </para>
/// <para>
/// The edges are followed in their direction, as by <see cref="Components.SccsOf"/>: the edges
/// <c>a -> b</c> and <c>b -> a</c> are two edges, and both are strong bridges of the SCC they form.
/// <see cref="Biconnectivity"/> has the counterparts for a graph read as undirected.
/// </para>
/// <para>Instances are immutable and safe to share between threads.</para>
/// <para><b>Memory:</b> up to 28 x NodeCount bytes.</para>
/// </remarks>
/// <example>
/// Here, in the control flow of the method <c>load</c>, the loop splits into several SCCs without
/// <c>header</c>, <c>body</c> or <c>next</c>, but not without <c>then</c>:
/// <!-- example: Connectivity.cs#strongConnectivity -->
/// <code language="csharp">
/// StrongConnectivity strong =
///     Components.StrongConnectivityOf(BiAdjacentGraph.Of(cfg));
/// Edges strongBridges = strong.StrongBridges;
/// // strongBridges: header -> body, body -> then, then -> next, next -> header
/// SortedIntView strongArticulationPoints = strong.StrongArticulationPoints;
/// // strongArticulationPoints: [1, 2, 4] - header, body and next
/// </code>
/// </example>
public sealed class StrongConnectivity
{
    private StrongConnectivity(Edges strongBridges, SortedIntView strongArticulationPoints)
    {
        StrongBridges = strongBridges;
        StrongArticulationPoints = strongArticulationPoints;
    }

    /// <summary>
    /// Computes both results, following Italiano, Laura and Santaroni: within an SCC and for any
    /// node <c>r</c> of it, an edge is a strong bridge exactly if it is a bridge of the flow graph
    /// rooted at <c>r</c> or of its transpose, and a node other than <c>r</c> is a strong
    /// articulation point exactly if it dominates another node in one of the two. Whether <c>r</c>
    /// itself is one is a separate check.
    /// </summary>
    internal static StrongConnectivity Of(BiAdjacentGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        // The dominators below allow one node less than Tarjan's algorithm: reject before it runs.
        ArrayLimits.CheckNodeCount(n, DfsPreorder.MaxNodeCount, nameof(Components.StrongConnectivityOf));
        Sccs sccs = Sccs.Of(graph);
        int sccCount = sccs.SccCount;
        int[] sccOf = sccs.Mapping;
        int[] members = sccs.Members;
        int[] memberStart = sccs.MemberStart;

        // One entry per component, its first member: sink components first for the graph, source
        // components first for its transpose, so every search stays in its component.
        var sinksFirst = new int[sccCount];
        var sourcesFirst = new int[sccCount];
        for (int c = 0; c < sccCount; c++)
        {
            sinksFirst[c] = members[memberStart[c]];
            sourcesFirst[c] = members[memberStart[sccCount - 1 - c]];
        }
        int[] forward = Dominators.WithinBlocks(graph, sinksFirst);
        int[] backward = Dominators.WithinBlocks(graph.Transposed(), sourcesFirst);

        var sources = new IntArrayList();
        var targets = new IntArrayList();
        var edgeIndices = new IntArrayList();
        var isArticulation = new bool[n];
        Collect(forward, graph.InStart, graph.InAdj, sccOf, false, sources, targets, edgeIndices, isArticulation);
        Collect(backward, graph.OutStart, graph.OutAdj, sccOf, true, sources, targets, edgeIndices, isArticulation);
        MarkEntries(graph, sccOf, members, memberStart, isArticulation);
        return new StrongConnectivity(
            Edges.SortedEdges(graph, sources.Elements, targets.Elements, edgeIndices.Elements, sources.Count, true),
            Biconnectivity.FlaggedNodes(isArticulation));
    }

    /// <summary>
    /// Adds the bridges and non-trivial dominators of one flow graph, given by its immediate
    /// dominators and its in-adjacency. The edge <c>idom(v) → v</c> is a bridge exactly if it exists
    /// and <c>v</c> dominates every other predecessor of <c>v</c> in its component. With
    /// <c>transposed</c>, the flow graph is the transpose, so a bridge <c>u → v</c> found there is
    /// the edge <c>v → u</c> of the graph, and the in-adjacency slot of <c>u</c> is the
    /// out-adjacency slot of the graph - its edge index.
    /// </summary>
    private static void Collect(int[] idom, int[] inStart, int[] inAdj, int[] sccOf, bool transposed,
        IntArrayList sources, IntArrayList targets, IntArrayList edgeIndices, bool[] isArticulation)
    {
        int n = idom.Length;
        var preorderIndex = new int[n];
        var subtreeSize = new int[n];
        ParentForest.Preorder(idom, preorderIndex, new int[n], subtreeSize);
        for (int v = 0; v < n; v++)
        {
            int u = idom[v];
            if (u < 0)
            {
                continue;
            }
            if (idom[u] >= 0)
            {
                isArticulation[u] = true;
            }
            int scc = sccOf[v];
            int pre = preorderIndex[v];
            int size = subtreeSize[v];
            int fromIdom = -1;
            bool bridge = true;
            for (int i = inStart[v], end = inStart[v + 1]; i < end; i++)
            {
                int w = inAdj[i];
                if (w == u)
                {
                    fromIdom = i;
                }
                else if (sccOf[w] == scc)
                {
                    // v dominates w exactly if w lies in v's subtree, as in PreorderAncestry.IsAncestorOf.
                    int offset = preorderIndex[w] - pre;
                    if (offset < 0 || offset >= size)
                    {
                        bridge = false;
                        break;
                    }
                }
            }
            if (bridge && fromIdom >= 0)
            {
                sources.Add(transposed ? v : u);
                targets.Add(transposed ? u : v);
                edgeIndices.Add(transposed ? fromIdom : -1);
            }
        }
    }

    /// <summary>
    /// Marks the entry <c>r</c> of every component of three or more nodes if the rest of the
    /// component is not strongly connected: a search from another member, avoiding <c>r</c> and the
    /// edges leaving the component, misses a member forwards or backwards.
    /// </summary>
    private static void MarkEntries(BiAdjacentGraph graph, int[] sccOf, int[] members, int[] memberStart,
        bool[] isArticulation)
    {
        int n = graph.NodeCount;
        var stamp = new int[n];
        var queue = new int[n];
        for (int c = 0; c < memberStart.Length - 1; c++)
        {
            int size = memberStart[c + 1] - memberStart[c];
            if (size < 3)
            {
                continue;
            }
            int entry = members[memberStart[c]];
            int start = members[memberStart[c] + 1];
            int rest = size - 1;
            // A fresh mark per component and direction, so stamp is never cleared.
            if (ReachedAvoiding(graph.OutStart, graph.OutAdj, sccOf, c, entry, start, (2 * c) + 1, stamp, queue) < rest
                || ReachedAvoiding(graph.InStart, graph.InAdj, sccOf, c, entry, start, (2 * c) + 2, stamp, queue) < rest)
            {
                isArticulation[entry] = true;
            }
        }
    }

    /// <summary>Breadth-first count of the members of component <paramref name="scc"/> reached from <paramref name="start"/>.</summary>
    private static int ReachedAvoiding(int[] adjStart, int[] adj, int[] sccOf, int scc, int avoided, int start,
        int mark, int[] stamp, int[] queue)
    {
        stamp[avoided] = mark;
        stamp[start] = mark;
        queue[0] = start;
        int head = 0;
        int tail = 1;
        while (head < tail)
        {
            int u = queue[head++];
            for (int i = adjStart[u], end = adjStart[u + 1]; i < end; i++)
            {
                int v = adj[i];
                if (stamp[v] != mark && sccOf[v] == scc)
                {
                    stamp[v] = mark;
                    queue[tail++] = v;
                }
            }
        }
        return tail;
    }

    /// <summary>Gets the strong bridges, in edge index order.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public Edges StrongBridges { get; }

    /// <summary>Gets the strong articulation points, in ascending order.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public SortedIntView StrongArticulationPoints { get; }
}
