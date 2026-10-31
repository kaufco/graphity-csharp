namespace Graphity;

/// <summary>
/// The dominators of a graph with one or more entries, such as the control flow of a method, given by
/// the immediate dominator of every node.
/// </summary>
/// <remarks>
/// <para>
/// A node <c>d</c> dominates a node <c>n</c> if every path from an entry to <c>n</c> passes through
/// <c>d</c>; every node dominates itself. The immediate dominator of <c>n</c> is the dominator
/// closest to <c>n</c> other than <c>n</c> itself. The immediate dominators form the dominator tree,
/// which <see cref="Graphs.DominatorTree"/> returns as a graph. Several entries act like one virtual
/// root above all of them. A node that only this virtual root dominates, such as an entry, has no
/// immediate dominator and is a root of the dominator tree, which is then a forest. A node that no
/// entry reaches has no immediate dominator either; <see cref="IsReachable"/> tells the two apart.
/// </para>
/// <para>
/// Whether one node dominates another is an ancestor query on the dominator tree.
/// <see cref="Ancestry.Of(Dominators)"/> answers it in O(log NodeCount) and
/// <see cref="PreorderAncestry"/> in O(1). The analysis follows the edges against their direction, so
/// it takes a <see cref="BiAdjacentGraph"/>.
/// </para>
/// <para>
/// The post-dominators are the dominators of the transposed graph, with the exits as entries. A node
/// <c>p</c> post-dominates <c>n</c> if every path from <c>n</c> to an exit passes through <c>p</c>. A
/// node from which no exit can be reached, such as one in an endless loop, has no post-dominator,
/// unless an extra exit with an edge from that loop is added to the graph first.
/// </para>
/// <para>
/// Uses <see href="https://en.wikipedia.org/wiki/Dominator_(graph_theory)">the algorithm of Lengauer and Tarjan</see>,
/// without recursion. Dominance frontiers and control
/// dependence are in the package <c>Graphity.ControlFlow</c>.
/// </para>
/// <para>Instances are immutable and safe to share between threads.</para>
/// <para>
/// <b>Memory:</b> 4 x NodeCount bytes and 1 bit per node, plus the graph, which stays reachable as
/// long as the dominators are.
/// </para>
/// </remarks>
/// <example>
/// Here, the dominators of the basic blocks of the method <c>load</c> from its <c>entry</c>, and
/// whether <c>body</c> and <c>then</c> dominate <c>next</c>:
/// <!-- example: Dominance.cs#dominators -->
/// <code language="csharp">
/// Dominators dominators = Dominators.Of(BiAdjacentGraph.Of(cfg), entry);
/// // 2 - the block body
/// int idom = dominators.ImmediateDominatorOf(next);
/// Ancestry dominance = Ancestry.Of(dominators);
/// bool bodyDominatesNext = dominance.IsAncestorOf(body, next);      // true
/// bool thenDominatesNext = dominance.IsAncestorOf(then, next);      // false
/// </code>
/// </example>
/// <example>
/// Here, <c>next</c> is the first block that every path from <c>body</c> to the <c>exit</c> of
/// <c>load</c> passes through:
/// <!-- example: Dominance.cs#postDominators -->
/// <code language="csharp">
/// Dominators postDominators =
///     Dominators.Of(BiAdjacentGraph.Of(cfg).Transposed(), exit);
/// int join = postDominators.ImmediateDominatorOf(body);   // 4 - the block next
/// </code>
/// </example>
public sealed class Dominators
{
    // The unlinked marker in the ancestor lane of the path-compression forest.
    private const int Unlinked = -1;

    // The immediate dominator of each node, -1 for a root of the forest and for a node not reached.
    internal readonly int[] ImmediateDominator;

    // One bit per node, set for the nodes that an entry reaches.
    private readonly ulong[] _reachable;

    private Dominators(BiAdjacentGraph graph, int[] immediateDominator, ulong[] reachable)
    {
        Graph = graph;
        ImmediateDominator = immediateDominator;
        _reachable = reachable;
    }

