namespace Graphity.TestUtils;

/// <summary>A <see cref="DfsTraverser"/> that records preorder and postorder; the protected state queries are exposed for tests.</summary>
public class RecordingDfsTraverser(Graph graph) : DfsTraverser(graph)
{
    /// <summary>Nodes in the order <see cref="EnterNode"/> saw them.</summary>
    public List<int> PreOrder { get; } = [];

    /// <summary>Nodes in the order <see cref="DfsTraverser.LeaveNode"/> saw them.</summary>
    public List<int> PostOrder { get; } = [];

    /// <summary><see cref="DfsTraverser.IsVisited"/>, callable from a test.</summary>
    public bool WasVisited(int node) => IsVisited(node);

    /// <summary><see cref="DfsTraverser.IsOnStack"/>, callable from a test.</summary>
    public bool IsOnPath(int node) => IsOnStack(node);

    /// <inheritdoc/>
    protected override bool EnterNode(int node)
    {
        PreOrder.Add(node);
        return true;
    }

    /// <inheritdoc/>
    protected override void LeaveNode(int node) => PostOrder.Add(node);
}

/// <summary>
/// A <see cref="FrontierTraverser"/> that records visit order, the level of each visit and the
/// completed levels; it enqueues all unvisited children (standard BFS).
/// </summary>
public class RecordingFrontierTraverser(Graph graph) : FrontierTraverser(graph)
{
    /// <summary>Nodes in visit order.</summary>
    public List<int> VisitOrder { get; } = [];

    /// <summary>The level at each visit.</summary>
    public List<int> LevelAtVisit { get; } = [];

    /// <summary>The levels reported to <see cref="LeaveLevel"/>.</summary>
    public List<int> CompletedLevels { get; } = [];

    /// <summary><see cref="FrontierTraverser.IsVisited"/>, callable from a test.</summary>
    public bool WasVisited(int node) => IsVisited(node);

    /// <summary>Marks <paramref name="node"/> visited before a run, as Java tests set bits of <c>visited</c>.</summary>
    public void PreMark(int node) => Visited[node] = true;

    /// <summary><see cref="FrontierTraverser.Enqueue"/>, callable from a test outside a run, as Java tests call <c>enqueue</c>.</summary>
    public bool EnqueueOutsideRun(int node) => Enqueue(node);

    /// <inheritdoc/>
    protected override void VisitNode(int node)
    {
        VisitOrder.Add(node);
        LevelAtVisit.Add(CurrentLevel);
        EnqueueChildren(node);
    }

    /// <inheritdoc/>
    protected override void LeaveLevel(int level) => CompletedLevels.Add(level);
}

/// <summary>
/// Preorder and postorder of a textbook recursive depth-first search, the oracle for the iterative
/// traversal cores. Roots ascending, out-nodes in adjacency order; plain recursion over the public
/// adjacency API, so only shallow graphs are suitable.
/// </summary>
public sealed class RecursiveDfsOrder(Graph graph)
{
    private readonly bool[] _visited = new bool[graph.NodeCount];

    /// <summary>The preorder after <see cref="Run"/>.</summary>
    public List<int> PreOrder { get; } = [];

    /// <summary>The postorder after <see cref="Run"/>.</summary>
    public List<int> PostOrder { get; } = [];

    /// <summary>Visits every node, starting a new tree at each unvisited node in ascending order.</summary>
    public void Run()
    {
        for (int node = 0; node < _visited.Length; node++)
        {
            if (!_visited[node])
            {
                Visit(node);
            }
        }
    }

    private void Visit(int node)
    {
        _visited[node] = true;
        PreOrder.Add(node);
        for (int edge = graph.OutEdgeStart(node); edge < graph.OutEdgeEnd(node); edge++)
        {
            int target = graph.EdgeTarget(edge);
            if (!_visited[target])
            {
                Visit(target);
            }
        }
        PostOrder.Add(node);
    }
}
