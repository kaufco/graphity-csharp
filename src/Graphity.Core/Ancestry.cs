using System.Globalization;

namespace Graphity;

/// <summary>
/// An index of a forest for ancestor queries: the parent and depth of a node, whether one node is an
/// ancestor of another, and the lowest common ancestor of two nodes.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Of(Graph)"/> builds the index of a forest whose edges point from parent to child, such
/// as a syntax tree. For a forest whose edges point from child to parent, pass the transposed graph:
/// <c>Ancestry.Of(BiAdjacentGraph.Of(forest).Transposed())</c>. <see cref="Of(Dominators)"/> builds
/// the index of a dominator tree, in which the ancestors of a node are its dominators. The index is
/// built once and answers each query in O(log NodeCount) at most, however deep the forest;
/// <see cref="PreorderAncestry.Of"/> adds an ancestor test in O(1) and the nodes of each subtree.
/// </para>
/// <para>
/// A node is its own ancestor, so <c>IsAncestorOf(a, a)</c> is <see langword="true"/> and
/// <c>LowestCommonAncestorOf(a, a)</c> is <c>a</c>. Two nodes in different trees of the forest have
/// no common ancestor. Uses <see href="https://en.wikipedia.org/wiki/Heavy-light_decomposition">heavy-light decomposition</see>.
/// </para>
/// <para>
/// Instances are immutable and safe to share between threads. No other assembly can derive from this
/// class; <see cref="PreorderAncestry"/> is its only subclass.
/// </para>
/// <para>
/// <b>Memory:</b> 12 x NodeCount bytes, of which an index built by <see cref="Of(Dominators)"/>
/// shares 4 x NodeCount bytes with the dominators.
/// </para>
/// </remarks>
public class Ancestry
{
    // The parent of each node, -1 for the root of a tree.
    internal readonly int[] Parent;

    // The number of edges from the root of its tree to each node, 0 for a root.
    internal readonly int[] Depth;

    // The topmost node of the heavy chain of each node. Jumping from a node to
    // Parent[ChainHead[node]] skips a whole chain, and every path from a root to a node crosses
    // O(log NodeCount) chains.
    internal readonly int[] ChainHead;

    internal Ancestry(int[] parent, int[] depth, int[] chainHead)
    {
        Parent = parent;
        Depth = depth;
        ChainHead = chainHead;
    }

