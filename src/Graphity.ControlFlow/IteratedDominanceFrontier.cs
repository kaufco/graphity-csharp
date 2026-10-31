namespace Graphity.ControlFlow;

/// <summary>
/// The iterated dominance frontiers of a control-flow graph, which tell where SSA (static single
/// assignment) form places phi-nodes.
/// </summary>
/// <remarks>
/// <para>
/// In SSA form, every assignment to a variable creates a new version of it, and a phi-node merges the
/// versions where paths of control flow meet. The phi-nodes of a variable go into the iterated
/// dominance frontier of the blocks that assign it: the dominance frontier of these blocks, then the
/// dominance frontier of the result, and so on until nothing new is added. A compiler asks this once
/// per variable, so <see cref="Of"/> prepares the queries once per control-flow graph, and
/// <see cref="FrontierOf"/> answers them.
/// </para>
/// <para>
/// Uses the algorithm of Sreedhar and Gao. A query walks the subtrees of the dominator tree below the
/// nodes it is given and the nodes it adds, so its cost depends on the part of the graph it explores,
/// not on the whole graph. It never builds the dominance frontiers themselves, which can grow
/// quadratically with the node count; <see cref="ControlFlow.DominanceFrontiers"/> returns them as a
/// graph.
/// </para>
/// <para>Instances are immutable and safe to share between threads.</para>
/// <para>
/// <b>Memory:</b> 16 x NodeCount bytes, plus the <see cref="Dominators"/> it was built from, which
/// stay reachable as long as it is held.
/// </para>
/// </remarks>
/// <example>
/// Here, for the loop variable <c>i</c> of a method <c>load</c>, whose control flow <c>cfg</c> has the
/// blocks <c>entry</c> and <c>next</c>:
/// <!-- example: AnalyzingControlFlow.cs#frontierOf -->
/// <code language="csharp">
/// Dominators dominators = Dominators.Of(BiAdjacentGraph.Of(cfg), entry);
/// IteratedDominanceFrontier phis = IteratedDominanceFrontier.Of(dominators);
/// // i is assigned in entry (i = 0) and in next (i++)
/// SortedIntView phiBlocks = phis.FrontierOf([entry, next]);
/// // phiBlocks: [1] - header
/// </code>
/// </example>
public sealed class IteratedDominanceFrontier
{
    private readonly Dominators _dominators;

    // The depth of each node in the dominator forest, 0 for a root, -1 if no entry reaches it.
    private readonly int[] _depth;

    // The position of each node in the preorder of the dominator forest.
    private readonly int[] _preorderIndex;

    // The size of the subtree of each node in the dominator forest, the node included.
    private readonly int[] _subtreeSize;

    // The node at each preorder position.
    private readonly int[] _order;

    private IteratedDominanceFrontier(Dominators dominators, int[] depth, int[] preorderIndex, int[] subtreeSize, int[] order)
    {
        _dominators = dominators;
        _depth = depth;
        _preorderIndex = preorderIndex;
        _subtreeSize = subtreeSize;
        _order = order;
    }

    /// <summary>
    /// Returns the iterated dominance frontiers of the graph that <paramref name="dominators"/> were
    /// computed on, ready for queries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// They are prepared anew on every call, not cached: keep the result and query it for every variable.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount).<br/>
    /// <b>Memory:</b> 16 x NodeCount bytes for the result; while it runs, 8 x NodeCount bytes more.
    /// </para>
    /// </remarks>
    /// <param name="dominators">The dominators of the control-flow graph.</param>
    /// <returns>The iterated dominance frontiers.</returns>
    public static IteratedDominanceFrontier Of(Dominators dominators)
    {
        ArgumentNullException.ThrowIfNull(dominators);
        Graph tree = Graphs.DominatorTree(dominators);
        int n = tree.NodeCount;
        ReadOnlySpan<int> childStart = Csr.OutAdjacencyStart(tree);
        ReadOnlySpan<int> children = Csr.OutAdjacency(tree);
        var preorderIndex = new int[n];
        var order = new int[n];
        var subtreeSize = new int[n];
        var depth = new int[n];

        // Canonical preorder - roots ascending, children ascending - as ParentForest.Preorder in the
        // core computes it; change both together. Every node is pushed exactly once, so the stack fits
        // in n slots; the subtree-size array serves as the stack until it is filled below. Preorder
        // visits every parent before its children, so depth follows on the way.
        int[] stack = subtreeSize;
        int top = 0;
        for (int node = n - 1; node >= 0; node--)
        {
            if (dominators.ImmediateDominatorOf(node) < 0)
            {
                stack[top++] = node;
                depth[node] = dominators.IsReachable(node) ? 0 : -1;
            }
        }
        int position = 0;
        while (top > 0)
        {
            int node = stack[--top];
            preorderIndex[node] = position;
            order[position++] = node;
            for (int i = childStart[node + 1] - 1; i >= childStart[node]; i--)
            {
                int child = children[i];
                depth[child] = depth[node] + 1;
                stack[top++] = child;
            }
        }

        // Reverse preorder visits every child before its parent.
        Array.Fill(subtreeSize, 1);
        for (int i = n - 1; i >= 0; i--)
        {
            int node = order[i];
            int p = dominators.ImmediateDominatorOf(node);
            if (p >= 0)
            {
                subtreeSize[p] += subtreeSize[node];
            }
        }
        return new IteratedDominanceFrontier(dominators, depth, preorderIndex, subtreeSize, order);
    }

