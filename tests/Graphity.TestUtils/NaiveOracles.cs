namespace Graphity.TestUtils;

/// <summary>
/// Naive reference algorithms that take the place of the JGraphT oracles of the Java tests
/// (<c>JGraphtAdapter</c>): each answers from the definition, with plain sets, breadth-first
/// searches and removal tests, and shares no idea with the algorithm it checks. Quadratic or worse;
/// meant for graphs of a few hundred nodes.
/// </summary>
/// <remarks>
/// The undirected oracles read the graph as JGraphT's <c>SimpleGraph</c> does: a reciprocal pair is
/// one edge and self-loops are dropped.
/// </remarks>
public static class NaiveOracles
{
    /// <summary>The nodes reachable from <paramref name="source"/> by a path of length zero or more.</summary>
    public static bool[] ReachableFrom(Graph graph, int source)
    {
        int n = graph.NodeCount;
        var seen = new bool[n];
        var queue = new Queue<int>();
        seen[source] = true;
        queue.Enqueue(source);
        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            foreach (int v in graph.OutNodes(u))
            {
                if (!seen[v])
                {
                    seen[v] = true;
                    queue.Enqueue(v);
                }
            }
        }
        return seen;
    }

    /// <summary>
    /// The strongly connected components as a set of node sets: <c>u</c> and <c>v</c> share one
    /// exactly if each reaches the other (the role of JGraphT's Kosaraju inspector).
    /// </summary>
    public static HashSet<string> StronglyConnectedSets(Graph graph)
    {
        int n = graph.NodeCount;
        var reach = new bool[n][];
        for (int u = 0; u < n; u++)
        {
            reach[u] = ReachableFrom(graph, u);
        }
        var assigned = new bool[n];
        var result = new HashSet<string>();
        for (int u = 0; u < n; u++)
        {
            if (assigned[u])
            {
                continue;
            }
            var component = new List<int>();
            for (int v = u; v < n; v++)
            {
                if (reach[u][v] && reach[v][u])
                {
                    assigned[v] = true;
                    component.Add(v);
                }
            }
            result.Add(Key(component));
        }
        return result;
    }

    /// <summary>The weakly connected components as a set of node sets (the role of JGraphT's connectivity inspector).</summary>
    public static HashSet<string> WeaklyConnectedSets(Graph graph)
    {
        int[] label = ComponentLabels(UndirectedNeighbors(graph), -1, -1, -1);
        var groups = new SortedDictionary<int, List<int>>();
        for (int v = 0; v < label.Length; v++)
        {
            if (!groups.TryGetValue(label[v], out var list))
            {
                groups[label[v]] = list = [];
            }
            list.Add(v);
        }
        return [.. groups.Values.Select(Key)];
    }

    /// <summary>A canonical key of a node set: its sorted members.</summary>
    public static string Key(IEnumerable<int> nodes) => string.Join(",", nodes.Order());

    /// <summary>Whether the graph has a directed cycle, by repeatedly removing nodes without in-edges (the role of JGraphT's cycle detector).</summary>
    public static bool HasCycle(Graph graph)
    {
        int n = graph.NodeCount;
        var inDegree = new int[n];
        for (int u = 0; u < n; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                inDegree[v]++;
            }
        }
        var queue = new Queue<int>();
        for (int v = 0; v < n; v++)
        {
            if (inDegree[v] == 0)
            {
                queue.Enqueue(v);
            }
        }
        int removed = 0;
        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            removed++;
            foreach (int v in graph.OutNodes(u))
            {
                if (--inDegree[v] == 0)
                {
                    queue.Enqueue(v);
                }
            }
        }
        return removed < n;
    }

    /// <summary>
    /// The lowest common ancestor in a forest of out-trees by intersecting ancestor sets, or -1 (the
    /// role of JGraphT's naive LCA finder).
    /// </summary>
    public static int LowestCommonAncestor(Graph forest, int a, int b)
    {
        int[] parent = Parents(forest);
        var ancestorsOfA = new HashSet<int>();
        for (int x = a; x >= 0; x = parent[x])
        {
            ancestorsOfA.Add(x);
        }
        for (int x = b; x >= 0; x = parent[x])
        {
            if (ancestorsOfA.Contains(x))
            {
                return x;
            }
        }
        return -1;
    }

    /// <summary>The parent of every node of a forest of out-trees, -1 for a root.</summary>
    public static int[] Parents(Graph forest)
    {
        var parent = new int[forest.NodeCount];
        Array.Fill(parent, -1);
        for (int u = 0; u < forest.NodeCount; u++)
        {
            foreach (int v in forest.OutNodes(u))
            {
                parent[v] = u;
            }
        }
        return parent;
    }

    /// <summary>The undirected simple edges <c>{u, v}</c>, <c>u &lt; v</c>, as packed keys.</summary>
    public static SortedSet<long> UndirectedEdges(Graph graph)
    {
        var edges = new SortedSet<long>();
        for (int u = 0; u < graph.NodeCount; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                if (u != v)
                {
                    edges.Add(PairKey(u, v));
                }
            }
        }
        return edges;
    }

    /// <summary>The key of the undirected edge <c>{a, b}</c>: the smaller endpoint high, the larger low.</summary>
    public static long PairKey(int a, int b) => ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);

    /// <summary>The neighbors of every node in the undirected simple reading, each once, ascending.</summary>
    public static int[][] UndirectedNeighbors(Graph graph)
    {
        int n = graph.NodeCount;
        var sets = new SortedSet<int>[n];
        for (int v = 0; v < n; v++)
        {
            sets[v] = [];
        }
        foreach (long edge in UndirectedEdges(graph))
        {
            int a = (int)(edge >> 32);
            int b = (int)edge;
            sets[a].Add(b);
            sets[b].Add(a);
        }
        return [.. sets.Select(set => set.ToArray())];
    }

    /// <summary>
    /// Component labels over <paramref name="neighbors"/>, with <paramref name="removedNode"/> and the
    /// edge <c>{removedA, removedB}</c> taken out; a removed node gets label -1.
    /// </summary>
    public static int[] ComponentLabels(int[][] neighbors, int removedNode, int removedA, int removedB)
    {
        int n = neighbors.Length;
        var label = new int[n];
        Array.Fill(label, -2);
        var queue = new int[n];
        int next = 0;
        for (int s = 0; s < n; s++)
        {
            if (s == removedNode)
            {
                label[s] = -1;
                continue;
            }
            if (label[s] != -2)
            {
                continue;
            }
            int head = 0;
            int tail = 0;
            label[s] = next;
            queue[tail++] = s;
            while (head < tail)
            {
                int u = queue[head++];
                foreach (int v in neighbors[u])
                {
                    if (label[v] != -2 || v == removedNode
                        || (u == removedA && v == removedB) || (u == removedB && v == removedA))
                    {
                        continue;
                    }
                    label[v] = next;
                    queue[tail++] = v;
                }
            }
            next++;
        }
        return label;
    }

    private static int CountComponents(int[] labels) => labels.Where(l => l >= 0).Distinct().Count();

    /// <summary>
    /// The bridges of the graph read as undirected: edges whose removal disconnects their endpoints.
    /// Only edges of a spanning forest can be bridges, so only those are tested.
    /// </summary>
    public static SortedSet<long> Bridges(Graph graph)
    {
        int[][] neighbors = UndirectedNeighbors(graph);
        int n = neighbors.Length;
        var result = new SortedSet<long>();
        var seen = new bool[n];
        for (int s = 0; s < n; s++)
        {
            if (seen[s])
            {
                continue;
            }
            seen[s] = true;
            var queue = new Queue<int>();
            queue.Enqueue(s);
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                foreach (int v in neighbors[u])
                {
                    if (seen[v])
                    {
                        continue;
                    }
                    seen[v] = true;
                    queue.Enqueue(v);
                    int[] label = ComponentLabels(neighbors, -1, u, v);
                    if (label[u] != label[v])
                    {
                        result.Add(PairKey(u, v));
                    }
                }
            }
        }
        return result;
    }

    /// <summary>
    /// The articulation points of the graph read as undirected: nodes whose removal splits their
    /// component - some two of their neighbors end up in different components.
    /// </summary>
    public static SortedSet<int> CutPoints(Graph graph)
    {
        int[][] neighbors = UndirectedNeighbors(graph);
        var result = new SortedSet<int>();
        for (int v = 0; v < neighbors.Length; v++)
        {
            if (neighbors[v].Length < 2)
            {
                continue;
            }
            int[] label = ComponentLabels(neighbors, v, -1, -1);
            if (neighbors[v].Select(w => label[w]).Distinct().Count() > 1)
            {
                result.Add(v);
            }
        }
        return result;
    }

    /// <summary>
    /// The blocks of the graph read as undirected, each as the sorted keys of its undirected edges.
    /// Two edges <c>{v, a}</c> and <c>{v, b}</c> at a common node lie on a common cycle exactly if
    /// <c>a</c> and <c>b</c> stay connected once <c>v</c> is removed; the blocks are the classes of
    /// that relation. Per node, the incident edges are joined by the component of their other end in
    /// the graph without the node, so the cost is one component labeling per node.
    /// </summary>
    public static HashSet<string> Blocks(Graph graph)
    {
        int[][] neighbors = UndirectedNeighbors(graph);
        long[] edges = [.. UndirectedEdges(graph)];
        var indexOf = new Dictionary<long, int>();
        for (int i = 0; i < edges.Length; i++)
        {
            indexOf[edges[i]] = i;
        }
        var classOf = new int[edges.Length];
        for (int i = 0; i < classOf.Length; i++)
        {
            classOf[i] = i;
        }
        int Find(int x)
        {
            while (classOf[x] != x)
            {
                classOf[x] = classOf[classOf[x]];
                x = classOf[x];
            }
            return x;
        }
        for (int v = 0; v < neighbors.Length; v++)
        {
            if (neighbors[v].Length < 2)
            {
                continue;
            }
            int[] label = ComponentLabels(neighbors, v, -1, -1);
            var firstEdgeOfComponent = new Dictionary<int, int>();
            foreach (int w in neighbors[v])
            {
                int edge = indexOf[PairKey(v, w)];
                if (firstEdgeOfComponent.TryGetValue(label[w], out int first))
                {
                    classOf[Find(edge)] = Find(first);
                }
                else
                {
                    firstEdgeOfComponent[label[w]] = edge;
                }
            }
        }
        var blocks = new Dictionary<int, List<long>>();
        for (int i = 0; i < edges.Length; i++)
        {
            int c = Find(i);
            if (!blocks.TryGetValue(c, out var list))
            {
                blocks[c] = list = [];
            }
            list.Add(edges[i]);
        }
        return [.. blocks.Values.Select(list => string.Join(",", list.Order()))];
    }

    /// <summary>Whether the graph read as undirected (self-loops dropped) is bipartite, by breadth-first two-coloring.</summary>
    public static bool IsBipartite(Graph graph)
    {
        int[][] neighbors = UndirectedNeighbors(graph);
        int n = neighbors.Length;
        var color = new int[n];
        Array.Fill(color, -1);
        for (int s = 0; s < n; s++)
        {
            if (color[s] >= 0)
            {
                continue;
            }
            color[s] = 0;
            var queue = new Queue<int>();
            queue.Enqueue(s);
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                foreach (int v in neighbors[u])
                {
                    if (color[v] < 0)
                    {
                        color[v] = 1 - color[u];
                        queue.Enqueue(v);
                    }
                    else if (color[v] == color[u])
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    /// <summary>Whether the graph read as undirected (self-loops dropped) is connected; true for the empty graph.</summary>
    public static bool IsConnected(Graph graph) =>
        CountComponents(ComponentLabels(UndirectedNeighbors(graph), -1, -1, -1)) <= 1;

    /// <summary>Whether the graph read as undirected (self-loops dropped) is a forest: as many edges as nodes minus components.</summary>
    public static bool IsUndirectedForest(Graph graph) =>
        UndirectedEdges(graph).Count == graph.NodeCount - CountComponents(ComponentLabels(UndirectedNeighbors(graph), -1, -1, -1));

    /// <summary>Whether the graph read as undirected (self-loops dropped) is a tree: a connected forest; true for the empty graph.</summary>
    public static bool IsUndirectedTree(Graph graph) => IsUndirectedForest(graph) && IsConnected(graph);
}
