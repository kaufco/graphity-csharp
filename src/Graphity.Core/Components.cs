namespace Graphity;

/// <summary>
/// The algorithms for the components of a graph, and for the edges and nodes whose removal splits
/// them.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SccsOf"/> returns the SCCs (strongly connected components), and <see cref="WccsOf"/>
/// the weakly connected components. <see cref="CcsOf"/> returns the weakly connected components
/// under another name: the connected components of the graph read as undirected.
/// <see cref="BiconnectivityOf"/> returns the bridges, articulation points and biconnected
/// components. Bridges and articulation points are the edges and nodes whose removal splits a
/// connected component. <see cref="StrongConnectivityOf"/> returns the strong bridges and strong
/// articulation points, which split an SCC.
/// </para>
/// <para>
/// A graph is always directed, and each method says how it reads the edges. <c>SccsOf</c> and
/// <c>StrongConnectivityOf</c> follow them in their direction. <c>WccsOf</c>, <c>CcsOf</c> and
/// <c>BiconnectivityOf</c> read the graph as undirected: an edge connects its source and target
/// whether or not the reverse edge exists, and the edges <c>a -> b</c> and <c>b -> a</c> count as
/// one undirected edge, not two. An undirected graph can therefore be stored with one edge per pair
/// of neighbors or with both directions, and these methods read it the same way. For the bridges
/// this matters: read as two edges, the two directions would form a cycle, and no edge stored in
/// both directions could be a bridge.
/// </para>
/// </remarks>
public static class Components
{
    /// <summary>
    /// Returns the SCCs of <paramref name="graph"/>. Uses
    /// <see href="https://en.wikipedia.org/wiki/Tarjan%27s_strongly_connected_components_algorithm">Tarjan's algorithm</see>,
    /// without recursion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every node is in exactly one SCC; a node that lies on no cycle is an SCC of its own. SCC IDs
    /// are in reverse topological order: every edge between two SCCs goes from the higher SCC ID to
    /// the lower one, so SCC 0 has no edges to other SCCs. The nodes of each SCC are in ascending
    /// order.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount), plus sorting the nodes of each SCC: up to
    /// O(NodeCount log NodeCount) when one SCC holds most of the nodes.<br/>
    /// <b>Memory:</b> up to 12 x NodeCount bytes for the result; while it runs, 24 x NodeCount
    /// bytes more.
    /// </para>
    /// </remarks>
    /// <example>
    /// Here, <c>dependencies</c> are the dependencies between the packages of a program, and
    /// <c>names</c> holds the names of the packages. The example groups the packages by SCC, in the
    /// order of the SCC IDs. In this order, each package comes after the packages it uses, and the
    /// packages that use each other come together:
    /// <!-- example: Connectivity.cs#sccsOf -->
    /// <code language="csharp">
    /// // The SCCs in reverse topological order, each as the names of its packages.
    /// Sccs sccs = Components.SccsOf(dependencies);
    /// List&lt;List&lt;string>> buildOrder = [];
    /// for (int scc = 0; scc &lt; sccs.SccCount; scc++)
    /// {
    ///     buildOrder.Add([.. sccs.NodesOf(scc).Select(node => names[node])]);
    /// }
    /// // buildOrder: [[util], [cli], [json], [model, storage], [service], [ui], [app]]
    /// </code>
    /// </example>
    /// <param name="graph">The graph.</param>
    /// <returns>The SCCs.</returns>
    /// <exception cref="ArgumentException"><paramref name="graph"/> has more than 2^30 - 29 nodes.</exception>
    public static Sccs SccsOf(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return Sccs.Of(graph);
    }

