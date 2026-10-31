using System.Globalization;
using System.Text;

namespace Graphity;

/// <summary>
/// A directed graph, which is the input of the algorithms in this library.
/// </summary>
/// <remarks>
/// <para>
/// Nodes are the <c>int</c> IDs <c>0..NodeCount-1</c>. Edges are numbered by edge index
/// <c>0..EdgeCount-1</c>, ordered by source and then by target. No two edges have the same source
/// and target; self-loops are allowed. The graph holds nothing but this structure: names, weights
/// and any other data live in arrays of the caller, indexed by node ID or by edge index.
/// </para>
/// <para>
/// <see cref="GraphBuilder"/> builds a graph from edges in any order. <see cref="Of"/> takes the
/// two arrays of a CSR (compressed sparse row) layout. A graph keeps its edges in this form, and
/// <see cref="Csr"/> hands the arrays to algorithm code. <see cref="WriteTo"/> and
/// <see cref="ReadFrom"/> store a graph in a stream and load it again.
/// </para>
/// <para>
/// A <see cref="Graph"/> holds the out-nodes of every node, called its out-adjacency. Algorithms
/// that also need the in-nodes take a <see cref="BiAdjacentGraph"/>, which
/// <see cref="BiAdjacentGraph.Of"/> builds and which also gives the transposed graph. Algorithms
/// are not methods of the graph but classes of their own, such as <see cref="Components"/>,
/// <see cref="TopOrder"/> and <see cref="Dominators"/>; the README lists them by topic.
/// </para>
/// <para>
/// Instances are immutable and safe to share between threads. No other assembly can derive from
/// this class; <see cref="BiAdjacentGraph"/> is its only subclass.
/// </para>
/// <para>
/// <b>Memory:</b> 4 x (NodeCount + EdgeCount) bytes.
/// </para>
/// <para>
/// A graph has at most <c>Array.MaxLength - 1</c> nodes and <c>Array.MaxLength</c> edges
/// (2,147,483,590 and 2,147,483,591).
/// </para>
/// </remarks>
public class Graph : IEquatable<Graph>
{
    // ToString runs implicitly, in debuggers and logs, so it lists a bounded part of the graph: at
    // most this many nodes, and this many out-nodes over all listed nodes together.
    private const int ToStringNodes = 16;
    private const int ToStringTargets = 64;

    // The longest listing the two limits allow stays under 1000 characters, even with ten-digit
    // counts and IDs, so the builder never grows.
    private const int ToStringCapacity = 1024;

    // The CSR layout: the out-nodes of node v are OutAdj[OutStart[v]..OutStart[v + 1] - 1], sorted
    // ascending, and the position of an out-node in OutAdj is the edge index of its edge. The
    // sentinel OutStart[NodeCount] == EdgeCount makes OutStart[v + 1] valid for every node without a
    // branch. Internal, for Csr and the algorithms of this assembly.
    internal readonly int[] OutStart;
    internal readonly int[] OutAdj;

    // Takes the arrays as they are, neither copied nor checked: the caller guarantees the layout.
    internal Graph(int[] outStart, int[] outAdj)
    {
        OutStart = outStart;
        OutAdj = outAdj;
    }