    /// <summary>
    /// Returns the dominators of <paramref name="graph"/> with the single entry <paramref name="entry"/>.
    /// Equivalent to <c>Of(graph, [entry])</c>.
    /// </summary>
    /// <remarks>
    /// <b>Time:</b> O((NodeCount + EdgeCount) log NodeCount).<br/>
    /// <b>Memory:</b> 4 x NodeCount bytes and 1 bit per node for the result; while it runs, up to
    /// 32 x NodeCount bytes more.
    /// </remarks>
    /// <param name="graph">The graph.</param>
    /// <param name="entry">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The dominators.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="entry"/> is out of range.</exception>
    /// <exception cref="ArgumentException"><paramref name="graph"/> has more than 2^30 - 30 nodes.</exception>
    public static Dominators Of(BiAdjacentGraph graph, int entry)
    {
        ArgumentNullException.ThrowIfNull(graph);
        Checks.Index(entry, graph.NodeCount);
        return Of(graph, [entry]);
    }

    /// <summary>
    /// Returns the dominators of <paramref name="graph"/> with the given entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Several entries act like one virtual root above all of them, as the class documentation
    /// describes. Their order does not matter. An entry given twice counts once, and with no entries, no
    /// node is reachable.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(entries.Length + (NodeCount + EdgeCount) log NodeCount).<br/>
    /// <b>Memory:</b> 4 x NodeCount bytes and 1 bit per node for the result; while it runs, up to
    /// 32 x NodeCount bytes more.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph.</param>
    /// <param name="entries">Node IDs, each in <c>0..NodeCount-1</c>.</param>
    /// <returns>The dominators.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An entry is out of range.</exception>
    /// <exception cref="ArgumentException"><paramref name="graph"/> has more than 2^30 - 30 nodes.</exception>
    public static Dominators Of(BiAdjacentGraph graph, ReadOnlySpan<int> entries)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        var dfs = DfsPreorder.Of(graph, entries, "Dominators");
        int[] idom = LengauerTarjan(dfs, graph.InStart, graph.InAdj, false);