    /// <summary>
    /// Returns the weakly connected components of <paramref name="graph"/>. These are the
    /// components of the graph read as undirected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every node is in exactly one component. Each component is a group of the result. The groups
    /// are in the order of their smallest nodes: group 0 holds node 0, group 1 the smallest node
    /// not in group 0, and so on. Uses
    /// <see href="https://en.wikipedia.org/wiki/Disjoint-set_data_structure">union-find</see>,
    /// whose cost grows with alpha(NodeCount). The inverse Ackermann function alpha is below 5 for
    /// any graph.
    /// </para>
    /// <para>
    /// <b>Time:</b> O((NodeCount + EdgeCount) x alpha(NodeCount)).<br/>
    /// <b>Memory:</b> up to 12 x NodeCount bytes for the result; while it runs, 9 x NodeCount bytes
    /// more.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph.</param>
    /// <returns>The weakly connected components.</returns>
    public static NodePartition WccsOf(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;

        // 1) Union the endpoints of every edge; self-loops are no-ops.
        int[] parent = UnionFind.Singletons(n);
        byte[] rank = new byte[n];
        int[] adjacency = graph.OutAdj;
        int[] adjacencyStart = graph.OutStart;
        for (int u = 0; u < n; u++)
        {
            for (int ei = adjacencyStart[u], end = adjacencyStart[u + 1]; ei < end; ei++)
            {
                UnionFind.Union(parent, rank, u, adjacency[ei]);
            }
        }

        // 2) Dense component IDs, in order of each component's smallest node.
        var nodeToGroup = new int[n];
        var groupOfRoot = new int[n];
        Array.Fill(groupOfRoot, -1);
        int groupCount = 0;
        for (int i = 0; i < n; i++)
        {
            int root = UnionFind.Find(parent, i);
            if (groupOfRoot[root] < 0)
            {
                groupOfRoot[root] = groupCount++;
            }
            nodeToGroup[i] = groupOfRoot[root];
        }

        // 3) Members as CSR.
        var groups = ParentForest.GroupIndices(nodeToGroup, groupCount);
        return new NodePartition(graph, nodeToGroup, groups.Members, groups.Start);
    }

    /// <summary>
    /// Returns the connected components of <paramref name="graph"/> read as undirected. Equivalent
    /// to <c>WccsOf(graph)</c>.
    /// </summary>
    /// <remarks>
    /// <para>The two names let a call say which reading of the graph it means.</para>
    /// <para>
    /// <b>Time:</b> O((NodeCount + EdgeCount) x alpha(NodeCount)), as for <see cref="WccsOf"/>.<br/>
    /// <b>Memory:</b> up to 12 x NodeCount bytes for the result; while it runs, 9 x NodeCount bytes
    /// more.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph.</param>
    /// <returns>The connected components.</returns>
    public static NodePartition CcsOf(Graph graph) => WccsOf(graph);

    /// <summary>
    /// Returns the bridges, articulation points and biconnected components of
    /// <paramref name="graph"/>, read as undirected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All three come from one depth-first search, so there is no cheaper method for one of them
    /// alone; <see cref="Biconnectivity"/> describes them. The search follows the edges of every
    /// node in both directions, so it takes a <see cref="BiAdjacentGraph"/>. For a plain
    /// <see cref="Graph"/>, call <c>Components.BiconnectivityOf(BiAdjacentGraph.Of(graph))</c>.
    /// Uses
    /// <see href="https://en.wikipedia.org/wiki/Biconnected_component">the algorithm of Hopcroft and Tarjan</see>,
    /// without recursion.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> up to 8 x EdgeCount + 20 x NodeCount bytes for the result; while it runs,
    /// 8 x EdgeCount + 63 x NodeCount bytes more.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph.</param>
    /// <returns>The bridges, articulation points and biconnected components.</returns>
    public static Biconnectivity BiconnectivityOf(BiAdjacentGraph graph) => Biconnectivity.Of(graph);

    /// <summary>
    /// Returns the strong bridges and strong articulation points of <paramref name="graph"/>. These
    /// are the edges and nodes whose removal splits an SCC.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edges are followed in their direction, as by <see cref="SccsOf"/>;
    /// <see cref="StrongConnectivity"/> describes the results. The computation also follows them
    /// against their direction, so it takes a <see cref="BiAdjacentGraph"/>. For a plain
    /// <see cref="Graph"/>, call <c>Components.StrongConnectivityOf(BiAdjacentGraph.Of(graph))</c>.
    /// Uses the algorithm of Italiano, Laura and Santaroni, which finds both from the
    /// <see cref="Dominators">dominators</see> within each SCC, without recursion.
    /// </para>
    /// <para>
    /// <b>Time:</b> O((NodeCount + EdgeCount) log NodeCount).<br/>
    /// <b>Memory:</b> up to 28 x NodeCount bytes for the result; while it runs, about 80 x NodeCount
    /// bytes more.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph.</param>
    /// <returns>The strong bridges and strong articulation points.</returns>
    /// <exception cref="ArgumentException"><paramref name="graph"/> has more than 2^30 - 30 nodes.</exception>
    public static StrongConnectivity StrongConnectivityOf(BiAdjacentGraph graph) => StrongConnectivity.Of(graph);
}
