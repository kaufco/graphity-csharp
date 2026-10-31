namespace Graphity;

/// <summary>
/// The base class of a depth-first search of your own over a <see cref="Graph"/>. A subclass overrides
/// the hooks <see cref="EnterNode"/> and <see cref="LeaveNode"/>, and a <c>Run</c> method starts the
/// search.
/// </summary>
/// <remarks>
/// <para>
/// The search visits each node it reaches once. It calls <see cref="EnterNode"/> when it reaches a
/// node (preorder) and <see cref="LeaveNode"/> when it has searched from all out-nodes of the node
/// (postorder). It takes the out-nodes of each node in ascending order, so the hooks come in the order
/// of a textbook recursive
/// <see href="https://en.wikipedia.org/wiki/Depth-first_search">depth-first search</see>. The search
/// itself runs without recursion and keeps its stack on the heap, so a deep graph does not overflow
/// the call stack.
/// </para>
/// <para>
/// Between its two hooks, a node is on the current path. The current path leads from the node where
/// the search started to the node being searched. The hooks may call <see cref="IsVisited"/> and
/// <see cref="IsOnStack"/>, end the search at a node by returning <see langword="false"/> from
/// <see cref="EnterNode"/>, and end the whole run with <see cref="Stop"/>.
/// </para>
/// <para>
/// <see cref="Run()"/> searches from every node in ascending order, <see cref="Run(int)"/> from one
/// node and <see cref="Run(ReadOnlySpan{int})"/> from several, such as the root nodes that
/// <see cref="Graphs.RootNodes"/> returns. The visited nodes add up over the runs of an instance: a
/// node is visited at most once in the lifetime of the instance, and a later run skips it. A hook must
/// not start another run of the same instance, which throws <see cref="InvalidOperationException"/>.
/// If a run ends with an exception from a hook or from the search, the state of the instance is
/// undefined: nodes of the interrupted path may still count as on the path, for example. A new
/// instance is needed for further runs.
/// </para>
/// <para>
/// <see cref="DfsEdgeTraverser"/> also calls a hook for every edge, which suits searches that look at
/// edges to nodes already visited. This class calls no such hook, which makes it the faster choice
/// where node hooks suffice. <see cref="FrontierTraverser"/> searches breadth-first. Common searches
/// are ready-made, such as <see cref="TopOrder"/>, <see cref="EdgeTypes"/>, <see cref="BackEdges"/>
/// and <see cref="Graphs.FindCycle"/>.
/// </para>
/// <para>Instances are mutable and not safe to share between threads.</para>
/// <para>
/// <b>Memory:</b> 2 bits per node for the visited state, and up to 8 x NodeCount bytes for the stack,
/// which grows with the deepest path searched.
/// </para>
/// </remarks>
/// <example>
/// Here, <c>calls</c> are the calls between the methods of a program, and the postorder puts each
/// method after the methods it calls, except along calls that close a cycle:
/// <!-- example: Traversals.cs#leaveNode -->
/// <code language="csharp">
/// private sealed class CalleesFirst(Graph calls) : DfsTraverser(calls)
/// {
///     public List&lt;int> Order { get; } = [];
///
///     protected override bool EnterNode(int method) => true;
///
///     protected override void LeaveNode(int method) => Order.Add(method);
/// }
///
/// private static List&lt;int> RunCalleesFirst(Graph calls, int main)
/// {
///     var calleesFirst = new CalleesFirst(calls);
///     calleesFirst.Run(main);
///     // calleesFirst.Order: [1, 6, 8, 3, 7, 9, 4, 5, 2, 0] - parseArgs, parse, read,
///     // load, format, write, save, show, run and main
///     return calleesFirst.Order;
/// }
/// </code>
/// </example>
public abstract class DfsTraverser
{
    // Bit 0 of the state of a node: the search has reached it.
    private const int Visited = 0b01;

    // Bit 1 of the state of a node: it is on the current path.
    private const int OnStack = 0b10;

    // The state of every node, 2 bits per node, 16 nodes per int; it adds up over the runs.
    private readonly int[] _state;

    // The stack of the search: frames of two interleaved ints, the node and its resume position in
    // the adjacency array. The field only carries the capacity from one run to the next.
    private int[] _frames;

    // Set by Stop, cleared when a run starts.
    private bool _stopRequested;

    // Set while a run is in progress, so that a hook cannot start another one.
    private bool _running;

