using System.Collections;

namespace Graphity;

/// <summary>
/// The base class of a breadth-first search of your own over a <see cref="Graph"/>, or of any search
/// that works off a queue of nodes and chooses which nodes enter it.
/// </summary>
/// <remarks>
/// <para>
/// The queue is called the frontier and works first in first out. The traverser calls
/// <see cref="VisitNode"/> for each node in the order in which it entered the frontier. The
/// <c>Run</c> methods put the start nodes into the frontier. The hook puts in the nodes that come
/// next: the out-nodes of a node with <see cref="EnqueueChildren"/>, or a single node with
/// <see cref="Enqueue"/>. A node counts as visited from the moment it enters the frontier, so it
/// enters the frontier at most once. From a node for which the hook enqueues nothing, the search goes
/// no further. With <c>EnqueueChildren(node)</c> in every <c>VisitNode(node)</c>, the traversal is a
/// <see href="https://en.wikipedia.org/wiki/Breadth-first_search">breadth-first search</see>.
/// </para>
/// <para>
/// The nodes come in levels: the start nodes form level 0, and the nodes that enter the frontier
/// while level <c>k</c> is visited form level <c>k + 1</c>. In a breadth-first search, those are the
/// nodes at distance <c>k + 1</c> from the nearest start node. <see cref="CurrentLevel"/> returns the
/// level of the node being visited, and <see cref="LeaveLevel"/> is called after the last node of
/// each level. When <see cref="VisitNode"/> returns, the nodes it enqueued are still waiting in the
/// frontier: unlike <see cref="DfsTraverser"/>, this traverser has no postorder. A hook can end the
/// whole run with <see cref="Stop"/>.
/// </para>
/// <para>
/// <see cref="Run()"/> searches from every node in ascending order, <see cref="Run(int)"/> from one
/// node and <see cref="Run(ReadOnlySpan{int})"/> from several, such as the root nodes that
/// <see cref="Graphs.RootNodes"/> returns. The visited nodes add up over the runs of an instance: a
/// node is visited at most once in the lifetime of the instance, and a later run skips it. Nodes
/// enqueued outside a run come first in the next run. A subclass can also keep nodes out of the
/// search by marking them in <see cref="Visited"/>.
/// </para>
/// <para>
/// A hook must not start another run of the same instance, which throws
/// <see cref="InvalidOperationException"/>. If a run ends with an exception from a hook or from the
/// search, the state of the instance is undefined: a later run may visit the rest of the frontier,
/// for example. A new instance is needed for further runs. <see cref="BfsDistances"/> and
/// <see cref="Graphs.ReachableFrom(Graph, ReadOnlySpan{int})"/> are ready-made breadth-first
/// searches.
/// </para>
/// <para>Instances are mutable and not safe to share between threads.</para>
/// <para>
/// <b>Memory:</b> 1 bit per node for the visited nodes, and 4 to 6 bytes for every node that has
/// entered the frontier, which keeps them all: up to 6 x NodeCount bytes.
/// </para>
/// </remarks>
/// <example>
/// Here, <c>calls</c> are the calls between the methods of a program, and the search follows them
/// from <c>main</c> for two levels:
/// <!-- example: Traversals.cs#currentLevel -->
/// <code language="csharp">
/// private sealed class WithinTwoCalls(Graph calls) : FrontierTraverser(calls)
/// {
///     public List&lt;int> Reached { get; } = [];
///
///     protected override void VisitNode(int method)
///     {
///         Reached.Add(method);
///         if (CurrentLevel &lt; 2)
///         {
///             EnqueueChildren(method);
///         }
///     }
/// }
///
/// private static List&lt;int> RunWithinTwoCalls(Graph calls, int main)
/// {
///     var withinTwoCalls = new WithinTwoCalls(calls);
///     withinTwoCalls.Run(main);
///     // withinTwoCalls.Reached: [0, 1, 2, 3, 4, 5] - main, parseArgs, run, load, save
///     // and show
///     return withinTwoCalls.Reached;
/// }
/// </code>
/// </example>
public abstract class FrontierTraverser
{
    // Every node that has entered the frontier, in order; _cursor is the next one to visit, and _level
    // the level of the node being visited.
    private readonly IntArrayList _frontier = new();
    private int _cursor;
    private int _level;

    // Set by Stop, cleared when a run starts.
    private bool _stopRequested;

