using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Graphity;

/// <summary>
/// A list of edges of a graph, each with its source, target and edge index. Algorithms such as
/// <see cref="BackEdges.Of"/> and <see cref="Biconnectivity.Bridges"/> return it.
/// </summary>
/// <remarks>
/// <para>
/// The edge at position <c>p</c>, in <c>0..Count-1</c>, is <c>SourceNodes[p] -> TargetNodes[p]</c>,
/// and <c>EdgeIndices[p]</c> is its edge index in the graph the algorithm ran on. The edge index
/// finds the data of the edge in the caller's arrays indexed by edge index, without a search. Each
/// method that returns <see cref="Edges"/> says in which order the edges come.
/// <see cref="GraphBuilder.AddEdges"/> adds them to a graph under construction.
/// </para>
/// <para>
/// Each of the three lists is an <see cref="ImmutableArray{T}"/>. Instances are immutable and safe
/// to share between threads.
/// </para>
/// <para><b>Memory:</b> 12 bytes per edge.</para>
/// </remarks>
/// <example>
/// Here, the back edges of the calls between the methods of a program are its recursive calls.
/// <c>names</c> holds the names of the methods, and <c>siteCount</c> holds the number of call sites
/// of each call, by edge index:
/// <!-- example: BuildingAndReading.cs#edges -->
/// <code language="csharp">
/// Edges recursive = BackEdges.Of(calls);
/// for (int p = 0; p &lt; recursive.Count; p++)
/// {
///     int caller = recursive.SourceNodes[p];
///     int callee = recursive.TargetNodes[p];
///     int edge = recursive.EdgeIndices[p];
///     output.WriteLine($"{names[caller]} -> {names[callee]}: {siteCount[edge]}");
/// }
/// // parse -> parse: 2
/// // format -> format: 1
/// </code>
/// </example>
public sealed class Edges
{
    private static readonly Edges EmptyEdges = new([], [], []);

    // The three lists have the same length; nobody else holds their arrays.
    internal Edges(ImmutableArray<int> sourceNodes, ImmutableArray<int> targetNodes, ImmutableArray<int> edgeIndices)
    {
        SourceNodes = sourceNodes;
        TargetNodes = targetNodes;
        EdgeIndices = edgeIndices;
    }

    // For three arrays of the same length that nobody else holds.
    internal static Edges Wrap(int[] sourceNodes, int[] targetNodes, int[] edgeIndices) =>
        new(ImmutableCollectionsMarshal.AsImmutableArray(sourceNodes),
            ImmutableCollectionsMarshal.AsImmutableArray(targetNodes),
            ImmutableCollectionsMarshal.AsImmutableArray(edgeIndices));

    internal static Edges Empty => EmptyEdges;

