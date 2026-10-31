namespace Graphity;

/// <summary>
/// A <see cref="Graph"/> that also holds the in-nodes of every node, for algorithms that follow
/// edges against their direction.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Of"/> builds the in-adjacency of a graph - the in-nodes of every node. The result has
/// the node IDs and edge indices of the graph and shares the out-nodes of every node, called its
/// out-adjacency. Algorithms that need in-nodes, such as <see cref="Dominators"/>, take a
/// <see cref="BiAdjacentGraph"/>, so that the in-adjacency is built once and serves all of them.
/// <see cref="Transposed"/> reverses every edge without copying, and <see cref="UniAdjacentGraph"/>
/// drops the in-adjacency again.
/// </para>
/// <para>
/// The in-nodes of a node are in ascending order, like its out-nodes. A
/// <see cref="BiAdjacentGraph"/> equals a <see cref="Graph"/> with the same nodes and edges.
/// Instances are immutable and safe to share between threads.
/// </para>
/// <para>
/// <b>Memory:</b> 8 x (NodeCount + EdgeCount) bytes, half of it for the out-adjacency, which is
/// shared with the graph it was built from.
/// </para>
/// </remarks>
public sealed class BiAdjacentGraph : Graph
{
    // The in-adjacency in the layout of the out-adjacency: the in-nodes of node v are
    // InAdj[InStart[v]..InStart[v + 1] - 1], sorted ascending, with the sentinel
    // InStart[NodeCount] == EdgeCount. A position in InAdj is not an edge index.
    internal readonly int[] InStart;
    internal readonly int[] InAdj;

    // Takes the arrays as they are, neither copied nor checked: the caller guarantees the layout.
    internal BiAdjacentGraph(int[] outStart, int[] outAdj, int[] inStart, int[] inAdj)
        : base(outStart, outAdj)
    {
        InStart = inStart;
        InAdj = inAdj;
    }

    // The body of Of(Graph) for a graph without in-adjacency.
    private static BiAdjacentGraph WithInAdjacency(Graph graph)
    {
        int n = graph.NodeCount;
        int m = graph.EdgeCount;
        int[] outStart = graph.OutStart;
        int[] outAdj = graph.OutAdj;
        var inStart = new int[n + 1];
        var inAdj = new int[m];

        // 1) Count in-degrees.
        for (int e = 0; e < m; e++)
        {
            inStart[outAdj[e]]++;
        }

        // 2) Prefix sum: in-degrees to start offsets.
        int nextStart = 0;
        for (int v = 0; v < n; v++)
        {
            int inDegree = inStart[v];
            inStart[v] = nextStart;
            nextStart += inDegree;
        }

        // 3) Fill; inStart[target]++ doubles as write pointer. The ascending node scan leaves each
        //    in-list sorted.
        for (int v = 0; v < n; v++)
        {
            for (int e = outStart[v], end = outStart[v + 1]; e < end; e++)
            {
                inAdj[inStart[outAdj[e]]++] = v;
            }
        }

        // 4) Repair: step 3 shifted the offsets left by one; shifting right restores them.
        for (int v = n; v > 0; v--)
        {
            inStart[v] = inStart[v - 1];
        }
        inStart[0] = 0;

        return new BiAdjacentGraph(outStart, outAdj, inStart, inAdj);
    }

    /// <summary>
    /// Returns a <see cref="BiAdjacentGraph"/> with the nodes and edges of
    /// <paramref name="graph"/>, building its in-adjacency.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Node IDs and edge indices stay the same, and the out-adjacency is shared with
    /// <paramref name="graph"/>, not copied. The in-adjacency is built anew on every call, not
    /// cached: keep the result to use it more than once. If <paramref name="graph"/> already is a
    /// <see cref="BiAdjacentGraph"/>, returns it.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount), or O(1) if already bi-adjacent.<br/>
    /// <b>Memory:</b> +4 x (NodeCount + EdgeCount) bytes, or none if already bi-adjacent.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to build the in-adjacency for.</param>
    /// <returns>The graph with in-adjacency.</returns>
    public static BiAdjacentGraph Of(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return graph as BiAdjacentGraph ?? WithInAdjacency(graph);
    }

    /// <summary>
    /// Returns the in-degree of <paramref name="node"/>. That is the number of edges with
    /// <paramref name="node"/> as target.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The in-degree.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int InDegree(int node)
    {
        Checks.Index(node, InStart.Length - 1);
        return InStart[node + 1] - InStart[node];
    }

    /// <summary>
    /// Returns the in-nodes of <paramref name="node"/> in ascending order. These are the sources of
    /// the edges with <paramref name="node"/> as target.
    /// </summary>
    /// <remarks>
    /// <para>The result is a view of this graph's own array, not a copy.</para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The in-nodes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public SortedIntView InNodes(int node)
    {
        Checks.Index(node, InStart.Length - 1);
        return new SortedIntView(InAdj, InStart[node], InStart[node + 1]);
    }

    /// <summary>Returns whether the edge <c>inNode -> node</c> exists.</summary>
    /// <remarks><b>Time:</b> O(log InDegree(node)).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <param name="inNode">Node ID; a value out of range yields <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the edge exists.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public bool HasInNode(int node, int inNode)
    {
        Checks.Index(node, InStart.Length - 1);
        return SortedIntView.ContainsInRange(InAdj, InStart[node], InStart[node + 1], inNode);
    }