    // Set while a run is in progress, so that a hook cannot start another one.
    private bool _running;

    /// <summary>Creates a traverser for <paramref name="graph"/>, with no node visited.</summary>
    /// <remarks><b>Time:</b> O(NodeCount).</remarks>
    /// <param name="graph">The graph to search.</param>
    protected FrontierTraverser(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        Graph = graph;
        Visited = new BitArray(graph.NodeCount);
    }

    /// <summary>Gets the graph that this traverser searches.</summary>
    protected Graph Graph { get; }

    /// <summary>
    /// Gets the visited nodes, one bit per node ID: a bit is set when its node enters the frontier.
    /// </summary>
    /// <remarks>
    /// A subclass may set bits to keep nodes out of the search, also before the first run: a node whose
    /// bit is set counts as visited and no longer enters the frontier, neither from a <c>Run</c> method
    /// nor from <see cref="Enqueue"/> or <see cref="EnqueueChildren"/>. A node already in the frontier is
    /// visited all the same. A subclass must not clear a bit; what happens then is not specified.
    /// </remarks>
    protected BitArray Visited { get; }

    /// <summary>
    /// Called for each node of the frontier, in the order in which the nodes entered it.
    /// </summary>
    /// <remarks>
    /// The hook puts the nodes that come next into the frontier: <c>EnqueueChildren(node)</c> for a
    /// breadth-first search, <see cref="Enqueue"/> for single nodes, or nothing to end the search at
    /// <paramref name="node"/>. They join the next level and are visited after the rest of the current
    /// one.
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    protected abstract void VisitNode(int node);

    /// <summary>
    /// Puts the out-nodes of <paramref name="node"/> that are not visited yet into the frontier, in
    /// ascending order, and marks them visited.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called from <c>VisitNode(node)</c>, it makes the traversal a breadth-first search. Called outside
    /// a run, it adds the nodes to the next run, as <see cref="Enqueue"/> does.
    /// </para>
    /// <para><b>Time:</b> O(OutDegree(node)).</para>
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    protected void EnqueueChildren(int node)
    {
        Checks.Index(node, Graph.NodeCount);
        int[] adjacency = Graph.OutAdj;
        int[] adjacencyStart = Graph.OutStart;
        BitArray visited = Visited;
        for (int i = adjacencyStart[node], end = adjacencyStart[node + 1]; i < end; i++)
        {
            int child = adjacency[i];
            if (!visited[child])
            {
                visited[child] = true;
                _frontier.Add(child);
            }
        }
    }

    /// <summary>
    /// Puts <paramref name="node"/> into the frontier and marks it visited, unless it is visited already.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called from a hook, it adds <paramref name="node"/> to the next level. Called outside a run, it
    /// adds <paramref name="node"/> to the next run: the nodes enqueued then come first in its level 0,
    /// ahead of its start nodes, and <see cref="Run()"/> visits them before it starts from a node of its
    /// own.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="node"/> was not visited and is now in the frontier.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    protected bool Enqueue(int node)
    {
        Checks.Index(node, Graph.NodeCount);
        return EnqueueUnchecked(node);
    }

    private bool EnqueueUnchecked(int node)
    {
        if (Visited[node])
        {
            return false;
        }
        Visited[node] = true;
        _frontier.Add(node);
        return true;
    }

    /// <summary>
    /// Returns whether <paramref name="node"/> is visited: it has entered the frontier, or a subclass has
    /// marked it in <see cref="Visited"/>.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns><see langword="true"/> if <paramref name="node"/> is visited.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    protected bool IsVisited(int node)
    {
        Checks.Index(node, Graph.NodeCount);
        return Visited[node];
    }

    /// <summary>
    /// Gets the level of the node being visited: 0 for the start nodes, <c>k + 1</c> for the nodes that
    /// entered the frontier while level <c>k</c> was visited.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In <see cref="LeaveLevel"/>, it is the level just completed. Outside a run, the value has no
    /// meaning.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    protected int CurrentLevel => _level;

    /// <summary>
    /// Called after the last node of a level has been visited, before the first node of the next level.
    /// Does nothing unless overridden.
    /// </summary>
    /// <remarks>
    /// Nodes enqueued here join the next level, which then follows even if the completed level enqueued
    /// nothing.
    /// </remarks>
    /// <param name="level">The level just completed.</param>
    protected virtual void LeaveLevel(int level)
    {
    }