    /// <summary>Returns the edges of one type from an edge classification, in edge index order.</summary>
    /// <remarks>
    /// <para>
    /// Each call goes through every edge, however few have the type: call it once per type and keep
    /// the result.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 12 bytes per edge of the result.
    /// </para>
    /// </remarks>
    /// <param name="edgeTypes">The classification of the edges of a graph.</param>
    /// <param name="type">The edge type to collect.</param>
    /// <returns>The edges of type <paramref name="type"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a defined <see cref="EdgeType"/>.</exception>
    public static Edges Of(EdgeTypes edgeTypes, EdgeType type)
    {
        ArgumentNullException.ThrowIfNull(edgeTypes);
        if (type > EdgeType.Cross)
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "not an EdgeType");
        }
        // Counts the edges of the type first, so that the arrays of the result get their exact
        // length, then fills them in one scan over all edges.
        Graph graph = edgeTypes.Graph;
        EdgeType[] types = edgeTypes.Types;
        int count = 0;
        foreach (EdgeType t in types)
        {
            if (t == type)
            {
                count++;
            }
        }
        if (count == 0)
        {
            return EmptyEdges;
        }
        var sources = new int[count];
        var targets = new int[count];
        var edgeIndices = new int[count];
        int[] adjacency = graph.OutAdj;
        int[] adjacencyStart = graph.OutStart;
        int n = graph.NodeCount;
        int p = 0;
        for (int node = 0; node < n; node++)
        {
            for (int e = adjacencyStart[node], end = adjacencyStart[node + 1]; e < end; e++)
            {
                if (types[e] == type)
                {
                    sources[p] = node;
                    targets[p] = adjacency[e];
                    edgeIndices[p] = e;
                    p++;
                }
            }
        }
        return Wrap(sources, targets, edgeIndices);
    }

    /// <summary>Gets the source of each edge, by position.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public ImmutableArray<int> SourceNodes { get; }

    /// <summary>Gets the target of each edge, by position.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public ImmutableArray<int> TargetNodes { get; }

    /// <summary>Gets the edge index of each edge in the graph the algorithm ran on, by position.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public ImmutableArray<int> EdgeIndices { get; }

    /// <summary>Gets the number of edges.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public int Count => SourceNodes.Length;

    /// <summary>Gets whether there are no edges. Equivalent to <c>Count == 0</c>.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public bool IsEmpty => SourceNodes.Length == 0;

    // Returns count edges of graph, given as parallel arrays in their direction in the graph, in edge
    // index order - by source, then target - and drops repeated edges if dropDuplicates is set,
    // keeping the first. The arrays are read, not kept, and may be longer than count. An edge index
    // of -1 is looked up with one binary search in the adjacency of the source; callers pass the
    // index wherever they know it.
    //
    // Two stable counting passes, first by target, then by source, sort in O(NodeCount + count)
    // without comparisons. Input already in order, or in exactly reverse order - the bridges of a
    // path arrive that way - is copied instead; the check stops at the first edge out of line. It
    // takes 4 x NodeCount bytes for the counts and 24 x count bytes for the intermediate and final
    // lists.
    internal static Edges SortedEdges(Graph graph, int[] sources, int[] targets, int[] edgeIndices,
        int count, bool dropDuplicates)
    {
        if (count == 0)
        {
            return EmptyEdges;
        }
        var sortedSources = new int[count];
        var sortedTargets = new int[count];
        var sortedEdgeIndices = new int[count];
        int direction = Direction(sources, targets, count);
        if (direction > 0)
        {
            Array.Copy(sources, sortedSources, count);
            Array.Copy(targets, sortedTargets, count);
            Array.Copy(edgeIndices, sortedEdgeIndices, count);
        }
        else if (direction < 0)
        {
            for (int i = 0, from = count - 1; i < count; i++, from--)
            {
                sortedSources[i] = sources[from];
                sortedTargets[i] = targets[from];
                sortedEdgeIndices[i] = edgeIndices[from];
            }
        }
        else
        {
            CountingSort(sources, targets, edgeIndices, count, graph.NodeCount,
                sortedSources, sortedTargets, sortedEdgeIndices);
        }
        int kept = count;
        if (dropDuplicates)
        {
            kept = 0;
            for (int i = 0; i < count; i++)
            {
                if (kept > 0 && sortedSources[kept - 1] == sortedSources[i]
                    && sortedTargets[kept - 1] == sortedTargets[i])
                {
                    if (sortedEdgeIndices[kept - 1] < 0)
                    {
                        sortedEdgeIndices[kept - 1] = sortedEdgeIndices[i];
                    }
                    continue;
                }
                sortedSources[kept] = sortedSources[i];
                sortedTargets[kept] = sortedTargets[i];
                sortedEdgeIndices[kept] = sortedEdgeIndices[i];
                kept++;
            }
        }
        if (kept < count)
        {
            sortedSources = sortedSources[..kept];
            sortedTargets = sortedTargets[..kept];
            sortedEdgeIndices = sortedEdgeIndices[..kept];
        }
        for (int p = 0; p < kept; p++)
        {
            if (sortedEdgeIndices[p] < 0)
            {
                sortedEdgeIndices[p] = graph.EdgeIndexOf(sortedSources[p], sortedTargets[p]);
            }
            Debug.Assert(sortedEdgeIndices[p] == graph.EdgeIndexOf(sortedSources[p], sortedTargets[p]),
                $"{sortedSources[p]} -> {sortedTargets[p]} is not edge {sortedEdgeIndices[p]}");
        }
        return Wrap(sortedSources, sortedTargets, sortedEdgeIndices);
    }

    // 1 if the edges are in order, -1 if exactly reversed, otherwise 0.
    private static int Direction(int[] sources, int[] targets, int count)
    {
        int direction = 0;
        for (int i = 1; i < count; i++)
        {
            int bySource = sources[i - 1].CompareTo(sources[i]);
            int step = bySource != 0 ? bySource : targets[i - 1].CompareTo(targets[i]);
            if (step == 0)
            {
                continue;
            }
            int sign = step < 0 ? 1 : -1;
            if (direction == 0)
            {
                direction = sign;
            }
            else if (direction != sign)
            {
                return 0;
            }
        }
        return direction == 0 ? 1 : direction;
    }

    // Two stable counting passes, by target and then by source, into the sorted arrays.
    private static void CountingSort(int[] sources, int[] targets, int[] edgeIndices, int count,
        int nodeCount, int[] sortedSources, int[] sortedTargets, int[] sortedEdgeIndices)
    {
        var start = new int[nodeCount + 1];
        for (int i = 0; i < count; i++)
        {
            start[targets[i] + 1]++;
        }
        for (int v = 0; v < nodeCount; v++)
        {
            start[v + 1] += start[v];
        }
        var byTargetSources = new int[count];
        var byTargetTargets = new int[count];
        var byTargetEdgeIndices = new int[count];
        for (int i = 0; i < count; i++)
        {
            int slot = start[targets[i]]++;
            byTargetSources[slot] = sources[i];
            byTargetTargets[slot] = targets[i];
            byTargetEdgeIndices[slot] = edgeIndices[i];
        }
        Array.Clear(start);
        for (int i = 0; i < count; i++)
        {
            start[byTargetSources[i] + 1]++;
        }
        for (int v = 0; v < nodeCount; v++)
        {
            start[v + 1] += start[v];
        }
        for (int i = 0; i < count; i++)
        {
            int source = byTargetSources[i];
            int slot = start[source]++;
            sortedSources[slot] = source;
            sortedTargets[slot] = byTargetTargets[i];
            sortedEdgeIndices[slot] = byTargetEdgeIndices[i];
        }
    }
}
