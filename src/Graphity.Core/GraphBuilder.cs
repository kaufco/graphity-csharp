using System.Globalization;

namespace Graphity;

/// <summary>
/// A builder for a <see cref="Graph"/>, from nodes and edges added in any order.
/// </summary>
/// <remarks>
/// <para>
/// The nodes added so far have the node IDs <c>0..NodeCount-1</c>, in the order they were added;
/// <see cref="AddNode"/> and <see cref="AddNodes"/> return the IDs of new ones. Edges name their
/// source and target by node ID, in any order and as often as the input has them.
/// <see cref="Build"/> sorts the edges into edge index order and merges duplicates. The builder stays
/// usable afterwards: more nodes and edges can be added, and <see cref="Build"/> called again.
/// </para>
/// <para>
/// Besides single edges, the builder takes edges in batches: the out-edges or in-edges of one node
/// (<see cref="AddOutEdges"/>, <see cref="AddInEdges"/>), a path (<see cref="AddPath"/>), pairs of
/// node IDs (<see cref="AddEdgePairs"/>), the edges of an algorithm result (<see cref="AddEdges"/>)
/// or a whole graph (<see cref="AddGraph"/>). <see cref="AddChild"/>, <see cref="AddParent"/> and
/// their plural forms add new nodes together with their edges. The out-edges, in-edges and paths
/// take their node IDs as a <c>params ReadOnlySpan&lt;int&gt;</c>: loose arguments, an array, a span
/// or the <see cref="SortedIntView"/> that <see cref="Graph.OutNodes"/> returns. So a result of this
/// library goes in without being copied into an array first. A span is read during the call and not
/// kept.
/// </para>
/// <para>
/// A method that takes the node ID of an existing node throws
/// <see cref="ArgumentOutOfRangeException"/> if it is not in <c>0..NodeCount-1</c>, so that an edge
/// to a missing node fails at the call. A batch method that throws adds nothing. The methods that add
/// edges between given nodes each have a twin with the suffix <c>AndNodes</c>, such as
/// <see cref="AddEdgeAndNodes"/>, which adds missing nodes instead. A twin raises the node count to
/// one more than the highest node ID it is given, and the nodes in between are added too, without
/// edges. So a graph can be built from input numbered elsewhere without counting its nodes first.
/// However, a node ID that is too high adds nodes instead of failing.
/// </para>
/// <para>
/// Nodes and edges cannot be removed. To leave nodes out, build the graph and take a subgraph with
/// <see cref="Graphs.Remapped"/> and <see cref="NodeMapping.ForSubgraph(System.Collections.BitArray)"/>.
/// </para>
/// <para>Instances are mutable and not safe to share between threads.</para>
/// <para><b>Memory:</b> 8 bytes per added edge, duplicates included, plus room to grow.</para>
/// <para>
/// A builder holds at most <c>Array.MaxLength - 1</c> nodes (2,147,483,590), as many as a graph can
/// have. This number is the <i>node limit</i>. A method that would add more throws before it changes
/// anything. A builder builds a graph from at most <c>Array.MaxLength</c> added edges
/// (2,147,483,591), duplicates included. This number is the <i>edge limit</i>. A batch method that
/// would add more throws <see cref="ArgumentException"/> and adds nothing. The single-edge methods
/// <see cref="AddEdge"/>, <see cref="AddEdgeAndNodes"/>, <see cref="AddChild"/> and
/// <see cref="AddParent"/> do not check the edge limit: beyond it, <see cref="Build"/> throws
/// <see cref="ArgumentException"/>, and the single-edge methods throw
/// <see cref="OutOfMemoryException"/> once the edge storage cannot grow any further. In .NET, that
/// happens at the edge limit itself.
/// </para>
/// </remarks>
/// <example>
/// Here, the calls between the methods of a program are built from its call sites, one edge per call
/// site; <c>names</c> holds the names of the methods:
/// <!-- example: BuildingAndReading.cs#build -->
/// <code language="csharp">
/// var builder = new GraphBuilder();
/// builder.AddNodes(names.Length);    // one node per method, numbered as in names
/// foreach (CallSite site in callSites)
/// {
///     builder.AddEdge(site.Caller, site.Callee);
/// }
/// Graph calls = builder.Build();
/// // 14 call sites, 12 edges: run -> show and parse -> parse were added twice
/// </code>
/// </example>
public sealed class GraphBuilder
{
    // The added edges as two parallel lists, duplicates included, in the order they were added.
    private readonly IntArrayList _edgeSources = new();
    private readonly IntArrayList _edgeTargets = new();
    private int _nodeCount;