    /// <summary>Creates a traverser for <paramref name="graph"/>, with no node visited.</summary>
    /// <remarks><b>Time:</b> O(NodeCount).</remarks>
    /// <param name="graph">The graph to search.</param>
    protected DfsTraverser(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        Graph = graph;
        int n = graph.NodeCount;
        _state = new int[(n >> 4) + 1];
        _frames = DfsFrames.Initial(n);
    }

    /// <summary>Gets the graph that this traverser searches.</summary>
    protected Graph Graph { get; }

    /// <summary>
    /// Called when the search reaches <paramref name="node"/>, before its out-nodes: the preorder hook.
    /// </summary>
    /// <remarks>
    /// The search calls it once per node: for a start node, or for an unvisited out-node of the node it
    /// is searching. From this call on, <paramref name="node"/> counts as visited and is on the current
    /// path. Returning <see langword="true"/> lets the search go on with the out-nodes of
    /// <paramref name="node"/>. Returning <see langword="false"/> skips them, and
    /// <see cref="LeaveNode"/> follows right away; out-nodes skipped this way stay unvisited unless the
    /// search reaches them from another node.
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>
    /// <see langword="true"/> to search from the out-nodes of <paramref name="node"/>,
    /// <see langword="false"/> to skip them.
    /// </returns>
    protected abstract bool EnterNode(int node);

    /// <summary>
    /// Called when the search leaves <paramref name="node"/>, after it has searched from all its
    /// out-nodes: the postorder hook. Does nothing unless overridden.
    /// </summary>
    /// <remarks>
    /// The search calls it once for every node it has visited, right after <see cref="EnterNode"/> if
    /// that returned <see langword="false"/>. From this call on, <paramref name="node"/> is no longer on
    /// the current path. After <see cref="Stop"/>, it is not called for the nodes still on the path.
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    protected virtual void LeaveNode(int node)
    {
    }

    /// <summary>
    /// Returns whether the search has reached <paramref name="node"/>, in this run or in an earlier one.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns><see langword="true"/> if <see cref="EnterNode"/> has been called for <paramref name="node"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    protected bool IsVisited(int node)
    {
        Checks.Index(node, Graph.NodeCount);
        return ((_state[node >> 4] >>> ((node & 0xF) << 1)) & Visited) != 0;
    }

    /// <summary>
    /// Returns whether <paramref name="node"/> is on the stack of the search, that is, on the current
    /// path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The current path leads from the node where the search started to the node being searched. A node
    /// is on the path from its <see cref="EnterNode"/> until its <see cref="LeaveNode"/>. An edge from
    /// the node being searched to a node on the path closes a cycle. Outside a run, no node is on the
    /// path.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns><see langword="true"/> if <paramref name="node"/> is on the current path.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    protected bool IsOnStack(int node)
    {
        Checks.Index(node, Graph.NodeCount);
        return ((_state[node >> 4] >>> ((node & 0xF) << 1)) & OnStack) != 0;
    }

    /// <summary>
    /// Stops the run in progress: the run returns as soon as the hook that calls this method returns,
    /// without calling another hook.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="LeaveNode"/> is not called for the nodes on the current path, but they count as visited
    /// and as no longer on the path. A later run skips every node visited so far; it does not go on where
    /// the stopped run ended. Called outside a run, this method has no effect. To cancel a run from
    /// another thread, let a hook check a flag of your own and call this method.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    protected void Stop() => _stopRequested = true;