    /// <summary>
    /// Returns the ancestor index of <paramref name="forest"/>, whose edges point from parent to child.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In such a forest, no node has more than one in-node, and there is no cycle, not even a self-loop;
    /// <see cref="Graphs.IsForest"/> tests this without an exception. The roots of the trees are the root
    /// nodes of the graph.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 12 x NodeCount bytes for the result; while it runs, 12 x NodeCount bytes more.
    /// </para>
    /// </remarks>
    /// <param name="forest">A forest whose edges point from parent to child.</param>
    /// <returns>The ancestor index.</returns>
    /// <exception cref="ArgumentException">
    /// A node of <paramref name="forest"/> has more than one in-node, or <paramref name="forest"/> has a
    /// cycle.
    /// </exception>
    public static Ancestry Of(Graph forest)
    {
        ArgumentNullException.ThrowIfNull(forest);
        // The forest check is that of Graphs.ComputeForestInfo, here with witnesses for the
        // messages; a change to what counts as a forest belongs in both.
        int n = forest.NodeCount;
        int[] adjacency = forest.OutAdj;
        int[] adjacencyStart = forest.OutStart;

        // Parent per node. A second parent means in-degree > 1, which no forest has.
        var parent = new int[n];
        Array.Fill(parent, -1);
        for (int node = 0; node < n; node++)
        {
            for (int i = adjacencyStart[node], end = adjacencyStart[node + 1]; i < end; i++)
            {
                int child = adjacency[i];
                if (parent[child] >= 0)
                {
                    throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                        $"not a forest: node {child} has more than one parent "
                        + $"({parent[child]} and {node})"), nameof(forest));
                }
                parent[child] = node;
            }
        }

        // BFS from all roots. With in-degree <= 1 every node is enqueued at most once, so the queue
        // doubles as a topological order: parents before children. A node it misses lies on a cycle, a
        // self-loop included, or below one.
        var depth = new int[n];
        Array.Fill(depth, -1);
        var order = new int[n];
        int head = 0;
        int tail = 0;
        for (int node = 0; node < n; node++)
        {
            if (parent[node] < 0)
            {
                depth[node] = 0;
                order[tail++] = node;
            }
        }
        while (head < tail)
        {
            int node = order[head++];
            for (int i = adjacencyStart[node], end = adjacencyStart[node + 1]; i < end; i++)
            {
                int child = adjacency[i];
                depth[child] = depth[node] + 1;
                order[tail++] = child;
            }
        }
        if (tail != n)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"not a forest: node {NodeOnCycle(parent, depth)} lies on a cycle"), nameof(forest));
        }
        return WithHeavyChains(parent, depth, order);
    }

    /// <summary>
    /// Returns the ancestor index of the dominator tree of <paramref name="dominators"/>, in which the
    /// ancestors of a node are its dominators.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>IsAncestorOf(a, b)</c> returns whether <c>a</c> dominates <c>b</c>.
    /// <c>LowestCommonAncestorOf(a, b)</c> returns the closest node that dominates both, or -1 if no node
    /// does. With several entries, only a virtual root lies above them. A node that no entry reaches is a
    /// tree of its own and dominates only itself here; <see cref="Dominators.IsReachable"/> returns
    /// <see langword="false"/> for such a node.
    /// </para>
    /// <para>
    /// The result is the same as that of <c>Ancestry.Of(Graphs.DominatorTree(dominators))</c>, but shares
    /// the immediate dominators instead of building the tree as a graph.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount).<br/>
    /// <b>Memory:</b> 8 x NodeCount bytes for the result, which shares the immediate dominators; while it
    /// runs, 20 x NodeCount bytes more.
    /// </para>
    /// </remarks>
    /// <param name="dominators">The dominators whose tree to index.</param>
    /// <returns>The ancestor index of the dominator tree.</returns>
    public static Ancestry Of(Dominators dominators)
    {
        ArgumentNullException.ThrowIfNull(dominators);
        int[] parent = dominators.ImmediateDominator;
        int n = parent.Length;

        // Children per node, then a BFS from all roots for a parents-first order and the depths.
        var forest = ParentForest.ChildrenOf(parent);
        int[] childStart = forest.Start;
        int[] children = forest.Members;
        var depth = new int[n];
        var order = new int[n];
        int head = 0;
        int tail = 0;
        for (int node = 0; node < n; node++)
        {
            if (parent[node] < 0)
            {
                order[tail++] = node;
            }
        }
        while (head < tail)
        {
            int node = order[head++];
            for (int i = childStart[node], end = childStart[node + 1]; i < end; i++)
            {
                int child = children[i];
                depth[child] = depth[node] + 1;
                order[tail++] = child;
            }
        }
        return WithHeavyChains(parent, depth, order);
    }

    // Completes the index from parents, depths and an order in which every parent comes before its
    // children.
    private static Ancestry WithHeavyChains(int[] parent, int[] depth, int[] order)
    {
        int n = parent.Length;

        // Subtree sizes, then the heaviest child per node; one backward pass accumulates the sizes.
        var subtreeSize = new int[n];
        Array.Fill(subtreeSize, 1);
        var heavyChild = new int[n];
        Array.Fill(heavyChild, -1);
        for (int i = n - 1; i >= 0; i--)
        {
            int node = order[i];
            int nodeParent = parent[node];
            if (nodeParent < 0)
            {
                continue;
            }
            subtreeSize[nodeParent] += subtreeSize[node];
            int rival = heavyChild[nodeParent];
            if (rival < 0 || subtreeSize[node] > subtreeSize[rival])
            {
                heavyChild[nodeParent] = node;
            }
        }

        // Chain heads, forwards: a heavy child continues its parent's chain, every other node starts one.
        var chainHead = new int[n];
        for (int i = 0; i < n; i++)
        {
            int node = order[i];
            int nodeParent = parent[node];
            chainHead[node] = nodeParent >= 0 && heavyChild[nodeParent] == node ? chainHead[nodeParent] : node;
        }
        return new Ancestry(parent, depth, chainHead);
    }

    // A node that lies on a cycle, not merely one hanging below it: walking up from an unreached node
    // stays unreached, and after NodeCount steps the walk is on the cycle it circles.
    private static int NodeOnCycle(int[] parent, int[] depth)
    {
        int n = depth.Length;
        int node = Array.IndexOf(depth, -1);
        for (int step = 0; step < n; step++)
        {
            node = parent[node];
        }
        return node;
    }

    /// <summary>
    /// Returns the lowest common ancestor of <paramref name="a"/> and <paramref name="b"/>, or -1 if they
    /// are in different trees.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lowest common ancestor is the deepest node that is an ancestor of both. As a node is its own
    /// ancestor, the result is <paramref name="a"/> if <paramref name="a"/> is an ancestor of
    /// <paramref name="b"/>.
    /// </para>
    /// <para><b>Time:</b> O(log NodeCount).</para>
    /// </remarks>
    /// <param name="a">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <param name="b">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The lowest common ancestor, or -1.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="a"/> or <paramref name="b"/> is out of range.</exception>
    public int LowestCommonAncestorOf(int a, int b)
    {
        Checks.Index(a, Parent.Length);
        Checks.Index(b, Parent.Length);
        // Lift whichever node sits on the deeper chain to its chain head's parent, until both are on
        // one chain; then the shallower of the two is the ancestor of the other.
        while (ChainHead[a] != ChainHead[b])
        {
            if (Depth[ChainHead[a]] < Depth[ChainHead[b]])
            {
                b = Parent[ChainHead[b]];
                if (b < 0)
                {
                    return -1;
                }
            }
            else
            {
                a = Parent[ChainHead[a]];
                if (a < 0)
                {
                    return -1;
                }
            }
        }
        return Depth[a] < Depth[b] ? a : b;
    }

    /// <summary>
    /// Returns whether <paramref name="node"/> is an ancestor of <paramref name="descendant"/>: whether
    /// it lies on the path from the root of its tree to <paramref name="descendant"/>. Equivalent to
    /// <c>LowestCommonAncestorOf(node, descendant) == node</c>.
    /// </summary>
    /// <remarks>
    /// <para>A node is its own ancestor. <see cref="PreorderAncestry"/> answers in O(1).</para>
    /// <para><b>Time:</b> O(log NodeCount).</para>
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <param name="descendant">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="node"/> is an ancestor of <paramref name="descendant"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="node"/> or <paramref name="descendant"/> is out of range.
    /// </exception>
    public virtual bool IsAncestorOf(int node, int descendant) => LowestCommonAncestorOf(node, descendant) == node;

    /// <summary>
    /// Returns the depth of <paramref name="node"/>: the number of edges from the root of its tree to it.
    /// A root has depth 0.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The depth.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int DepthOf(int node)
    {
        Checks.Index(node, Parent.Length);
        return Depth[node];
    }

    /// <summary>
    /// Returns the parent of <paramref name="node"/>, or -1 if it is the root of its tree.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The parent, or -1.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int ParentOf(int node)
    {
        Checks.Index(node, Parent.Length);
        return Parent[node];
    }
}