    // The edge limit, ArrayLimits.MaxArrayLength. A field rather than the constant only so that tests
    // can reach it without allocating that many edges, as the Java tests do with lists backed by no
    // array, which a span cannot be.
    private readonly int _edgeLimit;

    /// <summary>Creates an empty builder, without nodes or edges.</summary>
    public GraphBuilder()
        : this(ArrayLimits.MaxArrayLength)
    {
    }

    // An empty builder with a lower edge limit; tests pass a small one.
    internal GraphBuilder(int edgeLimit)
    {
        _edgeLimit = edgeLimit;
    }

    /// <summary>
    /// Makes room for <paramref name="totalEdges"/> edges in total, so that adding up to that many
    /// allocates nothing more.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The argument counts all edges the builder will hold, duplicates included, not the ones still to
    /// come. It can be called at any time and does not change the graph that <see cref="Build"/> returns.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(totalEdges) if the room does not suffice yet, otherwise O(1).<br/>
    /// <b>Memory:</b> 8 x totalEdges bytes if the room does not suffice yet, otherwise none.
    /// </para>
    /// </remarks>
    /// <param name="totalEdges">The number of edges to make room for, duplicates included.</param>
    /// <exception cref="ArgumentException"><paramref name="totalEdges"/> is negative or above the edge limit.</exception>
    public void EnsureEdgeCapacity(int totalEdges)
    {
        if (totalEdges < 0)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"totalEdges must be non-negative: {totalEdges}"), nameof(totalEdges));
        }
        if (totalEdges > _edgeLimit)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"totalEdges {totalEdges} exceeds the edge limit of {_edgeLimit}"),
                nameof(totalEdges));
        }
        _edgeSources.EnsureCapacity(totalEdges);
        _edgeTargets.EnsureCapacity(totalEdges);
    }

    /// <summary>Adds a node and returns its node ID, which is the next in order.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <returns>The node ID of the new node.</returns>
    /// <exception cref="InvalidOperationException">The node count has reached the node limit.</exception>
    public int AddNode()
    {
        CheckRoomForNode();
        return _nodeCount++;
    }

    /// <summary>
    /// Adds <paramref name="count"/> nodes and returns the node ID of the first; the others follow in
    /// order.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="count">The number of nodes to add.</param>
    /// <returns>The node ID of the first new node.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="count"/> is negative or the nodes would exceed the node limit.
    /// </exception>
    public int AddNodes(int count)
    {
        CheckNewNodeCount(count);
        int firstId = _nodeCount;
        _nodeCount += count;
        return firstId;
    }

    /// <summary>
    /// Adds a node with an edge from <paramref name="parent"/> to it, and returns its node ID.
    /// </summary>
    /// <remarks><b>Time:</b> O(1) amortized.</remarks>
    /// <param name="parent">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The node ID of the new node.</returns>
    /// <exception cref="InvalidOperationException">The node count has reached the node limit.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parent"/> is out of range.</exception>
    public int AddChild(int parent)
    {
        CheckRoomForNode();
        CheckNode(parent, "parent", nameof(parent));
        int child = _nodeCount++;
        Push(parent, child);
        return child;
    }

    /// <summary>
    /// Adds <paramref name="count"/> nodes, each with an edge from <paramref name="parent"/> to it, and
    /// returns the node ID of the first; the others follow in order.
    /// </summary>
    /// <remarks><b>Time:</b> O(count) amortized.</remarks>
    /// <param name="parent">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <param name="count">The number of nodes to add.</param>
    /// <returns>The node ID of the first new node.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="count"/> is negative, or the nodes or edges would exceed their limit.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parent"/> is out of range.</exception>
    public int AddChildren(int parent, int count)
    {
        CheckNewNodeCount(count);
        CheckNode(parent, "parent", nameof(parent));
        Reserve(count);
        int firstId = _nodeCount;
        _nodeCount += count;
        for (int i = 0; i < count; i++)
        {
            Push(parent, firstId + i);
        }
        return firstId;
    }

    /// <summary>
    /// Adds a node with an edge from it to <paramref name="child"/>, and returns its node ID.
    /// </summary>
    /// <remarks><b>Time:</b> O(1) amortized.</remarks>
    /// <param name="child">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The node ID of the new node.</returns>
    /// <exception cref="InvalidOperationException">The node count has reached the node limit.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="child"/> is out of range.</exception>
    public int AddParent(int child)
    {
        CheckRoomForNode();
        CheckNode(child, "child", nameof(child));
        int parent = _nodeCount++;
        Push(parent, child);
        return parent;
    }

    /// <summary>
    /// Adds <paramref name="count"/> nodes, each with an edge from it to <paramref name="child"/>, and
    /// returns the node ID of the first; the others follow in order.
    /// </summary>
    /// <remarks><b>Time:</b> O(count) amortized.</remarks>
    /// <param name="child">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <param name="count">The number of nodes to add.</param>
    /// <returns>The node ID of the first new node.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="count"/> is negative, or the nodes or edges would exceed their limit.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="child"/> is out of range.</exception>
    public int AddParents(int child, int count)
    {
        CheckNewNodeCount(count);
        CheckNode(child, "child", nameof(child));
        Reserve(count);
        int firstId = _nodeCount;
        _nodeCount += count;
        for (int i = 0; i < count; i++)
        {
            Push(firstId + i, child);
        }
        return firstId;
    }

    /// <summary>Adds the edge <c>source -> target</c>.</summary>
    /// <remarks>
    /// <para>
    /// An edge added more than once is still one edge in the graph: <see cref="Build"/> merges
    /// duplicates.
    /// </para>
    /// <para><b>Time:</b> O(1) amortized.</para>
    /// </remarks>
    /// <param name="source">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <param name="target">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="source"/> or <paramref name="target"/> is out of range.
    /// </exception>
    public void AddEdge(int source, int target)
    {
        CheckNode(source, "source", nameof(source));
        CheckNode(target, "target", nameof(target));
        Push(source, target);
    }

    /// <summary>
    /// Adds the edge <c>source -> target</c>, and the missing nodes up to the higher of the two node IDs.
    /// </summary>
    /// <remarks><b>Time:</b> O(1) amortized.</remarks>
    /// <param name="source">Node ID, 0 or more.</param>
    /// <param name="target">Node ID, 0 or more.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="source"/> or <paramref name="target"/> is negative or not below the node limit.
    /// </exception>
    public void AddEdgeAndNodes(int source, int target)
    {
        CheckCreatable(source, nameof(source));
        CheckCreatable(target, nameof(target));
        RaiseNodeCount(Math.Max(source, target));
        Push(source, target);
    }

    /// <summary>
    /// Adds an edge from <paramref name="source"/> to each node of <paramref name="targets"/>.
    /// </summary>
    /// <remarks><b>Time:</b> O(targets.Length) amortized.</remarks>
    /// <param name="source">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <param name="targets">Node IDs in <c>0..NodeCount-1</c>.</param>
    /// <exception cref="ArgumentException">The edges would exceed the edge limit.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="source"/> or a node of <paramref name="targets"/> is out of range.
    /// </exception>
    public void AddOutEdges(int source, params ReadOnlySpan<int> targets)
    {
        CheckNode(source, "source", nameof(source));
        CheckNodes(targets, "target", nameof(targets));
        WriteFanOut(source, targets);
    }

    /// <summary>
    /// Adds an edge from <paramref name="source"/> to each node of <paramref name="targets"/>, and the
    /// missing nodes up to the highest of these node IDs.
    /// </summary>
    /// <remarks><b>Time:</b> O(targets.Length) amortized.</remarks>
    /// <param name="source">Node ID, 0 or more.</param>
    /// <param name="targets">Node IDs, 0 or more.</param>
    /// <exception cref="ArgumentException">The edges would exceed the edge limit.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A node ID is negative or not below the node limit.</exception>
    public void AddOutEdgesAndNodes(int source, params ReadOnlySpan<int> targets)
    {
        CheckCreatable(source, nameof(source));
        int highest = Math.Max(source, HighestCreatable(targets, nameof(targets)));
        WriteFanOut(source, targets);
        RaiseNodeCount(highest);
    }

    /// <summary>
    /// Adds an edge from each node of <paramref name="sources"/> to <paramref name="target"/>.
    /// </summary>
    /// <remarks><b>Time:</b> O(sources.Length) amortized.</remarks>
    /// <param name="target">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <param name="sources">Node IDs in <c>0..NodeCount-1</c>.</param>
    /// <exception cref="ArgumentException">The edges would exceed the edge limit.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="target"/> or a node of <paramref name="sources"/> is out of range.
    /// </exception>
    public void AddInEdges(int target, params ReadOnlySpan<int> sources)
    {
        CheckNode(target, "target", nameof(target));
        CheckNodes(sources, "source", nameof(sources));
        WriteFanIn(target, sources);
    }

    /// <summary>
    /// Adds an edge from each node of <paramref name="sources"/> to <paramref name="target"/>, and the
    /// missing nodes up to the highest of these node IDs.
    /// </summary>
    /// <remarks><b>Time:</b> O(sources.Length) amortized.</remarks>
    /// <param name="target">Node ID, 0 or more.</param>
    /// <param name="sources">Node IDs, 0 or more.</param>
    /// <exception cref="ArgumentException">The edges would exceed the edge limit.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A node ID is negative or not below the node limit.</exception>
    public void AddInEdgesAndNodes(int target, params ReadOnlySpan<int> sources)
    {
        CheckCreatable(target, nameof(target));
        int highest = Math.Max(target, HighestCreatable(sources, nameof(sources)));
        WriteFanIn(target, sources);
        RaiseNodeCount(highest);
    }

    /// <summary>
    /// Adds a path through <paramref name="nodes"/>: an edge from each node of the list to the next.
    /// </summary>
    /// <remarks>
    /// <para>Fewer than two nodes add no edge.</para>
    /// <para><b>Time:</b> O(nodes.Length) amortized.</para>
    /// </remarks>
    /// <param name="nodes">Node IDs in <c>0..NodeCount-1</c>, in the order of the path.</param>
    /// <exception cref="ArgumentException">The edges would exceed the edge limit.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A node of <paramref name="nodes"/> is out of range.</exception>
    public void AddPath(params ReadOnlySpan<int> nodes)
    {
        CheckNodes(nodes, "path", nameof(nodes));
        WritePath(nodes);
    }

    /// <summary>
    /// Adds a path through <paramref name="nodes"/>, and the missing nodes up to the highest of these
    /// node IDs. The path has an edge from each node of the list to the next.
    /// </summary>
    /// <remarks>
    /// <para>Fewer than two nodes add no edge, but still add the nodes.</para>
    /// <para><b>Time:</b> O(nodes.Length) amortized.</para>
    /// </remarks>
    /// <param name="nodes">Node IDs, 0 or more, in the order of the path.</param>
    /// <exception cref="ArgumentException">The edges would exceed the edge limit.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A node ID is negative or not below the node limit.</exception>
    public void AddPathAndNodes(params ReadOnlySpan<int> nodes)
    {
        int highest = HighestCreatable(nodes, nameof(nodes));
        WritePath(nodes);
        RaiseNodeCount(highest);
    }

    /// <summary>
    /// Adds an edge for each pair of node IDs in <paramref name="endpoints"/>:
    /// <c>endpoints[0] -> endpoints[1]</c>, <c>endpoints[2] -> endpoints[3]</c>, and so on.
    /// </summary>
    /// <remarks><b>Time:</b> O(endpoints.Length) amortized.</remarks>
    /// <param name="endpoints">Node IDs in <c>0..NodeCount-1</c>, source and target of each edge in turn.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="endpoints"/> has an odd length, or the edges would exceed the edge limit.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A node of <paramref name="endpoints"/> is out of range.</exception>
    public void AddEdgePairs(params ReadOnlySpan<int> endpoints)
    {
        CheckPairCount(endpoints.Length);
        CheckNodes(endpoints, "endpoint", nameof(endpoints));
        WritePairs(endpoints);
    }

    /// <summary>
    /// Adds an edge for each pair of node IDs in <paramref name="endpoints"/>, as
    /// <see cref="AddEdgePairs"/> does, and the missing nodes up to the highest of these node IDs.
    /// </summary>
    /// <remarks><b>Time:</b> O(endpoints.Length) amortized.</remarks>
    /// <param name="endpoints">Node IDs, 0 or more, source and target of each edge in turn.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="endpoints"/> has an odd length, or the edges would exceed the edge limit.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A node ID is negative or not below the node limit.</exception>
    public void AddEdgePairsAndNodes(params ReadOnlySpan<int> endpoints)
    {
        CheckPairCount(endpoints.Length);
        int highest = HighestCreatable(endpoints, nameof(endpoints));
        WritePairs(endpoints);
        RaiseNodeCount(highest);
    }

    /// <summary>
    /// Adds the edges of an algorithm result, such as <see cref="BackEdges.Of"/> or
    /// <see cref="Biconnectivity.Bridges"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the sources and targets are read: the edge indices of <paramref name="edges"/> belong to the
    /// graph the algorithm ran on.
    /// </para>
    /// <para><b>Time:</b> O(edges.Count) amortized.</para>
    /// </remarks>
    /// <param name="edges">The edges to add, between nodes in <c>0..NodeCount-1</c>.</param>
    /// <exception cref="ArgumentException">The edges would exceed the edge limit.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A source or target is out of range.</exception>
    public void AddEdges(Edges edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        ReadOnlySpan<int> sources = edges.SourceNodes.AsSpan();
        ReadOnlySpan<int> targets = edges.TargetNodes.AsSpan();
        CheckNodes(sources, "source", nameof(edges));
        CheckNodes(targets, "target", nameof(edges));
        WriteLists(sources, targets);
    }

    /// <summary>
    /// Adds the edges of an algorithm result, as <see cref="AddEdges"/> does, and the missing nodes up to
    /// the highest of their node IDs.
    /// </summary>
    /// <remarks><b>Time:</b> O(edges.Count) amortized.</remarks>
    /// <param name="edges">The edges to add.</param>
    /// <exception cref="ArgumentException">The edges would exceed the edge limit.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A source or target is not below the node limit.</exception>
    public void AddEdgesAndNodes(Edges edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        ReadOnlySpan<int> sources = edges.SourceNodes.AsSpan();
        ReadOnlySpan<int> targets = edges.TargetNodes.AsSpan();
        int highest = Math.Max(HighestCreatable(sources, nameof(edges)), HighestCreatable(targets, nameof(edges)));
        WriteLists(sources, targets);
        RaiseNodeCount(highest);
    }

    /// <summary>
    /// Adds the nodes and edges of <paramref name="graph"/> after the nodes already added, and returns
    /// the offset of its node IDs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Node <c>v</c> of <paramref name="graph"/> becomes node <c>offset + v</c> of this builder, where
    /// the offset is the node count before the call. Into an empty builder, the node IDs stay the same,
    /// which makes this the way to extend a graph: a <see cref="Graph"/> is immutable, but a builder that
    /// starts with it can add to it and build a new one.
    /// </para>
    /// <para><b>Time:</b> O(graph.NodeCount + graph.EdgeCount) amortized.</para>
    /// </remarks>
    /// <example>
    /// Here, the dependencies between the packages of a program are extended to a next version, in
    /// which <c>ui</c> also uses <c>storage</c>:
    /// <!-- example: BuildingAndReading.cs#addGraph -->
    /// <code language="csharp">
    /// var builder = new GraphBuilder();
    /// // into an empty builder: the node IDs stay the same
    /// builder.AddGraph(dependencies);
    /// builder.AddEdge(ui, storage);
    /// Graph nextVersion = builder.Build();
    /// </code>
    /// </example>
    /// <param name="graph">The graph to add.</param>
    /// <returns>The offset: node ID of node 0 of <paramref name="graph"/> in this builder.</returns>
    /// <exception cref="ArgumentException">
    /// The nodes or edges of <paramref name="graph"/> would exceed their limit.
    /// </exception>
    public int AddGraph(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int srcNodeCount = graph.NodeCount;
        if (srcNodeCount > ArrayLimits.MaxNodeCount - _nodeCount)
        {
            throw BeyondNodeLimit(string.Create(CultureInfo.InvariantCulture, $"a graph of {srcNodeCount} nodes"));
        }
        Reserve(graph.EdgeCount);
        int offset = _nodeCount;
        _nodeCount += srcNodeCount;
        int[] adjacency = graph.OutAdj;
        int[] adjacencyStart = graph.OutStart;
        for (int node = 0; node < srcNodeCount; node++)
        {
            int end = adjacencyStart[node + 1];
            for (int i = adjacencyStart[node]; i < end; i++)
            {
                Push(node + offset, adjacency[i] + offset);
            }
        }
        return offset;
    }

    /// <summary>Returns a graph with the nodes and edges added so far.</summary>
    /// <remarks>
    /// <para>
    /// The edges are sorted into edge index order (by source and then by target), and duplicates are
    /// merged, so an edge does not keep the position at which it was added;
    /// <see cref="Graph.EdgeIndexOf"/> finds where it went. The builder does not change: more nodes and
    /// edges can be added and built into another graph.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + k log d), where k is the number of added edges, duplicates included,
    /// and d the most of them with the same source.<br/>
    /// <b>Memory:</b> 4 x (NodeCount + EdgeCount) bytes for the result; while it runs, briefly up to
    /// 4 x (NodeCount + k) bytes more.
    /// </para>
    /// </remarks>
    /// <returns>The graph.</returns>
    /// <exception cref="ArgumentException">The edges added exceed the edge limit.</exception>
    public Graph Build()
    {
        int nodeCount = _nodeCount;
        int rawEdgeCount = _edgeSources.Count;
        CheckEdgeLimit(rawEdgeCount, _edgeLimit);
        int[] sources = _edgeSources.Elements;
        int[] targets = _edgeTargets.Elements;

        // 1) Count out-degrees.
        var outStart = new int[nodeCount + 1];
        for (int i = 0; i < rawEdgeCount; i++)
        {
            outStart[sources[i] + 1]++;
        }

        // 2) Prefix sum: degrees to start offsets.
        for (int n = 0; n < nodeCount; n++)
        {
            outStart[n + 1] += outStart[n];
        }

        // 3) Distribute targets into per-node buckets.
        var outAdj = new int[outStart[nodeCount]];
        int[] writePos = outStart[..nodeCount];
        for (int i = 0; i < rawEdgeCount; i++)
        {
            outAdj[writePos[sources[i]]++] = targets[i];
        }

        // 4) Sort and deduplicate each bucket, compacting left: writes never pass the read position.
        int compactEnd = 0;
        for (int node = 0; node < nodeCount; node++)
        {
            int start = outStart[node];
            int end = outStart[node + 1];
            outAdj.AsSpan(start, end - start).Sort();
            outStart[node] = compactEnd;
            if (start < end)
            {
                outAdj[compactEnd++] = outAdj[start];
                for (int r = start + 1; r < end; r++)
                {
                    if (outAdj[r] != outAdj[r - 1])
                    {
                        outAdj[compactEnd++] = outAdj[r];
                    }
                }
            }
        }
        outStart[nodeCount] = compactEnd;
        if (compactEnd < outAdj.Length)
        {
            outAdj = outAdj[..compactEnd];
        }
        return new Graph(outStart, outAdj);
    }

    /// <summary>Current capacity of the edge storage, so that tests can observe when it reallocates.</summary>
    internal int EdgeCapacity => Math.Min(_edgeSources.Elements.Length, _edgeTargets.Elements.Length);

    private void Push(int source, int target)
    {
        _edgeSources.Add(source);
        _edgeTargets.Add(target);
    }

    private void WriteFanOut(int source, ReadOnlySpan<int> targets)
    {
        Reserve(targets.Length);
        foreach (int target in targets)
        {
            Push(source, target);
        }
    }

    private void WriteFanIn(int target, ReadOnlySpan<int> sources)
    {
        Reserve(sources.Length);
        foreach (int source in sources)
        {
            Push(source, target);
        }
    }

    private void WritePath(ReadOnlySpan<int> nodes)
    {
        Reserve(Math.Max(nodes.Length - 1, 0));
        for (int i = 1; i < nodes.Length; i++)
        {
            Push(nodes[i - 1], nodes[i]);
        }
    }

    private void WritePairs(ReadOnlySpan<int> endpoints)
    {
        Reserve(endpoints.Length >> 1);
        for (int i = 0; i < endpoints.Length; i += 2)
        {
            Push(endpoints[i], endpoints[i + 1]);
        }
    }

    private void WriteLists(ReadOnlySpan<int> sources, ReadOnlySpan<int> targets)
    {
        Reserve(sources.Length);
        for (int i = 0; i < sources.Length; i++)
        {
            Push(sources[i], targets[i]);
        }
    }

    // The role word goes into Java's message; paramName is the parameter of the public method.
    private void CheckNode(int node, string role, string paramName)
    {
        if ((uint)node >= (uint)_nodeCount)
        {
            throw new ArgumentOutOfRangeException(paramName, node,
                string.Create(CultureInfo.InvariantCulture,
                    $"{role} node does not exist: {node} (nodeCount={_nodeCount})"));
        }
    }

    /// <summary>Validates every consumed ID before any is written, so that a rejected batch leaves the builder unchanged.</summary>
    private void CheckNodes(ReadOnlySpan<int> nodes, string role, string paramName)
    {
        foreach (int node in nodes)
        {
            CheckNode(node, role, paramName);
        }
    }

    /// <summary>
    /// Rejects IDs that cannot be created: negative ones, and those whose node count would exceed
    /// <see cref="ArrayLimits.MaxNodeCount"/>.
    /// </summary>
    private static void CheckCreatable(int node, string paramName)
    {
        if (node < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, node, string.Create(CultureInfo.InvariantCulture,
                $"node ID must be non-negative: {node}"));
        }
        if (node >= ArrayLimits.MaxNodeCount)
        {
            throw new ArgumentOutOfRangeException(paramName, node,
                string.Create(CultureInfo.InvariantCulture,
                    $"node ID must be below the node limit of {ArrayLimits.MaxNodeCount}: {node}"));
        }
    }

    /// <summary>Rejects a negative count and one that would exceed <see cref="ArrayLimits.MaxNodeCount"/>.</summary>
    private void CheckNewNodeCount(int count)
    {
        if (count < 0)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"count must be non-negative: {count}"), nameof(count));
        }
        // Cannot overflow: the node count never exceeds the limit.
        if (count > ArrayLimits.MaxNodeCount - _nodeCount)
        {
            throw BeyondNodeLimit(string.Create(CultureInfo.InvariantCulture, $"count {count}"));
        }
    }

    private void CheckRoomForNode()
    {
        if (_nodeCount == ArrayLimits.MaxNodeCount)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"the node limit of {ArrayLimits.MaxNodeCount} is reached"));
        }
    }

    private ArgumentException BeyondNodeLimit(string what) =>
        new(string.Create(CultureInfo.InvariantCulture,
            $"{what} would exceed the node limit of {ArrayLimits.MaxNodeCount} (nodeCount={_nodeCount})"));

    /// <summary>Validates every ID before any node is created, and returns the highest, or -1 if empty.</summary>
    private static int HighestCreatable(ReadOnlySpan<int> nodes, string paramName)
    {
        int highest = -1;
        foreach (int node in nodes)
        {
            CheckCreatable(node, paramName);
            if (node > highest)
            {
                highest = node;
            }
        }
        return highest;
    }

    /// <summary>
    /// Raises the node count so that <paramref name="node"/> exists. Every ID below it is created too,
    /// because node IDs are contiguous. A negative argument leaves the count untouched.
    /// </summary>
    /// <remarks>
    /// The batch twins call it after writing their edges, because the write can still reject the
    /// batch at the edge limit and must then leave the node count as it was. The writers do not read
    /// the node count.
    /// </remarks>
    private void RaiseNodeCount(int node)
    {
        if (node >= _nodeCount)
        {
            _nodeCount = node + 1;
        }
    }

    // The single-edge methods leave the edge limit to Build. In .NET their storage cannot grow
    // past Array.MaxLength, the edge limit of the public constructor, so the check fails only under
    // the lower limit of the tests; it is kept for the contract Java documents. Static, so that the
    // tests reach the real limit without adding the edges.
    internal static void CheckEdgeLimit(int addedEdges, int edgeLimit)
    {
        if (addedEdges > edgeLimit)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"{addedEdges} edges added exceed the edge limit of {edgeLimit}"));
        }
    }

    private static void CheckPairCount(int length)
    {
        if ((length & 1) != 0)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"expected an even number of node IDs, got {length}"), "endpoints");
        }
    }

    /// <summary>
    /// Reserves room for <paramref name="count"/> more edges in both edge lists, or rejects them
    /// before any is written if they would exceed the edge limit. Grows by at least half the
    /// capacity, like <see cref="IntArrayList.Add"/>, so that batch calls in a loop cost amortized
    /// O(1) per edge; <see cref="IntArrayList.EnsureCapacity"/> alone grows to the exact size, which
    /// would make such a loop quadratic. The half step stops at the edge limit, and a batch larger
    /// than the step gets exactly the room it needs, one allocation per list.
    /// </summary>
    private void Reserve(int count)
    {
        int size = _edgeSources.Count;
        long required = (long)size + count;
        if (required > _edgeLimit)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"{count} more edges would exceed the edge limit of "
                + $"{_edgeLimit} ({size} edges added)"));
        }
        int capacity = _edgeSources.Elements.Length;
        if (required <= capacity)
        {
            return;
        }
        long grown = Math.Max(required, capacity + (long)(capacity >> 1));
        int newCapacity = (int)Math.Min(grown, _edgeLimit);
        _edgeSources.EnsureCapacity(newCapacity);
        _edgeTargets.EnsureCapacity(newCapacity);
    }
}
