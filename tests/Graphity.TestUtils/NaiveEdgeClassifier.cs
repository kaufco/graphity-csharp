namespace Graphity.TestUtils;

/// <summary>
/// Independent, textbook recursive DFS edge classifier, the oracle for <see cref="EdgeTypes"/> and
/// <see cref="BackEdges"/>. Plain arrays and the public adjacency API, no library traverser. Order:
/// roots ascending, out-edges in adjacency order. Recursion depth equals the longest DFS path, so
/// only shallow test graphs are suitable.
/// </summary>
public static class NaiveEdgeClassifier
{
    /// <summary><c>types[e]</c> is the <see cref="EdgeType"/> of edge index <c>e</c>.</summary>
    public static EdgeType[] Classify(Graph graph)
    {
        int n = graph.NodeCount;
        var types = new EdgeType[graph.EdgeCount];
        if (n == 0)
        {
            return types;
        }
        var visited = new bool[n];
        var onStack = new bool[n];
        var disc = new int[n];
        int time = 0;
        for (int start = 0; start < n; start++)
        {
            if (!visited[start])
            {
                Dfs(start);
            }
        }
        return types;

        void Dfs(int u)
        {
            visited[u] = true;
            onStack[u] = true;
            disc[u] = ++time;
            for (int ei = graph.OutEdgeStart(u); ei < graph.OutEdgeEnd(u); ei++)
            {
                int v = graph.EdgeTarget(ei);
                if (!visited[v])
                {
                    types[ei] = EdgeType.Tree;
                    Dfs(v);
                }
                else if (onStack[v])
                {
                    types[ei] = EdgeType.Back;
                }
                else if (disc[u] < disc[v])
                {
                    types[ei] = EdgeType.Forward;
                }
                else
                {
                    types[ei] = EdgeType.Cross;
                }
            }
            onStack[u] = false;
        }
    }
}
