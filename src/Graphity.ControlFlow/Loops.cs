namespace Graphity.ControlFlow;

/// <summary>
/// The loop nesting forest of a control-flow graph: its loops, how they nest and where each of them is
/// entered, irreducible loops included.
/// </summary>
/// <remarks>
/// <para>
/// A loop has a header, through which it is normally entered, and a body: the header, the other nodes
/// of the loop and those of the loops nested in it. <see cref="Of(BiAdjacentGraph, int)"/> finds the
/// loops from the entry of the graph. The forest is defined on every graph, reducible or not, over the
/// nodes that the entries reach:
/// </para>
/// <list type="bullet">
///   <item><description>
///   The outermost loops are the <see cref="Sccs">strongly connected components</see> that contain at
///   least one edge.
///   </description></item>
///   <item><description>
///   The header of a loop is the node of its component that a depth-first search from the entries
///   reaches first. The search takes the entries in list order and the out-nodes of a node in
///   ascending order.
///   </description></item>
///   <item><description>
///   Without the edges that lead into the header from inside the component, the rest of it breaks up
///   into the nested loops, by the same rule.
///   </description></item>
/// </list>
/// <para>
/// Uses Havlak's algorithm, without recursion. On a reducible graph, the loops are the natural loops.
/// An irreducible loop is a cycle that can be entered at more than one node, as <c>goto</c>, bytecode
/// or generated state machines produce it. It is one loop with several
/// <see cref="EntriesOf">entries</see>, and which of them is the header depends on the order of the
/// search. An entry of the graph that lies in a loop is an entry of that loop. A node with a self-loop
/// is the header of a loop of its own, unless it already is the header of a larger one. Nodes that no
/// entry reaches are in no loop.
/// </para>
/// <para>
/// Loops have IDs <c>0..LoopCount-1</c>, in preorder of the forest: a loop has a smaller ID than the
/// loops nested in it, which follow it as one range of IDs. Going through the IDs downwards visits the
/// inner loops first.
/// </para>
/// <para>
/// Like <see cref="Dominators"/>, the analysis follows edges against their direction and so takes a
/// <see cref="BiAdjacentGraph"/>; the same graph serves both. Instances are immutable and safe to
/// share between threads.
/// </para>
/// <para>
/// <b>Memory:</b> 4 x NodeCount bytes, plus 4 bytes per node in a loop, 24 bytes per loop and 4 bytes
/// per entry of a loop.
/// </para>
/// </remarks>
/// <example>
/// Here, the loop of a method <c>load</c> around its block <c>then</c>, from its control flow
/// <c>cfg</c> and its entry block <c>entry</c>:
/// <!-- example: AnalyzingControlFlow.cs#loops -->
/// <code language="csharp">
/// // The basic blocks of load are the nodes 0..5 of cfg: entry, header, body, then,
/// // next, exit.
///
/// // Find the loops of load, from its entry block.
/// Loops loops = Loops.Of(BiAdjacentGraph.Of(cfg), entry);
///
/// // Find the loop around the block then, with its header and its body.
/// int loop = loops.InnermostLoopOf(then);         // 0 - the for loop, the only loop
/// int header = loops.HeaderOf(loop);              // 1 - the block header
/// // [1, 2, 3, 4] - header, body, then and next
/// ReadOnlySpan&lt;int> blocks = loops.BodyOf(loop);
/// </code>
/// </example>
public sealed class Loops
{
    // The innermost loop of each node, -1 outside every loop.
    private readonly int[] _innermostLoop;

    // The header of each loop.
    private readonly int[] _header;

    // The loop that immediately encloses each loop, -1 for an outermost loop.
    private readonly int[] _parent;

    // One past the last loop nested in each loop: the loops in L are L.._subtreeEnd[L] - 1.
    private readonly int[] _subtreeEnd;

    // The number of loops that contain each loop, itself included.
    private readonly int[] _depth;

    // The start of the own nodes of each loop in _bodies; the body of L with its nested loops is
    // _bodies[_bodyStart[L].._bodyStart[_subtreeEnd[L]] - 1].
    private readonly int[] _bodyStart;

    // All bodies in the order of the loop IDs, the own nodes of each loop with the header first, then
    // ascending.
    private readonly int[] _bodies;

    // The entries of each loop in the layout of the out-adjacency of a Graph, each list ascending.
    private readonly int[] _entryStart;

    private readonly int[] _entries;