    /// <summary>
    /// Stops the run in progress: the run returns as soon as the hook that calls this method returns,
    /// without calling another hook.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The nodes still waiting in the frontier are dropped from it, but stay marked visited, so no later
    /// run visits them. A later run skips every node visited so far. Called outside a run, this method
    /// has no effect. To cancel a run from another thread, let a hook check a flag of your own and call
    /// this method.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    protected void Stop() => _stopRequested = true;

    /// <summary>
    /// Searches from every node not visited yet, in ascending order, until every node is visited.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nodes enqueued before the call come first, as a level 0 of their own. Then each node that no
    /// earlier search has reached when its turn comes starts a search of its own, with levels counted
    /// from 0 again.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount), plus the hooks: O(NodeCount + EdgeCount) for a breadth-first search.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Called from a hook, during a run of this instance.</exception>
    public void Run()
    {
        Begin();
        try
        {
            // Nodes enqueued outside a run first; an empty frontier calls no hook.
            if (!ProcessFrontier())
            {
                return;
            }
            int n = Graph.NodeCount;
            int root = NextUnvisited(0, n);
            while (root < n)
            {
                EnqueueUnchecked(root);
                if (!ProcessFrontier())
                {
                    return;
                }
                root = NextUnvisited(root + 1, n);
            }
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>
    /// Searches from <paramref name="startNode"/> and from the nodes enqueued before the call, visiting
    /// the nodes they reach that are not visited yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Level 0 holds the nodes enqueued before the call, then <paramref name="startNode"/> unless it is
    /// visited already.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(nodes visited), plus the hooks: O(reachable nodes + reachable edges) for a
    /// breadth-first search.
    /// </para>
    /// </remarks>
    /// <param name="startNode">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="startNode"/> is out of range.</exception>
    /// <exception cref="InvalidOperationException">Called from a hook, during a run of this instance.</exception>
    public void Run(int startNode)
    {
        Checks.Index(startNode, Graph.NodeCount);
        Begin();
        try
        {
            EnqueueUnchecked(startNode);
            ProcessFrontier();
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>
    /// Searches from <paramref name="startNodes"/> and from the nodes enqueued before the call, visiting
    /// the nodes they reach that are not visited yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Level 0 holds the nodes enqueued before the call, then the start nodes in list order, skipping
    /// those visited already. The list is checked first, so a start node out of range leaves the
    /// traverser unchanged.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(startNodes.Length + nodes visited), plus the hooks:
    /// O(startNodes.Length + reachable nodes + reachable edges) for a breadth-first search.
    /// </para>
    /// </remarks>
    /// <param name="startNodes">
    /// Node IDs, each in <c>0..NodeCount-1</c>; not to be changed until the call returns.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A start node is out of range.</exception>
    /// <exception cref="InvalidOperationException">Called from a hook, during a run of this instance.</exception>
    public void Run(ReadOnlySpan<int> startNodes)
    {
        Checks.Nodes(startNodes, Graph.NodeCount);
        Begin();
        try
        {
            foreach (int start in startNodes)
            {
                EnqueueUnchecked(start);
            }
            ProcessFrontier();
        }
        finally
        {
            _running = false;
        }
    }

    private void Begin()
    {
        if (_running)
        {
            throw new InvalidOperationException("a run is in progress; a hook must not start another run");
        }
        _running = true;
        _stopRequested = false;
    }

    private int NextUnvisited(int from, int n)
    {
        while (from < n && Visited[from])
        {
            from++;
        }
        return from;
    }

    /// <summary>Processes the frontier one level per round and fires <see cref="LeaveLevel"/> after each.</summary>
    /// <returns><c>false</c> if the traversal was stopped.</returns>
    private bool ProcessFrontier()
    {
        _level = 0;
        while (_cursor < _frontier.Count)
        {
            int levelEnd = _frontier.Count;
            while (_cursor < levelEnd)
            {
                VisitNode(_frontier[_cursor++]);
                if (_stopRequested)
                {
                    _cursor = _frontier.Count;
                    return false;
                }
            }
            LeaveLevel(_level);
            if (_stopRequested)
            {
                _cursor = _frontier.Count;
                return false;
            }
            _level++;
        }
        return true;
    }
}
