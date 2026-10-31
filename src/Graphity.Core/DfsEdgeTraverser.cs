namespace Graphity;

/// <summary>
/// The base class of a depth-first search of your own over a <see cref="Graph"/>, with a hook for
/// every edge. The hook also sees the edges to nodes already visited.
/// </summary>
/// <remarks>
/// <para>
/// Like <see cref="DfsTraverser"/>, it calls <see cref="EnterNode"/> when the search reaches a node
/// and <see cref="LeaveNode"/> when it leaves the node. In between, it reports each out-edge of the
/// node to <see cref="VisitEdge"/>, in edge index order, with the state of the target:
/// <see cref="TargetState.Unvisited">unvisited</see>,
/// <see cref="TargetState.OnStack">on the current path</see> or
/// <see cref="TargetState.Finished">finished</see>. The hook returns what the search does with the
/// edge: <see cref="EdgeAction.Descend">descend</see> into an unvisited target and search from it
/// before the next edge, <see cref="EdgeAction.Skip">skip</see> the edge, or
/// <see cref="EdgeAction.Stop">stop</see> the run; any hook can stop the run with <see cref="Stop"/>.
/// </para>
/// <para>
/// By default, the search descends into every unvisited target, and the hooks come in the order of a
/// textbook recursive
/// <see href="https://en.wikipedia.org/wiki/Depth-first_search">depth-first search</see>, as in
/// <see cref="DfsTraverser"/>. The search itself runs without recursion and keeps its stack on the
/// heap. An edge to a target on the current path closes a cycle. <see cref="Graphs.FindCycle"/>
/// returns such a cycle as a list of nodes. The state of a target does not tell a forward edge from
/// a cross edge; <see cref="EdgeTypes"/> classifies every edge.
/// </para>
/// <para>
/// The runs work as in <see cref="DfsTraverser"/>. The visited nodes add up over the runs of an
/// instance. A hook must not start another run of the same instance, which throws
/// <see cref="InvalidOperationException"/>. After a run that ends with an exception, the state of
/// the instance is undefined. A search that needs no hook per edge is faster on
/// <see cref="DfsTraverser"/>.
/// </para>
/// <para>Instances are mutable and not safe to share between threads.</para>
/// <para>
/// <b>Memory:</b> 2 bits per node for the visited state, and up to 8 x NodeCount bytes for the stack,
/// which grows with the deepest path searched.
/// </para>
/// </remarks>
/// <example>
/// Here, <c>dependencies</c> are the dependencies between the packages of a program, and the search
/// stops at the first dependency that closes a cycle:
/// <!-- example: Traversals.cs#visitEdge -->
/// <code language="csharp">
/// private sealed class FirstCycle(Graph dependencies) : DfsEdgeTraverser(dependencies)
/// {
///     public int ClosingEdge { get; private set; } = -1;
///
///     protected override EdgeAction VisitEdge(
///         int source, int target, int edgeIndex, TargetState state)
///     {
///         if (state == TargetState.OnStack)
///         {
///             ClosingEdge = edgeIndex;
///             return EdgeAction.Stop;
///         }
///         return base.VisitEdge(source, target, edgeIndex, state);
///     }
/// }
///
/// private static int RunFirstCycle(Graph dependencies)
/// {
///     var firstCycle = new FirstCycle(dependencies);
///     firstCycle.Run();
///     // firstCycle.ClosingEdge: 10 - the edge storage -> model
///     return firstCycle.ClosingEdge;
/// }
/// </code>
/// </example>
public abstract class DfsEdgeTraverser
{
    /// <summary>The state of the target of an edge when <see cref="VisitEdge"/> reports the edge.</summary>
    public enum TargetState
    {
        /// <summary>The search has not reached the target yet.</summary>
        Unvisited,

        /// <summary>
        /// The target is on the current path - the source itself or one of its ancestors in the search - so
        /// the edge closes a cycle.
        /// </summary>
        OnStack,

        /// <summary>The search has reached the target and left it, or a stopped run left it on its path.</summary>
        Finished,
    }

    /// <summary>What the search does with an edge that <see cref="VisitEdge"/> reports.</summary>
    public enum EdgeAction
    {
        /// <summary>
        /// Enters the target if it is unvisited, and searches from it before the next edge; for any other
        /// target, the same as <see cref="Skip"/>.
        /// </summary>
        Descend,

        /// <summary>
        /// Goes on with the next edge of the source. An unvisited target stays unvisited; a later edge or run
        /// may still reach it.
        /// </summary>
        Skip,

        /// <summary>
        /// Ends the run, as <see cref="DfsEdgeTraverser.Stop"/> does.
        /// <see cref="DfsEdgeTraverser.LeaveNode"/> is not called for the nodes on the current path, but
        /// they count as finished afterwards; an unvisited target stays unvisited.
        /// </summary>
        Stop,
    }

    // Bit 0 of the state of a node: the search has reached it.
    private const int Visited = 0b01;

    // Bit 1 of the state of a node: it is on the current path.
    private const int OnStack = 0b10;

    // The target state by the two bits of the state of a node; 2, on the path but not visited, does
    // not occur.
    private static readonly TargetState[] TargetStates =
    [
        TargetState.Unvisited, TargetState.Finished, TargetState.OnStack, TargetState.OnStack,
    ];