    private Loops(int[] innermostLoop, int[] header, int[] parent, int[] subtreeEnd, int[] depth,
        int[] bodyStart, int[] bodies, int[] entryStart, int[] entries, bool reducible)
    {
        _innermostLoop = innermostLoop;
        _header = header;
        _parent = parent;
        _subtreeEnd = subtreeEnd;
        _depth = depth;
        _bodyStart = bodyStart;
        _bodies = bodies;
        _entryStart = entryStart;
        _entries = entries;
        IsReducible = reducible;
    }

    /// <summary>
    /// Returns the loop nesting forest of <paramref name="graph"/> with one entry. Equivalent to
    /// <c>Of(graph, [entry])</c>.
    /// </summary>
    /// <remarks>
    /// <b>Time:</b> as for <see cref="Of(BiAdjacentGraph, ReadOnlySpan{int})"/>.<br/>
    /// <b>Memory:</b> as for <see cref="Of(BiAdjacentGraph, ReadOnlySpan{int})"/>.
    /// </remarks>
    /// <param name="graph">The control-flow graph.</param>
    /// <param name="entry">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The loop nesting forest.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="entry"/> is out of range.</exception>
    /// <exception cref="ArgumentException"><paramref name="graph"/> has more than 2^30 - 30 nodes.</exception>
    public static Loops Of(BiAdjacentGraph graph, int entry)
    {
        ArgumentNullException.ThrowIfNull(graph);
        Checks.Index(entry, graph.NodeCount);
        return Of(graph, [entry]);
    }

    /// <summary>
    /// Returns the loop nesting forest of <paramref name="graph"/> with the given entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The entries work as in <see cref="Dominators.Of(BiAdjacentGraph, ReadOnlySpan{int})"/>: an entry
    /// given twice counts once, and with no entries, no node is reachable, so there is no loop. Their
    /// order matters here: it decides which node of an irreducible loop the search reaches first, and so
    /// which node is its header.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(entries.Length + (NodeCount + EdgeCount) log NodeCount) on a reducible graph. On an
    /// irreducible graph, an edge costs one more step for every loop it enters other than through the
    /// header.<br/>
    /// <b>Memory:</b> the result, as given in the class documentation; while it runs, about
    /// 45 x NodeCount + 4 x EdgeCount bytes more, plus 28 bytes per loop and 12 bytes per entry of a loop.
    /// </para>
    /// </remarks>
    /// <param name="graph">The control-flow graph.</param>
    /// <param name="entries">Node IDs in <c>0..NodeCount-1</c>, in the order the search takes them.</param>
    /// <returns>The loop nesting forest.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An entry is out of range.</exception>
    /// <exception cref="ArgumentException"><paramref name="graph"/> has more than 2^30 - 30 nodes.</exception>
    public static Loops Of(BiAdjacentGraph graph, ReadOnlySpan<int> entries)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        var dfs = DfsPreorder.Of(graph, entries, "Loops");
        int size = dfs.ReachedCount + 1;
        int[] order = dfs.Order;
        int[] number = dfs.PreorderNumber;

        // 1) Last preorder number in each search subtree: v's subtree is [v, last[v]].
        var last = new int[size];
        for (int v = 0; v < size; v++)
        {
            last[v] = v;
        }
        for (int v = size - 1; v >= 1; v--)
        {
            int p = dfs.Parent[v];
            if (p > 0 && last[v] > last[p])
            {
                last[p] = last[v];
            }
        }

        // Predecessors from the in-adjacency over node IDs, translated per edge through `number`
        // (0 = unreachable).
        ReadOnlySpan<int> predStart = Csr.InAdjacencyStart(graph);
        ReadOnlySpan<int> preds = Csr.InAdjacency(graph);

        // 2) Headers in reverse preorder, inner before outer. The body of w is the part of its
        //    search subtree that reaches w inside that subtree. Union-find collapses finished loops
        //    into their header; the edges that enter a collapsed region from outside wait in a
        //    linked list per region, so an enclosing header scans each of them once instead of
        //    rescanning every predecessor of every node inside.
        var uf = new int[size];
        for (int v = 0; v < size; v++)
        {
            uf[v] = v;
        }
        var enclosing = new int[size];
        var mark = new int[size];
        var pendingHead = new int[size];
        var pendingTail = new int[size];
        Array.Fill(pendingHead, -1);
        Array.Fill(pendingTail, -1);
        var pendingNext = new int[preds.Length];
        var stack = new int[size];
        var body = new int[size];
        var isHeader = new bool[size];
        int loopCount = 0;