    /// <summary>
    /// Searches from every node not visited yet, in ascending order, until every node is visited.
    /// </summary>
    /// <remarks>
    /// <para>A node that no earlier search has reached when its turn comes starts a search of its own.</para>
    /// <para><b>Time:</b> O(NodeCount + EdgeCount), plus the hooks.</para>
    /// </remarks>
    /// <exception cref="ArgumentException">A depth-first path is longer than 2^30 - 29 nodes.</exception>
    /// <exception cref="InvalidOperationException">Called from a hook, during a run of this instance.</exception>
    public void Run()
    {
        Begin();
        try
        {
            int n = Graph.NodeCount;
            for (int root = 0; root < n; root++)
            {
                if (!DfsIfUnvisited(root))
                {
                    return;
                }
            }
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>
    /// Searches from <paramref name="startNode"/>, visiting the nodes it reaches that are not visited
    /// yet.
    /// </summary>
    /// <remarks>
    /// <para>If <paramref name="startNode"/> is visited already, nothing happens.</para>
    /// <para><b>Time:</b> O(reachable nodes + reachable edges), plus the hooks.</para>
    /// </remarks>
    /// <param name="startNode">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="startNode"/> is out of range.</exception>
    /// <exception cref="ArgumentException">A depth-first path is longer than 2^30 - 29 nodes.</exception>
    /// <exception cref="InvalidOperationException">Called from a hook, during a run of this instance.</exception>
    public void Run(int startNode)
    {
        Checks.Index(startNode, Graph.NodeCount);
        Begin();
        try
        {
            DfsIfUnvisited(startNode);
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>
    /// Searches from each of <paramref name="startNodes"/> in list order, skipping those visited already.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The list is checked before the first hook, so a start node out of range leaves the traverser
    /// unchanged. <see cref="Stop"/> ends the whole call, not only the search from one start node.
    /// </para>
    /// <para><b>Time:</b> O(startNodes.Length + reachable nodes + reachable edges), plus the hooks.</para>
    /// </remarks>
    /// <param name="startNodes">
    /// Node IDs, each in <c>0..NodeCount-1</c>; not to be changed until the call returns.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A start node is out of range.</exception>
    /// <exception cref="ArgumentException">A depth-first path is longer than 2^30 - 29 nodes.</exception>
    /// <exception cref="InvalidOperationException">Called from a hook, during a run of this instance.</exception>
    public void Run(ReadOnlySpan<int> startNodes)
    {
        Checks.Nodes(startNodes, Graph.NodeCount);
        Begin();
        try
        {
            foreach (int start in startNodes)
            {
                if (!DfsIfUnvisited(start))
                {
                    return;
                }
            }
        }
        finally
        {
            _running = false;
        }
    }

    // Starts a run after its argument checks: rejects a call from a hook and clears an earlier stop.
    private void Begin()
    {
        if (_running)
        {
            throw new InvalidOperationException("a run is in progress; a hook must not start another run");
        }
        _running = true;
        _stopRequested = false;
    }

    // The single entry of every run method, where the visited check lives. Returns false if the run
    // was stopped.
    private bool DfsIfUnvisited(int root) => IsVisited(root) || DfsFrom(root);

    // The search from root, which is unvisited. The tight inner scan keeps i and end in registers and
    // touches the stack only when it descends. Returns false if the run was stopped.
    private bool DfsFrom(int root)
    {
        int[] outStart = Graph.OutStart;
        int[] adj = Graph.OutAdj;
        int[] state = _state;
        int[] frames = _frames;

        int rShift = (root & 0xF) << 1;
        state[root >> 4] |= (Visited | OnStack) << rShift;
        bool descend = EnterNode(root);
        if (_stopRequested)
        {
            state[root >> 4] &= ~(OnStack << rShift);
            return false;
        }
        if (!descend)
        {
            state[root >> 4] &= ~(OnStack << rShift);
            LeaveNode(root);
            return !_stopRequested;
        }
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
                if (((state[v >> 4] >>> shift) & Visited) != 0)
                {
                    continue;
                }
                // Tree edge.
                state[v >> 4] |= (Visited | OnStack) << shift;
                descend = EnterNode(v);
                if (_stopRequested)
                {
                    state[v >> 4] &= ~(OnStack << shift);
                    LeaveStack(frames, top);
                    return false;
                }
                if (descend)
                {
                    frames[2 * top + 1] = i;
                    if (++top == frames.Length >> 1)
                    {
                        frames = DfsFrames.Grow(frames, Graph.NodeCount);
                        _frames = frames; // keep the grown capacity for later runs
                    }
                    frames[2 * top] = v;
                    frames[2 * top + 1] = outStart[v];
                    descended = true;
                    break;
                }
                // The frame's cursor is saved only on descent; u's scan goes on from the local i.
                state[v >> 4] &= ~(OnStack << shift);
                LeaveNode(v);
                if (_stopRequested)
                {
                    LeaveStack(frames, top);
                    return false;
                }
            }
            if (descended)
            {
                continue;
            }
            state[u >> 4] &= ~(OnStack << ((u & 0xF) << 1));
            LeaveNode(u);
            top--;
            if (_stopRequested)
            {
                LeaveStack(frames, top);
                return false;
            }
        }
        return true;
    }

    // Clears the on-path flag of every node in frames[0..top], once per stop.
    private void LeaveStack(int[] frames, int top)
    {
        for (int d = 0; d <= top; d++)
        {
            int w = frames[2 * d];
            _state[w >> 4] &= ~(OnStack << ((w & 0xF) << 1));
        }
    }
}