    // The state of every node, 2 bits per node, 16 nodes per int; it adds up over the runs.
    private readonly int[] _state;

    // The stack of the search: frames of two interleaved ints, the node and its resume position in
    // the adjacency array. The field only carries the capacity from one run to the next.
    private int[] _frames;

    // Set by Stop and by EdgeAction.Stop, cleared when a run starts.
    private bool _stopRequested;

    // Set while a run is in progress, so that a hook cannot start another one.
    private bool _running;

    /// <summary>Creates a traverser for <paramref name="graph"/>, with no node visited.</summary>
    /// <remarks><b>Time:</b> O(NodeCount).</remarks>
    /// <param name="graph">The graph to search.</param>
    protected DfsEdgeTraverser(Graph graph)
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
    /// Called when the search reaches <paramref name="node"/>, before its out-edges: the preorder hook.
    /// Returns <see langword="true"/> unless overridden.
    /// </summary>
    /// <remarks>
    /// The search calls it once per node: for a start node, or for the target of an edge for which
    /// <see cref="VisitEdge"/> returned <see cref="EdgeAction.Descend"/> while the target was unvisited.
    /// From this call on, <paramref name="node"/> counts as visited and is on the current path. Returning
    /// <see langword="false"/> skips the out-edges of <paramref name="node"/>: none of them is reported,
    /// and <see cref="LeaveNode"/> follows right away.
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>
    /// <see langword="true"/> to report the out-edges of <paramref name="node"/>,
    /// <see langword="false"/> to skip them.
    /// </returns>
    protected virtual bool EnterNode(int node) => true;

    /// <summary>
    /// Called when the search leaves <paramref name="node"/>, after all its out-edges have been reported
    /// and searched: the postorder hook. Does nothing unless overridden.
    /// </summary>
    /// <remarks>
    /// The search calls it once for every node it has visited, right after <see cref="EnterNode"/> if
    /// that returned <see langword="false"/>. From this call on, <paramref name="node"/> is no longer on
    /// the current path. After the run is stopped, it is not called for the nodes still on the path.
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    protected virtual void LeaveNode(int node)
    {
    }

    /// <summary>
    /// Called for each out-edge of a node that the search has entered, with the state of its target;
    /// returns what the search does with the edge.
    /// </summary>
    /// <remarks>
    /// The search reports the out-edges of <paramref name="source"/> in edge index order, between
    /// <c>EnterNode(source)</c> and <c>LeaveNode(source)</c>. It reports no out-edge of a node whose
    /// <see cref="EnterNode"/> returned <see langword="false"/>. The state is that of the target when the
    /// edge is reported: an unvisited target that one edge skips is reported as unvisited again by the
    /// next edge to it. Unless overridden, returns <see cref="EdgeAction.Descend"/> for an unvisited
    /// target and <see cref="EdgeAction.Skip"/> for any other. A value outside the members of
    /// <see cref="EdgeAction"/> makes the run throw <see cref="ArgumentOutOfRangeException"/>.
    /// </remarks>
    /// <param name="source">The source of the edge, which is on the current path.</param>
    /// <param name="target">The target of the edge.</param>
    /// <param name="edgeIndex">The edge index of the edge <c>source -> target</c>.</param>
    /// <param name="state">The state of <paramref name="target"/>.</param>
    /// <returns>What the search does with the edge.</returns>
    protected virtual EdgeAction VisitEdge(int source, int target, int edgeIndex, TargetState state) =>
        state == TargetState.Unvisited ? EdgeAction.Descend : EdgeAction.Skip;

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
    /// is on the path from its <see cref="EnterNode"/> until its <see cref="LeaveNode"/>. Outside a run,
    /// no node is on the path.
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
    /// It works from every hook; from <see cref="VisitEdge"/>, it does the same as returning
    /// <see cref="EdgeAction.Stop"/>. <see cref="LeaveNode"/> is not called for the nodes on the current
    /// path, but they count as finished afterwards. This includes a node whose <see cref="EnterNode"/>
    /// called this method. A later run skips every node visited so far; it does not go on where the
    /// stopped run ended. Called outside a run, this method has no effect. To cancel a run from another
    /// thread, let a hook check a flag of your own and call this method.
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
    /// unchanged. <see cref="EdgeAction.Stop"/> and <see cref="Stop"/> end the whole call, not only the
    /// search from one start node.
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

    // The search from root, which is unvisited. Returns false if the run was stopped.
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
                int edge = i++;
                int v = adj[edge];
                int shift = (v & 0xF) << 1;
                int flags = (state[v >> 4] >>> shift) & (Visited | OnStack);
                EdgeAction action = VisitEdge(u, v, edge, TargetStates[flags]);
                if (_stopRequested)
                {
                    action = EdgeAction.Stop;
                }
                if (action == EdgeAction.Stop)
                {
                    LeaveStack(frames, top);
                    return false;
                }
                if (action != EdgeAction.Descend)
                {
                    if (action != EdgeAction.Skip)
                    {
                        // A value the hook returned, not an argument: no parameter to name.
                        throw new ArgumentOutOfRangeException(null, action, "not an EdgeAction");
                    }
                    continue;
                }
                if (flags != 0)
                {
                    continue;
                }
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
                        _frames = frames;
                    }
                    frames[2 * top] = v;
                    frames[2 * top + 1] = outStart[v];
                    descended = true;
                    break;
                }
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