        for (int w = size - 1; w >= 1; w--)
        {
            int lastW = last[w];
            bool selfLoop = false;
            int top = 0;
            int node = order[w];
            for (int i = predStart[node], end = predStart[node + 1]; i < end; i++)
            {
                int v = number[preds[i]];
                if (v == 0)
                {
                    continue;
                }
                if (v >= w && v <= lastW)
                {
                    if (v == w)
                    {
                        selfLoop = true;
                    }
                    else
                    {
                        int r = Find(uf, v);
                        if (mark[r] != w)
                        {
                            mark[r] = w;
                            stack[top++] = r;
                        }
                    }
                }
                else
                {
                    Append(pendingHead, pendingTail, pendingNext, w, i);
                }
            }
            if (top == 0 && !selfLoop)
            {
                continue;
            }

            isHeader[w] = true;
            loopCount++;
            int bodyCount = 0;
            while (top > 0)
            {
                int x = stack[--top];
                body[bodyCount++] = x;
                int i = pendingHead[x];
                pendingHead[x] = -1;
                pendingTail[x] = -1;
                while (i >= 0)
                {
                    int next = pendingNext[i];
                    int r = Find(uf, number[preds[i]]);
                    if (r >= w && r <= lastW)
                    {
                        if (r != w && mark[r] != w)
                        {
                            mark[r] = w;
                            stack[top++] = r;
                        }
                    }
                    else
                    {
                        Append(pendingHead, pendingTail, pendingNext, w, i);
                    }
                    i = next;
                }
            }
            for (int b = 0; b < bodyCount; b++)
            {
                uf[body[b]] = w;
                enclosing[body[b]] = w;
            }
        }

        // 3) Loop IDs in preorder of the forest; siblings by the preorder of their headers.
        // The child lists and the walk are ParentForest.GroupCounted and ParentForest.Preorder of the
        // core, which a module cannot call; change them together.
        // mark and stack are free now: mark maps each header to its index, stack walks the forest.
        int[] loopIndex = mark;
        Array.Fill(loopIndex, -1);
        var indexHeader = new int[loopCount];
        int k = 0;
        for (int v = 1; v < size; v++)
        {
            if (isHeader[v])
            {
                loopIndex[v] = k;
                indexHeader[k++] = v;
            }
        }
        var childStart = new int[loopCount + 1];
        var indexParent = new int[loopCount];
        for (int i = 0; i < loopCount; i++)
        {
            int e = enclosing[indexHeader[i]];
            indexParent[i] = e == 0 ? -1 : loopIndex[e];
            if (indexParent[i] >= 0)
            {
                childStart[indexParent[i]]++;
            }
        }
        int nextChildStart = 0;
        for (int i = 0; i < loopCount; i++)
        {
            int count = childStart[i];
            childStart[i] = nextChildStart;
            nextChildStart += count;
        }
        childStart[loopCount] = nextChildStart;
        var children = new int[nextChildStart];
        // childStart[p] doubles as write pointer and is shifted back afterwards.
        for (int i = 0; i < loopCount; i++)
        {
            if (indexParent[i] >= 0)
            {
                children[childStart[indexParent[i]]++] = i;
            }
        }
        for (int i = loopCount; i > 0; i--)
        {
            childStart[i] = childStart[i - 1];
        }
        childStart[0] = 0;

        var idOfIndex = new int[loopCount];
        var header = new int[loopCount];
        var parent = new int[loopCount];
        var depth = new int[loopCount];
        var subtreeEnd = new int[loopCount];
        int id = 0;
        int forestTop = 0;
        for (int i = loopCount - 1; i >= 0; i--)
        {
            if (indexParent[i] < 0)
            {
                stack[forestTop++] = i;
            }
        }
        while (forestTop > 0)
        {
            int i = stack[--forestTop];
            idOfIndex[i] = id;
            header[id] = order[indexHeader[i]];
            parent[id] = indexParent[i] < 0 ? -1 : idOfIndex[indexParent[i]];
            depth[id] = parent[id] < 0 ? 1 : depth[parent[id]] + 1;
            id++;
            for (int c = childStart[i + 1] - 1; c >= childStart[i]; c--)
            {
                stack[forestTop++] = children[c];
            }
        }
        for (int l = 0; l < loopCount; l++)
        {
            subtreeEnd[l] = l + 1;
        }
        for (int l = loopCount - 1; l >= 0; l--)
        {
            if (parent[l] >= 0 && subtreeEnd[l] > subtreeEnd[parent[l]])
            {
                subtreeEnd[parent[l]] = subtreeEnd[l];
            }
        }