    /// <summary>
    /// Returns a graph from the two arrays of its CSR layout, as <see cref="Csr"/> returns them and
    /// <see cref="WriteTo"/> writes them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The out-nodes of node <c>v</c> are
    /// <c>outAdjacency[outAdjacencyStart[v]..outAdjacencyStart[v + 1] - 1]</c>, and the position of
    /// an out-node in <paramref name="outAdjacency"/> is the edge index of its edge. Both spans are
    /// copied and checked: the offsets start at 0, never decrease and end at
    /// <c>outAdjacency.Length</c>, and the out-nodes of each node are strictly ascending. For edges
    /// in any order or with duplicates, use <see cref="GraphBuilder"/>.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 4 x (NodeCount + EdgeCount) bytes for the copies, which the result keeps.
    /// </para>
    /// </remarks>
    /// <example>
    /// For example, the control flow of a method with a loop:
    /// <!-- example: Building.cs#of -->
    /// <code language="csharp">
    /// // blocks 0..5: entry, header, body, then, next, exit
    /// int[] outAdjacencyStart = [0, 1, 3, 5, 6, 7, 7];
    /// int[] outAdjacency =
    /// [
    ///     1,       // entry  -> header
    ///     2, 5,    // header -> body, exit
    ///     3, 4,    // body   -> then, next
    ///     4,       // then   -> next
    ///     1,       // next   -> header
    /// ];
    /// Graph cfg = Graph.Of(outAdjacencyStart, outAdjacency);
    /// </code>
    /// </example>
    /// <param name="outAdjacencyStart">
    /// Where the out-nodes of each node start in <paramref name="outAdjacency"/>, followed by
    /// EdgeCount; length NodeCount + 1.
    /// </param>
    /// <param name="outAdjacency">The out-nodes of all nodes, node after node; length EdgeCount.</param>
    /// <returns>The graph.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An out-node is not in <c>0..NodeCount-1</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="outAdjacencyStart"/> is empty, does not start at 0, decreases or does not end
    /// at <c>outAdjacency.Length</c>, the out-nodes of a node are not strictly ascending, or the
    /// spans hold more nodes or edges than a graph can have.
    /// </exception>
    public static Graph Of(ReadOnlySpan<int> outAdjacencyStart, ReadOnlySpan<int> outAdjacency)
    {
        // An array cannot exceed the limits, a span over other memory can: check before the copy.
        if (outAdjacencyStart.Length == 0)
        {
            throw EmptyStart(nameof(outAdjacencyStart));
        }
        CheckCounts(outAdjacencyStart.Length - 1, outAdjacency.Length);
        return OfOwned(outAdjacencyStart.ToArray(), outAdjacency.ToArray(), named: true);
    }

