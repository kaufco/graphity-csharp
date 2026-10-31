namespace Graphity;

/// <summary>
/// The edges added and removed between two graphs over the same nodes, as <see cref="Graphs.Diff"/>
/// returns them.
/// </summary>
/// <remarks>
/// <para>
/// Each edge comes with its edge index in the graph it belongs to: an added edge with its edge index
/// in the newer graph, a removed edge with its edge index in the older one. With these edge indices,
/// the caller can look up data on the edges of either graph in arrays indexed by edge index. Both
/// lists are in edge index order. To see whether a graph changed without keeping the older one,
/// compare hashes from <see cref="Graphs.TopologyHash"/> instead.
/// </para>
/// <para>Instances are immutable and safe to share between threads.</para>
/// <para><b>Memory:</b> 12 bytes per added or removed edge.</para>
/// </remarks>
public sealed class GraphDiff
{
    internal GraphDiff(Edges addedEdges, Edges removedEdges)
    {
        AddedEdges = addedEdges;
        RemovedEdges = removedEdges;
    }

    /// <summary>
    /// Gets the edges of the newer graph that the older one does not have, with their edge indices in
    /// the newer graph, in edge index order.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public Edges AddedEdges { get; }

    /// <summary>
    /// Gets the edges of the older graph that the newer one does not have, with their edge indices in the
    /// older graph, in edge index order.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public Edges RemovedEdges { get; }

    /// <summary>
    /// Gets whether no edge was added or removed, that is, whether the two graphs are equal. Equivalent
    /// to <c>AddedEdges.IsEmpty &amp;&amp; RemovedEdges.IsEmpty</c>.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public bool IsEmpty => AddedEdges.IsEmpty && RemovedEdges.IsEmpty;

    /// <summary>Returns the numbers of added and removed edges, for debuggers and logs.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <returns>The text.</returns>
    public override string ToString() => $"GraphDiff[added={AddedEdges.Count}, removed={RemovedEdges.Count}]";
}
