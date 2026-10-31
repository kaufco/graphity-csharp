using System.Globalization;

namespace Graphity;

/// <summary>
/// A partition of the nodes of a graph into groups, such as its components.
/// </summary>
/// <remarks>
/// <para>
/// <c>Get(node)</c> returns the group of <c>node</c>, or -1 if it is in no group, and
/// <see cref="NodesOf"/> the nodes of a group in ascending order. <see cref="Components"/> returns
/// partitions into components, in which every node is in exactly one group: an
/// <see cref="Sccs"/> for the strongly connected components, a plain <see cref="NodePartition"/>
/// for the weakly connected ones. <see cref="Of"/> takes groups of the caller's choice, which may
/// leave nodes out.
/// </para>
/// <para>
/// A partition holds the graph whose nodes it partitions. <see cref="Condense"/> builds the
/// condensed graph, with a node for each group and an edge between two groups wherever an edge of
/// the graph runs between them. <see cref="Graphs.EdgePartition"/> builds the
/// <see cref="EdgePartition"/> that tells which edges of the graph are behind each edge of the
/// condensed graph. The members for groups of any kind, such as
/// <see cref="IndexPartition.GroupCount"/>, <see cref="IndexPartition.SizeOf"/> and <c>Reduce</c>,
/// come from <see cref="IndexPartition"/>.
/// </para>
/// <para>
/// Instances are immutable and safe to share between threads. No other assembly can derive from
/// this class; <see cref="Sccs"/> is its only subclass.
/// </para>
/// <para>
/// <b>Memory:</b> 4 x (NodeCount + MappedCount + GroupCount) bytes, where MappedCount is the number
/// of nodes that belong to a group; the graph is shared, not copied.
/// </para>
/// </remarks>
public class NodePartition : IndexPartition
{
    // The partitioned graph, for Condense and Graphs.EdgePartition: neither takes it as a
    // parameter, so a wrong one cannot be passed.
    internal readonly Graph SourceGraph;

    // Takes the arrays as they are, neither copied nor checked: the caller guarantees the layout of
    // IndexPartition.
    internal NodePartition(Graph graph, int[] nodeToGroup, int[] members, int[] memberStart)
        : base(nodeToGroup, members, memberStart)
    {
        SourceGraph = graph;
    }