    /// <summary>Returns a <see cref="Graph"/> with the same nodes and edges, without the in-adjacency.</summary>
    /// <remarks>
    /// <para>
    /// The result shares the out-adjacency of this graph; nothing is copied. Once only the result is
    /// kept, the in-adjacency can be garbage-collected.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <returns>A graph over the out-adjacency of this graph.</returns>
    public override Graph UniAdjacentGraph() => new(OutStart, OutAdj);

    /// <summary>
    /// Returns the transposed graph as a view of this graph's arrays. The transposed graph has every
    /// edge reversed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Node IDs stay the same, edge indices do not: edge <c>e</c> of the transposed graph is in
    /// general not the reverse of edge <c>e</c> of this graph. <see cref="TransposedEdgeIndex"/>
    /// translates one edge index, and <see cref="Graphs.TransposedEdgeMapping"/> translates all of
    /// them.
    /// </para>
    /// <para>
    /// A plain <see cref="Graph"/> needs its in-adjacency first:
    /// <c>BiAdjacentGraph.Of(graph).Transposed()</c>.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <example>
    /// Here, the packages of a program that depend on <c>model</c>, directly or indirectly, are the
    /// ones reachable from it against the direction of the dependencies:
    /// <!-- example: Transposing.cs#transposed -->
    /// <code language="csharp">
    /// int model = 4;
    /// BiAdjacentGraph usedBy = BiAdjacentGraph.Of(dependencies).Transposed();
    /// SortedIntView affected = Graphs.ReachableFrom(usedBy, model);
    /// // affected: [0, 2, 3, 4, 5] - app, ui, service, model and storage
    /// </code>
    /// </example>
    /// <returns>The transposed graph.</returns>
    public BiAdjacentGraph Transposed() => new(InStart, InAdj, OutStart, OutAdj);

    /// <summary>
    /// Returns the edge index that edge <paramref name="edgeIndex"/> has in the transposed graph.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called on <see cref="Transposed"/>, it translates back:
    /// <c>Transposed().TransposedEdgeIndex(TransposedEdgeIndex(e)) == e</c>. To translate every edge,
    /// <see cref="Graphs.TransposedEdgeMapping"/> needs one pass instead of a search per edge; it maps
    /// from the transposed graph to this one.
    /// </para>
    /// <para><b>Time:</b> O(log NodeCount).</para>
    /// </remarks>
    /// <param name="edgeIndex">Edge index in <c>0..EdgeCount-1</c>.</param>
    /// <returns>The edge index in the transposed graph.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edgeIndex"/> is out of range.</exception>
    public int TransposedEdgeIndex(int edgeIndex)
    {
        Checks.Index(edgeIndex, EdgeCount);
        int source = EdgeSource(edgeIndex);
        int target = OutAdj[edgeIndex];
        return SortedIntView.IndexOfInRange(InAdj, InStart[target], InStart[target + 1], source);
    }

    /// <summary>
    /// Returns the degree of <paramref name="node"/>. Equivalent to
    /// <c>InDegree(node) + OutDegree(node)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It counts edges, not neighbors: a self-loop counts twice, and so do the edges
    /// <c>node -> v</c> and <c>v -> node</c>, although <c>v</c> is one neighbor.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The degree.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int Degree(int node) => InDegree(node) + OutDegree(node);

    /// <summary>
    /// Returns the neighbors of <paramref name="node"/> in ascending order and without duplicates.
    /// These are its in-nodes and out-nodes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="InNodes"/> and <see cref="Graph.OutNodes"/>, which are views, the result is a
    /// copy, merged on every call.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(InDegree(node) + OutDegree(node)).<br/>
    /// <b>Memory:</b> up to 4 x (InDegree(node) + OutDegree(node)) bytes for the result; while it
    /// runs, up to as much again if a neighbor is both an in-node and an out-node.
    /// </para>
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The neighbors.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public SortedIntView Neighbors(int node)
    {
        Checks.Index(node, InStart.Length - 1);
        return new SortedIntView(SortedIntView.UnionInRange(
            OutAdj, OutStart[node], OutStart[node + 1],
            InAdj, InStart[node], InStart[node + 1]));
    }

    /// <summary>
    /// Returns whether <paramref name="neighbor"/> is a neighbor of <paramref name="node"/>.
    /// Equivalent to <c>HasOutNode(node, neighbor) || HasInNode(node, neighbor)</c>.
    /// </summary>
    /// <remarks><b>Time:</b> O(log OutDegree(node) + log InDegree(node)).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <param name="neighbor">Node ID; a value out of range yields <see langword="false"/>.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="neighbor"/> is an in-node or an out-node of
    /// <paramref name="node"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public bool HasNeighbor(int node, int neighbor) => HasOutNode(node, neighbor) || HasInNode(node, neighbor);

    /// <summary>
    /// Returns whether <paramref name="node"/> is a root node. Equivalent to
    /// <c>InDegree(node) == 0</c>.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Graphs.RootNodes"/> returns all root nodes.</para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns><see langword="true"/> if no edge has <paramref name="node"/> as target.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public bool IsRootNode(int node) => InDegree(node) == 0;
}