    // Of without the copy, for arrays that nobody else holds, as ReadFrom reads them. ReadFrom
    // passes named = false: its caller passed no such parameters, so the exceptions name none and
    // carry no actual value, and their messages are the bare text, as in Java.
    internal static Graph OfOwned(int[] outAdjacencyStart, int[] outAdjacency, bool named)
    {
        string? startName = named ? nameof(outAdjacencyStart) : null;
        string? adjacencyName = named ? nameof(outAdjacency) : null;
        if (outAdjacencyStart.Length == 0)
        {
            throw EmptyStart(startName);
        }
        int n = outAdjacencyStart.Length - 1;
        int m = outAdjacency.Length;
        CheckCounts(n, m);
        if (outAdjacencyStart[0] != 0)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"outAdjacencyStart[0] must be 0, but was {outAdjacencyStart[0]}"),
                startName);
        }
        // Offsets first: once they rise from 0 to m, every list below lies inside outAdjacency.
        for (int v = 0; v < n; v++)
        {
            if (outAdjacencyStart[v + 1] < outAdjacencyStart[v])
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                    $"outAdjacencyStart decreases after node {v}: "
                    + $"{outAdjacencyStart[v]} > {outAdjacencyStart[v + 1]}"), startName);
            }
        }
        if (outAdjacencyStart[n] != m)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"outAdjacencyStart[{n}] must be outAdjacency.Length {m}, "
                + $"but was {outAdjacencyStart[n]}"), startName);
        }
        for (int v = 0; v < n; v++)
        {
            int end = outAdjacencyStart[v + 1];
            int previous = -1;
            for (int e = outAdjacencyStart[v]; e < end; e++)
            {
                int target = outAdjacency[e];
                if ((uint)target >= (uint)n)
                {
                    throw new ArgumentOutOfRangeException(adjacencyName, named ? target : null,
                        string.Create(CultureInfo.InvariantCulture,
                            $"out-node {target} of node {v} at position {e} is not in 0..{n - 1}"));
                }
                if (target <= previous)
                {
                    throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                        $"the out-nodes of node {v} must be strictly ascending, "
                        + $"but {target} follows {previous}"), adjacencyName);
                }
                previous = target;
            }
        }
        return new Graph(outAdjacencyStart, outAdjacency);
    }

    /// <summary>
    /// Writes this graph to <paramref name="output"/>, for <see cref="ReadFrom"/> to read back with
    /// the same node IDs and edge indices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the structure is written. Data in arrays indexed by node ID or edge index stays valid
    /// for the graph read back and can follow it in the same stream. A <see cref="BiAdjacentGraph"/>
    /// writes its out-adjacency only.
    /// </para>
    /// <para>
    /// The format is the two arrays that <see cref="Of"/> takes, preceded by the node count and the
    /// edge count. Every field is a signed 32-bit integer in little-endian byte order:
    /// </para>
    /// <code>
    /// offset in bytes          size in bytes         field
    /// 0                        4                     nodeCount
    /// 4                        4                     edgeCount
    /// 8                        4 x (nodeCount + 1)   outAdjacencyStart
    /// 8 + 4 x (nodeCount + 1)  4 x edgeCount         outAdjacency
    /// </code>
    /// <para>
    /// The arrays follow the rules of <see cref="Of"/>. Nothing else is written: no padding,
    /// checksum, magic number or version. So a graph takes exactly 8 + 4 x (nodeCount + 1 +
    /// edgeCount) bytes, and <see cref="ReadFrom"/> recognizes foreign data only where it does not
    /// form a valid graph. Every implementation of Graphity reads and writes this format, so a
    /// graph written in one language can be read in another.
    /// </para>
    /// <para>
    /// Writes in blocks of up to 64 KiB, so <paramref name="output"/> needs a
    /// <see cref="BufferedStream"/> only if other data shares it in small pieces.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> none on a little-endian machine, where the blocks are written straight from
    /// the arrays of the graph; a 64 KiB buffer otherwise.
    /// </para>
    /// </remarks>
    /// <example>
    /// Here, the names of the methods of a program follow the calls between them:
    /// <!-- example: Storing.cs#writeTo -->
    /// <code language="csharp">
    /// using (var output = new BinaryWriter(File.Create(file)))
    /// {
    ///     calls.WriteTo(output.BaseStream);
    ///     foreach (string name in names)
    ///     {
    ///         output.Write(name);
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <param name="output">The stream to write to; not closed.</param>
    /// <exception cref="IOException">Writing to the stream fails.</exception>
    public void WriteTo(Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);
        CsrStreams.Write(OutStart, OutAdj, output);
    }

    /// <summary>
    /// Reads a graph that <see cref="WriteTo"/> wrote, with the same node IDs and edge indices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads exactly the bytes of the graph and leaves <paramref name="input"/> right after them,
    /// where data written after the graph follows. The result has no in-adjacency, even if the
    /// stream was written from a <see cref="BiAdjacentGraph"/> and even when called as
    /// <c>BiAdjacentGraph.ReadFrom(input)</c>; <see cref="BiAdjacentGraph.Of"/> builds it again.
    /// </para>
    /// <para>
    /// The arrays are checked as by <see cref="Of"/>, but not copied. They grow with the data that
    /// arrives, so a header that announces more than the stream holds ends in an
    /// <see cref="EndOfStreamException"/>, not in an allocation of the announced size. Reads in
    /// blocks of up to 64 KiB, so <paramref name="input"/> needs a <see cref="BufferedStream"/> only
    /// if other data shares it in small pieces.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 4 x (NodeCount + EdgeCount) bytes for the result and up to a 64 KiB buffer;
    /// while an array grows, briefly up to 4 x max(NodeCount, EdgeCount) bytes more.
    /// </para>
    /// </remarks>
    /// <example>
    /// Here, the names of the methods of a program follow the graph:
    /// <!-- example: Storing.cs#readFrom -->
    /// <code language="csharp">
    /// Graph calls;
    /// string[] names;
    /// using (var input = new BinaryReader(File.OpenRead(file)))
    /// {
    ///     calls = Graph.ReadFrom(input.BaseStream);
    ///     // equal to the graph written, edge indices included
    ///     names = new string[calls.NodeCount];
    ///     for (int method = 0; method &lt; names.Length; method++)
    ///     {
    ///         names[method] = input.ReadString();
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <param name="input">The stream to read from; not closed.</param>
    /// <returns>The graph.</returns>
    /// <exception cref="EndOfStreamException">The stream ends before the graph does.</exception>
    /// <exception cref="InvalidDataException">
    /// A count in the header is negative or larger than a graph can have, or the arrays do not form
    /// a graph that <see cref="Of"/> accepts, whose exception is then the inner exception.
    /// </exception>
    /// <exception cref="IOException">Reading from the stream fails.</exception>
    public static Graph ReadFrom(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var (start, adjacency) = CsrStreams.Read(input);
        try
        {
            return OfOwned(start, adjacency, named: false);
        }
        catch (ArgumentException e)
        {
            throw new InvalidDataException($"the stream does not hold a valid graph: {e.Message}", e);
        }
    }

    private static ArgumentException EmptyStart(string? paramName) =>
        new("outAdjacencyStart must hold nodeCount + 1 offsets, but is empty", paramName);

    // Rejects more nodes than ArrayLimits.MaxNodeCount and more edges than
    // ArrayLimits.MaxArrayLength. Arrays never exceed them in .NET, spans can.
    private static void CheckCounts(int n, int m)
    {
        if (n > ArrayLimits.MaxNodeCount || m > ArrayLimits.MaxArrayLength)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"a graph holds at most {ArrayLimits.MaxNodeCount} nodes and "
                + $"{ArrayLimits.MaxArrayLength} edges, but these arrays hold {n} and {m}"));
        }
    }

    /// <summary>Gets the number of nodes.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public int NodeCount => OutStart.Length - 1;

    /// <summary>Gets the number of edges.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public int EdgeCount => OutAdj.Length;

    /// <summary>Gets whether this graph has no nodes. Equivalent to <c>NodeCount == 0</c>.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public bool IsEmpty => OutStart.Length == 1;

    /// <summary>
    /// Returns the out-degree of <paramref name="node"/>. That is the number of edges with
    /// <paramref name="node"/> as source.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The out-degree.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int OutDegree(int node)
    {
        Checks.Index(node, OutStart.Length - 1);
        return OutStart[node + 1] - OutStart[node];
    }

    /// <summary>
    /// Returns the out-nodes of <paramref name="node"/> in ascending order. These are the targets of
    /// the edges with <paramref name="node"/> as source.
    /// </summary>
    /// <remarks>
    /// <para>The result is a view of this graph's own array, not a copy.</para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The out-nodes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public SortedIntView OutNodes(int node)
    {
        Checks.Index(node, OutStart.Length - 1);
        return new SortedIntView(OutAdj, OutStart[node], OutStart[node + 1]);
    }

    /// <summary>
    /// Returns the start of the edge index range of the out-edges of <paramref name="node"/>. The
    /// out-edges are the edges with <paramref name="node"/> as source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Their edge indices are <c>OutEdgeStart(node)..OutEdgeEnd(node)-1</c>, in the order of
    /// <see cref="OutNodes"/>; the range is empty if <paramref name="node"/> has no out-edges. A loop
    /// over the range reads the data of each edge from arrays indexed by edge index.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <example>
    /// Here, the loop lists the methods that <c>run</c> calls, with the number of call sites of each
    /// call in <c>siteCount</c>:
    /// <!-- example: EdgeData.cs#outEdges -->
    /// <code language="csharp">
    /// for (int edge = calls.OutEdgeStart(run); edge &lt; calls.OutEdgeEnd(run); edge++)
    /// {
    ///     output.WriteLine($"{names[calls.EdgeTarget(edge)]}: {siteCount[edge]}");
    /// }
    /// // load: 1
    /// // save: 1
    /// // show: 2
    /// </code>
    /// </example>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The start of the range.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int OutEdgeStart(int node)
    {
        Checks.Index(node, OutStart.Length - 1);
        return OutStart[node];
    }

    /// <summary>
    /// Returns the end, exclusive, of the edge index range of the out-edges of
    /// <paramref name="node"/>; see <see cref="OutEdgeStart"/>.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The end of the range, exclusive.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int OutEdgeEnd(int node)
    {
        Checks.Index(node, OutStart.Length - 1);
        return OutStart[node + 1];
    }

    /// <summary>Returns the node IDs <c>0..NodeCount-1</c> in ascending order.</summary>
    /// <remarks><b>Time:</b> O(1) to create the sequence.</remarks>
    /// <returns>The node IDs.</returns>
    public IEnumerable<int> Nodes() => Enumerable.Range(0, NodeCount);

    /// <summary>
    /// Calls <paramref name="action"/> for every edge with its source, target and edge index, in
    /// edge index order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The source comes with each edge at no cost, whereas <see cref="EdgeSource"/> searches for it.
    /// </para>
    /// <para><b>Time:</b> O(NodeCount + EdgeCount).</para>
    /// </remarks>
    /// <param name="action">Called with <c>(source, target, edgeIndex)</c> for each edge.</param>
    public void ForEachEdge(EdgeConsumer action)
    {
        ArgumentNullException.ThrowIfNull(action);
        int[] start = OutStart;
        int[] adj = OutAdj;
        int n = start.Length - 1;
        for (int source = 0; source < n; source++)
        {
            for (int e = start[source], end = start[source + 1]; e < end; e++)
            {
                action(source, adj[e], e);
            }
        }
    }

    /// <summary>Returns the target of edge <paramref name="edgeIndex"/>.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="edgeIndex">Edge index in <c>0..EdgeCount-1</c>.</param>
    /// <returns>The target node ID.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edgeIndex"/> is out of range.</exception>
    public int EdgeTarget(int edgeIndex)
    {
        Checks.Index(edgeIndex, OutAdj.Length);
        return OutAdj[edgeIndex];
    }

    /// <summary>Returns the source of edge <paramref name="edgeIndex"/>.</summary>
    /// <remarks>
    /// <para>
    /// Unlike the target, the source is searched for; <see cref="ForEachEdge"/> gives every edge
    /// with its source without a search.
    /// </para>
    /// <para><b>Time:</b> O(log NodeCount).</para>
    /// </remarks>
    /// <param name="edgeIndex">Edge index in <c>0..EdgeCount-1</c>.</param>
    /// <returns>The source node ID.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edgeIndex"/> is out of range.</exception>
    public int EdgeSource(int edgeIndex)
    {
        Checks.Index(edgeIndex, OutAdj.Length);
        // The largest node with OutStart[node] <= edgeIndex: a node without out-edges shares its
        // offset with the next one.
        int lo = 0;
        int hi = NodeCount - 1;
        while (lo < hi)
        {
            int mid = (int)((uint)(lo + hi + 1) >> 1);
            if (OutStart[mid] <= edgeIndex)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return lo;
    }

    /// <summary>Returns whether the edge <c>node -> outNode</c> exists.</summary>
    /// <remarks><b>Time:</b> O(log OutDegree(node)).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <param name="outNode">Node ID; a value out of range yields <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the edge exists.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public bool HasOutNode(int node, int outNode)
    {
        Checks.Index(node, OutStart.Length - 1);
        return SortedIntView.ContainsInRange(OutAdj, OutStart[node], OutStart[node + 1], outNode);
    }

    /// <summary>
    /// Returns the edge index of the edge <c>source -> target</c>, or -1 if there is none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="GraphBuilder"/> sorts the edges and merges duplicates, so an edge does not keep the
    /// position at which it was added; this method finds where it went.
    /// </para>
    /// <para><b>Time:</b> O(log OutDegree(source)).</para>
    /// </remarks>
    /// <example>
    /// Here, <c>calls</c> was built with one edge per call site, and the call sites of each call are
    /// counted:
    /// <!-- example: EdgeData.cs#edgeIndexOf -->
    /// <code language="csharp">
    /// int[] siteCount = new int[calls.EdgeCount];
    /// foreach (CallSite site in callSites)
    /// {
    ///     siteCount[calls.EdgeIndexOf(site.Caller, site.Callee)]++;
    /// }
    /// // 2 for run -> show and for parse -> parse, 1 for every other edge
    /// </code>
    /// </example>
    /// <param name="source">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <param name="target">Node ID; a value out of range yields -1.</param>
    /// <returns>The edge index in <c>0..EdgeCount-1</c>, or -1.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="source"/> is out of range.</exception>
    public int EdgeIndexOf(int source, int target)
    {
        Checks.Index(source, OutStart.Length - 1);
        return SortedIntView.IndexOfInRange(OutAdj, OutStart[source], OutStart[source + 1], target);
    }

    /// <summary>
    /// Returns whether <paramref name="node"/> has an edge to itself, called a self-loop. Equivalent
    /// to <c>HasOutNode(node, node)</c>.
    /// </summary>
    /// <remarks><b>Time:</b> O(log OutDegree(node)).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns><see langword="true"/> if the edge <c>node -> node</c> exists.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public bool ContainsSelfLoop(int node) => HasOutNode(node, node);

    /// <summary>
    /// Returns a graph with the same nodes and edges, without in-adjacency. A plain
    /// <see cref="Graph"/> returns itself, and a <see cref="BiAdjacentGraph"/> returns a
    /// <see cref="Graph"/> over its out-adjacency.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <returns>This graph, or a graph over the out-adjacency of a <see cref="BiAdjacentGraph"/>.</returns>
    public virtual Graph UniAdjacentGraph() => this;

    /// <summary>Returns whether <paramref name="other"/> is a graph with the same nodes and edges.</summary>
    /// <remarks>
    /// <para>
    /// The in-adjacency does not count: a <see cref="BiAdjacentGraph"/> equals a <see cref="Graph"/>
    /// with the same nodes and edges.
    /// </para>
    /// <para><b>Time:</b> O(NodeCount + EdgeCount).</para>
    /// </remarks>
    /// <param name="other">The graph to compare with, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if both have the same nodes and edges.</returns>
    public bool Equals(Graph? other) =>
        other is not null
        && (ReferenceEquals(this, other)
            || (OutStart.AsSpan().SequenceEqual(other.OutStart) && OutAdj.AsSpan().SequenceEqual(other.OutAdj)));

    /// <inheritdoc cref="Equals(Graph?)"/>
    public sealed override bool Equals(object? obj) => obj is Graph other && Equals(other);

    /// <summary>
    /// Returns a hash code over the nodes and edges, consistent with <see cref="Equals(Graph?)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is computed on every call, so each lookup of a graph in a hash-based collection walks the
    /// whole graph. <see cref="Graphs.TopologyHash"/> gives a 64-bit hash to compute once and keep.
    /// </para>
    /// <para><b>Time:</b> O(NodeCount + EdgeCount).</para>
    /// </remarks>
    /// <returns>The hash code.</returns>
    public sealed override int GetHashCode() => unchecked((31 * ListHash(OutStart)) + ListHash(OutAdj));

    // Java's Arrays.hashCode, so that the hash code is the same as in Java.
    private static int ListHash(int[] values)
    {
        int h = 1;
        foreach (int value in values)
        {
            h = unchecked((31 * h) + value);
        }
        return h;
    }

    /// <summary>
    /// Returns the node and edge counts and the out-nodes of the first nodes, for debuggers and
    /// logs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The text stays short however large the graph: it lists at most 16 nodes and 64 out-nodes in
    /// total. <c>...</c> marks where it is cut - after the 16th node, or inside the list that would
    /// go beyond 64 out-nodes. A <see cref="BiAdjacentGraph"/> is named as such and lists its
    /// out-nodes. <see cref="Graphs.AdjacencyListOf"/> gives the whole graph as text.
    /// </para>
    /// <para><b>Time:</b> O(1), as the text is bounded.</para>
    /// </remarks>
    /// <example>
    /// For the control flow of a method with a loop:
    /// <!-- example: Building.cs#toString -->
    /// <code language="csharp">
    /// string text = cfg.ToString();
    /// // Graph[nodes=6, edges=7] {0 -> [1], 1 -> [2, 5], 2 -> [3, 4], 3 -> [4],
    /// //                          4 -> [1], 5 -> []}
    /// </code>
    /// </example>
    /// <returns>The text.</returns>
    public sealed override string ToString()
    {
        int n = NodeCount;
        var sb = new StringBuilder(ToStringCapacity);
        sb.Append(GetType().Name).Append("[nodes=").Append(n).Append(", edges=").Append(EdgeCount).Append("] {");
        int budget = ToStringTargets;
        int listed = Math.Min(n, ToStringNodes);
        for (int v = 0; v < listed; v++)
        {
            if (v > 0)
            {
                sb.Append(", ");
            }
            sb.Append(v).Append(" -> [");
            int start = OutStart[v];
            int end = OutStart[v + 1];
            int shown = Math.Min(end - start, budget);
            for (int e = start; e < start + shown; e++)
            {
                if (e > start)
                {
                    sb.Append(", ");
                }
                sb.Append(OutAdj[e]);
            }
            budget -= shown;
            if (shown < end - start)
            {
                sb.Append(shown > 0 ? ", ...]" : "...]");
                if (v + 1 < n)
                {
                    sb.Append(", ...");
                }
                return sb.Append('}').ToString();
            }
            sb.Append(']');
        }
        if (listed < n)
        {
            sb.Append(", ...");
        }
        return sb.Append('}').ToString();
    }
}