    /// <summary>
    /// Returns the iterated dominance frontier of <paramref name="nodes"/>, in ascending order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The result can contain given nodes, such as the header of a natural loop, which is in its own
    /// dominance frontier. Nodes that the entries do not reach are ignored, and so are duplicates.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(nodes.Length + e + (s + k) log(s + k)), where e is the number of nodes and edges
    /// the query explores, at most NodeCount + EdgeCount; s is the number of distinct given nodes that
    /// the entries reach, and k the size of the result.<br/>
    /// <b>Memory:</b> 4 x k bytes for the result; while it runs, up to about 60 bytes per node the query
    /// explores, and nothing sized by the whole graph.
    /// </para>
    /// </remarks>
    /// <param name="nodes">Node IDs in <c>0..NodeCount-1</c>, such as the blocks that assign a variable.</param>
    /// <returns>The iterated dominance frontier.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A node is out of range.</exception>
    /// <exception cref="ArgumentException">The query explores more than 805,306,368 nodes.</exception>
    public SortedIntView FrontierOf(ReadOnlySpan<int> nodes)
    {
        int n = _depth.Length;
        int size = nodes.Length;
        // Sized by a capped guess and grown on demand, so that duplicates and unreachable nodes in a
        // long input cost no scratch space.
        int initial = Math.Min(Math.Max(4, size), 1024);
        var given = new IntOpenHashSet(initial);
        var heap = new int[2 * initial];
        int heapSize = 0;
        for (int i = 0; i < size; i++)
        {
            int node = nodes[i];
            Checks.Index(node, n, nameof(nodes));
            if (_depth[node] < 0 || !given.Add(node))
            {
                continue;
            }
            heap = Push(heap, heapSize++, _depth[node], node, n);
        }

        ReadOnlySpan<int> outStart = Csr.OutAdjacencyStart(_dominators.Graph);
        ReadOnlySpan<int> adj = Csr.OutAdjacency(_dominators.Graph);
        var inFrontier = new IntOpenHashSet();
        var walked = new IntOpenHashSet();
        var result = new IntArrayList();

        // Deepest root first. A subtree walked once need not be walked again for a shallower root:
        // every edge it could contribute leaves no deeper than the earlier root, and was taken then.
        // Walked parts are closed under descendants, so the scan skips each one in a single step.
        while (heapSize > 0)
        {
            int rootDepth = heap[0];
            int root = heap[1];
            heapSize = Pop(heap, heapSize);
            int position = _preorderIndex[root];
            int end = position + _subtreeSize[root];
            while (position < end)
            {
                int x = _order[position];
                if (!walked.Add(x))
                {
                    position += _subtreeSize[x];
                    continue;
                }
                for (int i = outStart[x], last = outStart[x + 1]; i < last; i++)
                {
                    int y = adj[i];
                    // Targets deeper than the root lie strictly inside its subtree and fall out here;
                    // what remains - the root itself or a node beyond its subtree - is in its frontier.
                    int yDepth = _depth[y];
                    if (yDepth > rootDepth || !inFrontier.Add(y))
                    {
                        continue;
                    }
                    result.Add(y);
                    if (!given.Contains(y))
                    {
                        heap = Push(heap, heapSize++, yDepth, y, n);
                    }
                }
                position++;
            }
        }

        int[] sorted = result.ToArray();
        Array.Sort(sorted);
        return SortedIntView.ViewOf(sorted, 0, sorted.Length);
    }

    /// <summary>
    /// Adds <c>(key, node)</c> to the binary max-heap of interleaved pairs holding
    /// <paramref name="size"/> entries, growing the array if needed, and returns the array. Every node
    /// enters at most once, so the array never needs more than two slots per node of the graph, and
    /// the growth is capped there instead of doubling past <see cref="int.MaxValue"/>.
    /// </summary>
    private static int[] Push(int[] heap, int size, int key, int node, int nodeCount)
    {
        if (2 * (size + 1) > heap.Length)
        {
            Array.Resize(ref heap, (int)Math.Min(2L * heap.Length, 2L * nodeCount));
        }
        int i = size;
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            if (heap[2 * parent] >= key)
            {
                break;
            }
            heap[2 * i] = heap[2 * parent];
            heap[(2 * i) + 1] = heap[(2 * parent) + 1];
            i = parent;
        }
        heap[2 * i] = key;
        heap[(2 * i) + 1] = node;
        return heap;
    }

    /// <summary>Removes the top of the heap holding <paramref name="size"/> entries and returns the new size.</summary>
    private static int Pop(int[] heap, int size)
    {
        int last = size - 1;
        int key = heap[2 * last];
        int node = heap[(2 * last) + 1];
        int i = 0;
        while (true)
        {
            int child = (2 * i) + 1;
            if (child >= last)
            {
                break;
            }
            if (child + 1 < last && heap[2 * (child + 1)] > heap[2 * child])
            {
                child++;
            }
            if (heap[2 * child] <= key)
            {
                break;
            }
            heap[2 * i] = heap[2 * child];
            heap[(2 * i) + 1] = heap[(2 * child) + 1];
            i = child;
        }
        heap[2 * i] = key;
        heap[(2 * i) + 1] = node;
        return last;
    }
}