    /// <summary>
    /// Returns a partition of the nodes of <paramref name="graph"/> into groups of the caller's
    /// choice.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>groupOf[node]</c> is the group of each node, in <c>0..groupCount-1</c>, or -1 for a node
    /// in no group; the span is copied. A group may have no nodes: it becomes a node without edges
    /// in <see cref="Condense"/>, so that group IDs from a numbering of the caller stay valid.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + groupCount).<br/>
    /// <b>Memory:</b> 4 x (NodeCount + MappedCount + groupCount) bytes for the result.
    /// </para>
    /// </remarks>
    /// <example>
    /// Here, <c>packageOf</c> holds the package of each method of <c>calls</c> as a node ID of the
    /// package graph <c>dependencies</c>, and the condensed graph shows which packages call which:
    /// <!-- example: Partitions.cs#of -->
    /// <code language="csharp">
    /// NodePartition byPackage =
    ///     NodePartition.Of(calls, packageOf, dependencies.NodeCount);
    /// Graph packageCalls = byPackage.Condense();
    /// // packageCalls: app -> [cli, ui, service], ui -> [model],
    /// //               service -> [model, storage]
    /// </code>
    /// </example>
    /// <param name="graph">The graph whose nodes are partitioned.</param>
    /// <param name="groupOf">The group of each node, or -1; length <c>graph.NodeCount</c>.</param>
    /// <param name="groupCount">The number of groups, at most as many as a graph can have nodes.</param>
    /// <returns>The partition.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A group is not in <c>-1..groupCount-1</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <c>groupOf.Length != graph.NodeCount</c>, or <paramref name="groupCount"/> is negative or
    /// larger than a graph can have nodes.
    /// </exception>
    public static NodePartition Of(Graph graph, ReadOnlySpan<int> groupOf, int groupCount)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        Checks.Length(groupOf.Length, n, "groupOf");
        if (groupCount < 0 || groupCount > ArrayLimits.MaxNodeCount)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"groupCount out of range: {groupCount}"), nameof(groupCount));
        }
        int[] nodeToGroup = groupOf.ToArray();
        // Counted here rather than in ParentForest.GroupIndices, so that the range check needs no
        // pass of its own.
        var memberStart = new int[groupCount + 1];
        for (int node = 0; node < n; node++)
        {
            int group = nodeToGroup[node];
            if (group < -1 || group >= groupCount)
            {
                throw new ArgumentOutOfRangeException(nameof(groupOf), group,
                    string.Create(CultureInfo.InvariantCulture,
                        $"group of node {node} is {group}, outside -1..{groupCount - 1}"));
            }
            if (group >= 0)
            {
                memberStart[group]++;
            }
        }
        int[] members = ParentForest.GroupCounted(nodeToGroup, memberStart);
        return new NodePartition(graph, nodeToGroup, members, memberStart);
    }

    /// <summary>
    /// Gets the number of nodes of the partitioned graph, in a group or not. Equivalent to
    /// <see cref="IndexMapping.IndexCount"/>.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public int NodeCount => IndexCount;

    /// <summary>
    /// Returns the nodes in group <paramref name="groupId"/>, in ascending order. Equivalent to
    /// <c>IndicesOf(groupId)</c>.
    /// </summary>
    /// <remarks>
    /// <para>The result is a view of this partition's own array, not a copy.</para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="groupId">Group ID in <c>0..GroupCount-1</c>.</param>
    /// <returns>The nodes in the group.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="groupId"/> is out of range.</exception>
    public SortedIntView NodesOf(int groupId) => IndicesOf(groupId);

    /// <summary>
    /// Returns the condensed graph, with a node for each group and an edge between two groups
    /// wherever an edge of the partitioned graph runs between them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The node IDs of the condensed graph are the group IDs. It has the edge <c>g -> h</c> if a
    /// node in group <c>g</c> has an out-node in group <c>h</c> and <c>g != h</c>. Edges within a
    /// group and edges from or to a node in no group are left out. The edges from one group to
    /// another become a single edge. <see cref="Graphs.EdgePartition"/> tells which edges are behind
    /// each edge of the condensed graph. For an <see cref="Sccs"/>, the condensed graph is acyclic.
    /// </para>
    /// <para>
    /// The condensed graph is built anew on every call, not cached: keep the result to use it more
    /// than once.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount + GroupCount), plus O(d log d) for each group with d
    /// out-nodes in the result.<br/>
    /// <b>Memory:</b> 4 x (GroupCount + condensedEdgeCount) bytes for the result, where
    /// condensedEdgeCount is its edge count; while it runs, 1 bit per group and up to
    /// 6 x condensedEdgeCount bytes more.
    /// </para>
    /// </remarks>
    /// <returns>The condensed graph.</returns>
    public Graph Condense()
    {
        int groups = GroupCount;
        int[] nodeToGroup = Mapping;
        int[] adjacency = SourceGraph.OutAdj;
        int[] adjacencyStart = SourceGraph.OutStart;
        int[] members = Members;
        int[] memberStart = MemberStart;

        var condensedStart = new int[groups + 1];
        var condensedTargets = new IntArrayList();
        // Own bit array: clearing exactly the bits set for one group costs one write per emitted
        // edge instead of one per word.
        var seen = new ulong[(groups >> 6) + 1];

        for (int g = 0; g < groups; g++)
        {
            int groupStart = condensedTargets.Count;
            condensedStart[g] = groupStart;
            for (int ni = memberStart[g], nodesEnd = memberStart[g + 1]; ni < nodesEnd; ni++)
            {
                int u = members[ni];
                for (int ei = adjacencyStart[u], edgeEnd = adjacencyStart[u + 1]; ei < edgeEnd; ei++)
                {
                    int targetGroup = nodeToGroup[adjacency[ei]];
                    if (targetGroup != g && targetGroup >= 0 && (seen[targetGroup >> 6] & (1UL << targetGroup)) == 0)
                    {
                        seen[targetGroup >> 6] |= 1UL << targetGroup;
                        condensedTargets.Add(targetGroup);
                    }
                }
            }
            // The bits set above are exactly the group's emitted targets; zeroing the whole word is
            // safe, every set bit in it belongs to this group.
            int groupEnd = condensedTargets.Count;
            int[] elements = condensedTargets.Elements;
            for (int i = groupStart; i < groupEnd; i++)
            {
                seen[elements[i] >> 6] = 0;
            }
            elements.AsSpan(groupStart, groupEnd - groupStart).Sort();
        }
        condensedStart[groups] = condensedTargets.Count;
        return new Graph(condensedStart, condensedTargets.ToArray());
    }
}