        // 4) Innermost loop per node: its own loop for a header, else the loop that collapsed it.
        var innermostLoop = new int[n];
        Array.Fill(innermostLoop, -1);
        for (int v = 1; v < size; v++)
        {
            int h = isHeader[v] ? v : enclosing[v];
            if (h != 0)
            {
                innermostLoop[order[v]] = idOfIndex[loopIndex[h]];
            }
        }

        // 5) Bodies: each loop's own nodes, header first, then ascending; loops in ID order, so the
        //    nested loops of L follow its own nodes and the whole body is one slice.
        var bodyStart = new int[loopCount + 1];
        for (int node = 0; node < n; node++)
        {
            if (innermostLoop[node] >= 0)
            {
                bodyStart[innermostLoop[node]]++;
            }
        }
        int nextBodyStart = 0;
        for (int l = 0; l < loopCount; l++)
        {
            int count = bodyStart[l];
            bodyStart[l] = nextBodyStart;
            nextBodyStart += count;
        }
        bodyStart[loopCount] = nextBodyStart;
        var bodies = new int[nextBodyStart];
        int[] bodyWritePos = bodyStart[..loopCount];
        for (int l = 0; l < loopCount; l++)
        {
            bodies[bodyWritePos[l]++] = header[l];
        }
        for (int node = 0; node < n; node++)
        {
            int l = innermostLoop[node];
            if (l >= 0 && header[l] != node)
            {
                bodies[bodyWritePos[l]++] = node;
            }
        }

        // 6) Entries: x enters every loop that contains x but not one of its predecessors - a chain
        //    from the innermost loop of x upwards. Walking each chain only above the part already
        //    marked keeps this proportional to the result. A graph entry has the virtual root as
        //    predecessor, which no loop contains.
        var entryLoops = new IntArrayList();
        var entryNodes = new IntArrayList();
        for (int x = 0; x < n; x++)
        {
            int lx = innermostLoop[x];
            if (lx < 0)
            {
                continue;
            }
            int v = number[x];
            int marked = -1;
            if (dfs.IsEntry(v))
            {
                for (int l = lx; l >= 0; l = parent[l])
                {
                    entryLoops.Add(l);
                    entryNodes.Add(x);
                }
                continue;
            }
            for (int i = predStart[x], end = predStart[x + 1]; i < end; i++)
            {
                int y = preds[i];
                if (number[y] == 0)
                {
                    continue;
                }
                int ly = innermostLoop[y];
                int l;
                if (marked < 0)
                {
                    l = lx;
                }
                else if (Contains(subtreeEnd, marked, ly))
                {
                    continue;
                }
                else
                {
                    l = parent[marked];
                }
                while (l >= 0 && !Contains(subtreeEnd, l, ly))
                {
                    entryLoops.Add(l);
                    entryNodes.Add(x);
                    marked = l;
                    l = parent[l];
                }
            }
        }
        var entryStart = new int[loopCount + 1];
        for (int i = 0; i < entryLoops.Count; i++)
        {
            entryStart[entryLoops[i]]++;
        }
        int nextEntryStart = 0;
        bool reducible = true;
        for (int l = 0; l < loopCount; l++)
        {
            int count = entryStart[l];
            if (count != 1)
            {
                reducible = false;
            }
            entryStart[l] = nextEntryStart;
            nextEntryStart += count;
        }
        entryStart[loopCount] = nextEntryStart;
        var loopEntries = new int[nextEntryStart];
        int[] entryWritePos = entryStart[..loopCount];
        for (int i = 0; i < entryLoops.Count; i++)
        {
            loopEntries[entryWritePos[entryLoops[i]]++] = entryNodes[i];
        }

