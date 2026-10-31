namespace Graphity;

/// <summary>
/// Access to the arrays in which a graph keeps its edges, for algorithm code that loops over all
/// edges. These arrays are called the CSR (compressed sparse row) layout of the graph.
/// </summary>
/// <remarks>
/// <para>
/// Each method returns a span over the graph's own array, not a copy. The out-nodes of node <c>v</c>
/// are <c>OutAdjacency[OutAdjacencyStart[v]..OutAdjacencyStart[v + 1] - 1]</c>, in ascending order,
/// and the position of an out-node in <c>OutAdjacency</c> is the edge index of its edge.
/// <c>OutAdjacencyStart</c> has <c>NodeCount + 1</c> values, from <c>0</c> to <c>EdgeCount</c>. A
/// loop loads the two spans into local variables once and reads them without a method call or an
/// argument check per edge.
/// </para>
/// <para>
/// A <see cref="BiAdjacentGraph"/> also holds the in-nodes of every node, called its in-adjacency.
/// The in-adjacency has the same layout, in <see cref="InAdjacencyStart"/> and
/// <see cref="InAdjacency"/>. A position in <c>InAdjacency</c> is not an edge index of the graph,
/// though, but of its transposed graph; <see cref="Graphs.TransposedEdgeMapping"/> translates all of
/// them in one pass.
/// </para>
/// <para>
/// The spans cannot modify the arrays. That keeps what every algorithm relies on: that the graph is
/// immutable and safe to share between threads, and that the out-nodes of each node are sorted. A
/// span lives on the stack only; to keep an array beyond a method, copy it with <c>ToArray()</c> or
/// keep the graph and ask again. The algorithms of this library read the arrays the same way. Code
/// that reads the out-nodes of a few nodes is better served by <see cref="Graph.OutNodes"/> and the
/// edge index methods of <see cref="Graph"/>, which check their arguments.
/// </para>
/// </remarks>
/// <example>
/// Here, <c>calls</c> holds the calls between the methods of a program, and the loop counts the
/// callers of each method:
/// <!-- example: BuildingAndReading.cs#loop -->
/// <code language="csharp">
/// ReadOnlySpan&lt;int> start = Csr.OutAdjacencyStart(calls);
/// ReadOnlySpan&lt;int> callees = Csr.OutAdjacency(calls);
/// int[] callerCount = new int[calls.NodeCount];
/// for (int method = 0; method &lt; calls.NodeCount; method++)
/// {
///     for (int edge = start[method], end = start[method + 1]; edge &lt; end; edge++)
///     {
///         callerCount[callees[edge]]++;
///     }
/// }
/// // callerCount: 3 for format, 2 for parse, 0 for main, 1 for every other method
/// </code>
/// </example>
public static class Csr
{
    /// <summary>
    /// Returns where the out-nodes of each node start in <see cref="OutAdjacency"/>, followed by
    /// EdgeCount.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="graph">The graph.</param>
    /// <returns>A read-only span over the graph's own array, of length NodeCount + 1.</returns>
    public static ReadOnlySpan<int> OutAdjacencyStart(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return graph.OutStart;
    }

    /// <summary>
    /// Returns the out-nodes of all nodes, node after node, at the positions of their edge indices.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="graph">The graph.</param>
    /// <returns>A read-only span over the graph's own array, of length EdgeCount.</returns>
    public static ReadOnlySpan<int> OutAdjacency(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return graph.OutAdj;
    }

    /// <summary>
    /// Returns where the in-nodes of each node start in <see cref="InAdjacency"/>, followed by
    /// EdgeCount.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="graph">The graph.</param>
    /// <returns>A read-only span over the graph's own array, of length NodeCount + 1.</returns>
    public static ReadOnlySpan<int> InAdjacencyStart(BiAdjacentGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return graph.InStart;
    }

    /// <summary>Returns the in-nodes of all nodes, node after node.</summary>
    /// <remarks>
    /// <para>
    /// A position in this span is the edge index of the edge in the transposed graph, not in
    /// <paramref name="graph"/>.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="graph">The graph.</param>
    /// <returns>A read-only span over the graph's own array, of length EdgeCount.</returns>
    public static ReadOnlySpan<int> InAdjacency(BiAdjacentGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return graph.InAdj;
    }
}