        // Back to node IDs; the virtual root becomes -1.
        int[] order = dfs.Order;
        var immediateDominator = new int[n];
        Array.Fill(immediateDominator, -1);
        var reachable = new ulong[(n >> 6) + 1];
        for (int v = 1; v <= dfs.ReachedCount; v++)
        {
            int node = order[v];
            reachable[node >> 6] |= 1UL << node;
            if (idom[v] != 0)
            {
                immediateDominator[node] = order[idom[v]];
            }
        }
        return new Dominators(graph, immediateDominator, reachable);
    }

    /// <summary>
    /// Immediate dominators of each block's induced subgraph, as node IDs, -1 for the entry of a
    /// block. A block is the set of nodes the search numbers from one entry. No entry may be reached
    /// by the search from an earlier one, and every node must be reached: one node of every SCC,
    /// sink components first, satisfies this.
    /// </summary>
    /// <exception cref="ArgumentException">The graph has more than <see cref="DfsPreorder.MaxNodeCount"/> nodes.</exception>
    internal static int[] WithinBlocks(BiAdjacentGraph graph, ReadOnlySpan<int> entries)
    {
        var dfs = DfsPreorder.Of(graph, entries, "Dominators");
        int[] idom = LengauerTarjan(dfs, graph.InStart, graph.InAdj, true);
        int[] order = dfs.Order;
        var immediateDominator = new int[graph.NodeCount];
        Array.Fill(immediateDominator, -1);
        for (int v = 1; v <= dfs.ReachedCount; v++)
        {
            if (idom[v] != 0)
            {
                immediateDominator[order[v]] = order[idom[v]];
            }
        }
        return immediateDominator;
    }

    /// <summary>
    /// Immediate dominators by preorder number, 0 for the virtual root. <paramref name="inStart"/>
    /// and <paramref name="inAdj"/> are the in-adjacency over node IDs, translated per edge into
    /// preorder numbers (0 = unreachable).
    /// </summary>
    /// <remarks>
    /// Step 1 settles a node once the search path from its semidominator down to it is linked: the
    /// node then gets its immediate dominator, or a node with the same one, which step 2 resolves.
    /// With <paramref name="withinBlocks"/>, predecessors outside the block of the node are ignored;
    /// a block is a contiguous range of preorder numbers that starts at its entry, and every
    /// predecessor in another block comes from a later one.
    /// </remarks>
    private static int[] LengauerTarjan(DfsPreorder dfs, int[] inStart, int[] inAdj, bool withinBlocks)
    {
        int size = dfs.ReachedCount + 1;
        int[] parent = dfs.Parent;
        int[] order = dfs.Order;
        int[] number = dfs.PreorderNumber;
        var semi = new int[size];
        // Two lanes per preorder number, ancestor and label of the path-compression forest: one
        // cache line serves both reads of a compression step.
        var lanes = new int[2 * size];
        // While a node waits in a bucket, its idom slot links it to the next node of the same bucket.
        var idom = new int[size];
        var bucket = new int[size];
        Array.Fill(bucket, -1);
        for (int v = 0; v < size; v++)
        {
            semi[v] = v;
            lanes[2 * v] = Unlinked;
            lanes[(2 * v) + 1] = v;
        }

        // 1) Reverse preorder: semidominator, link to the search parent, settle the parent's bucket.
        int blockEnd = size - 1;
        for (int v = size - 1; v >= 1; v--)
        {
            int best = v;
            int node = order[v];
            if (withinBlocks)
            {
                for (int i = inStart[node], end = inStart[node + 1]; i < end; i++)
                {
                    int u = number[inAdj[i]];
                    if (u > blockEnd)
                    {
                        continue;
                    }
                    int candidate = u < v ? u : semi[Eval(lanes, semi, u)];
                    if (candidate < best)
                    {
                        best = candidate;
                    }
                }
                if (dfs.IsEntry(v))
                {
                    blockEnd = v - 1;
                }
            }
            else
            {
                for (int i = inStart[node], end = inStart[node + 1]; i < end; i++)
                {
                    int u = number[inAdj[i]];
                    if (u == 0)
                    {
                        continue;
                    }
                    int candidate = u < v ? u : semi[Eval(lanes, semi, u)];
                    if (candidate < best)
                    {
                        best = candidate;
                    }
                }
            }
            int s = dfs.IsEntry(v) ? 0 : best;
            semi[v] = s;
            idom[v] = bucket[s];
            bucket[s] = v;
            int p = parent[v];
            lanes[2 * v] = p;
            for (int w = bucket[p]; w >= 0;)
            {
                int next = idom[w];
                int u = Eval(lanes, semi, w);
                idom[w] = semi[u] < semi[w] ? u : p;
                w = next;
            }
            bucket[p] = -1;
        }

        // 2) Preorder: a node settled with a deferred dominator takes that node's, already final.
        for (int v = 1; v < size; v++)
        {
            if (idom[v] != semi[v])
            {
                idom[v] = idom[idom[v]];
            }
        }
        return idom;
    }

    /// <summary>
    /// The node of least semidominator on the forest path from <paramref name="v"/> up to, but
    /// excluding, its tree root, compressing the path on the way.
    /// </summary>
    private static int Eval(int[] lanes, int[] semi, int v)
    {
        int ancestor = lanes[2 * v];
        if (ancestor == Unlinked)
        {
            return v;
        }
        if (lanes[2 * ancestor] != Unlinked)
        {
            Compress(lanes, semi, v);
        }
        return lanes[(2 * v) + 1];
    }

    /// <summary>
    /// Path compression without recursion and without a stack: the way up reverses the ancestor
    /// pointers, the way down restores them pointing at the tree root and carries the best label.
    /// </summary>
    private static void Compress(int[] lanes, int[] semi, int v)
    {
        int below = Unlinked;
        int x = v;
        while (lanes[2 * lanes[2 * x]] != Unlinked)
        {
            int up = lanes[2 * x];
            lanes[2 * x] = below;
            below = x;
            x = up;
        }
        while (below != Unlinked)
        {
            int y = below;
            below = lanes[2 * y];
            int xLabel = lanes[(2 * x) + 1];
            if (semi[xLabel] < semi[lanes[(2 * y) + 1]])
            {
                lanes[(2 * y) + 1] = xLabel;
            }
            lanes[2 * y] = lanes[2 * x];
            x = y;
        }
    }

    /// <summary>
    /// Gets the graph these dominators were computed on, for analyses that need both, such as dominance
    /// frontiers.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public BiAdjacentGraph Graph { get; }

    /// <summary>
    /// Returns the immediate dominator of <paramref name="node"/>, or -1 if it has none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A node has none if it is an entry, if only the virtual root above several entries dominates it,
    /// or if no entry reaches it.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The immediate dominator, or -1.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int ImmediateDominatorOf(int node)
    {
        Checks.Index(node, ImmediateDominator.Length);
        return ImmediateDominator[node];
    }

    /// <summary>Returns whether <paramref name="node"/> is reachable from an entry.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns><see langword="true"/> if a path leads from an entry to <paramref name="node"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public bool IsReachable(int node)
    {
        Checks.Index(node, ImmediateDominator.Length);
        return (_reachable[node >> 6] & (1UL << node)) != 0;
    }
}