        return new Loops(innermostLoop, header, parent, subtreeEnd, depth, bodyStart, bodies,
            entryStart, loopEntries, reducible);
    }

    /// <summary>Whether the loop <paramref name="inner"/>, -1 for none, is <paramref name="loop"/> or nested in it.</summary>
    private static bool Contains(int[] subtreeEnd, int loop, int inner) => inner >= loop && inner < subtreeEnd[loop];

    private static int Find(int[] uf, int x)
    {
        int root = x;
        while (uf[root] != root)
        {
            root = uf[root];
        }
        while (uf[x] != root)
        {
            int up = uf[x];
            uf[x] = root;
            x = up;
        }
        return root;
    }

    private static void Append(int[] head, int[] tail, int[] next, int list, int item)
    {
        next[item] = -1;
        if (tail[list] < 0)
        {
            head[list] = item;
        }
        else
        {
            next[tail[list]] = item;
        }
        tail[list] = item;
    }

    /// <summary>Gets the number of loops.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public int LoopCount => _header.Length;

    /// <summary>
    /// Gets whether the graph is reducible: whether every loop has its header as its only entry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The loops of a reducible graph are its natural loops. A node counts only if the entries reach it.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    public bool IsReducible { get; }

    /// <summary>
    /// Returns the innermost loop that contains <paramref name="node"/>, or -1 if it lies in no loop.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The loop ID, or -1.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int InnermostLoopOf(int node)
    {
        Checks.Index(node, _innermostLoop.Length);
        return _innermostLoop[node];
    }

    /// <summary>
    /// Returns the loop depth of <paramref name="node"/>: the number of loops that contain it. A node in
    /// no loop has loop depth 0.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The loop depth.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int LoopDepthOf(int node)
    {
        Checks.Index(node, _innermostLoop.Length);
        int loop = _innermostLoop[node];
        return loop < 0 ? 0 : _depth[loop];
    }

    /// <summary>
    /// Returns whether <paramref name="node"/> lies in <paramref name="loop"/>, directly or in a loop
    /// nested in it.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <param name="loop">Loop ID in <c>0..LoopCount-1</c>.</param>
    /// <returns><see langword="true"/> if the node is in the loop.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="node"/> or <paramref name="loop"/> is out of range.
    /// </exception>
    public bool IsInLoop(int node, int loop)
    {
        Checks.Index(node, _innermostLoop.Length);
        Checks.Index(loop, _header.Length);
        return Contains(_subtreeEnd, loop, _innermostLoop[node]);
    }

    /// <summary>Returns the header of <paramref name="loop"/>.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="loop">Loop ID in <c>0..LoopCount-1</c>.</param>
    /// <returns>The header node ID.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="loop"/> is out of range.</exception>
    public int HeaderOf(int loop)
    {
        Checks.Index(loop, _header.Length);
        return _header[loop];
    }

    /// <summary>
    /// Returns the entries of <paramref name="loop"/>, in ascending order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the nodes of the loop with an in-node outside the loop, and the entries of the graph
    /// that lie in the loop. An in-node counts only if the entries of the graph reach it. A loop that can
    /// only be entered through its header has the header as its only entry.
    /// </para>
    /// <para>The result is a view of this forest's own array, not a copy.</para>
    /// <para><b>Time:</b> O(EntriesOf(loop).Count).</para>
    /// </remarks>
    /// <param name="loop">Loop ID in <c>0..LoopCount-1</c>.</param>
    /// <returns>The entry node IDs.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="loop"/> is out of range.</exception>
    public SortedIntView EntriesOf(int loop)
    {
        Checks.Index(loop, _header.Length);
        return SortedIntView.ViewOf(_entries, _entryStart[loop], _entryStart[loop + 1]);
    }

    /// <summary>
    /// Returns the loop that immediately encloses <paramref name="loop"/>, or -1 for an outermost loop.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="loop">Loop ID in <c>0..LoopCount-1</c>.</param>
    /// <returns>The enclosing loop ID, smaller than <paramref name="loop"/>, or -1.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="loop"/> is out of range.</exception>
    public int ParentOf(int loop)
    {
        Checks.Index(loop, _header.Length);
        return _parent[loop];
    }

    /// <summary>
    /// Returns the body of <paramref name="loop"/>: its nodes and those of the loops nested in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The nodes come in this order: first the header, then, in ascending order, the other nodes whose
    /// innermost loop is <paramref name="loop"/>, then the nodes of the nested loops in the same form,
    /// loop after loop in ID order.
    /// </para>
    /// <para>
    /// The result is a read-only span over this forest's own array, not a copy; to keep it beyond a
    /// method, copy it with <c>ToArray()</c>. All bodies share the array, so a node is stored once,
    /// however deeply its loop is nested.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="loop">Loop ID in <c>0..LoopCount-1</c>.</param>
    /// <returns>The node IDs of the body.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="loop"/> is out of range.</exception>
    public ReadOnlySpan<int> BodyOf(int loop)
    {
        Checks.Index(loop, _header.Length);
        int from = _bodyStart[loop];
        return new ReadOnlySpan<int>(_bodies, from, _bodyStart[_subtreeEnd[loop]] - from);
    }
}
