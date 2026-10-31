namespace Graphity;

/// <summary>
/// A partition of the edges of a graph into groups: the edges behind each edge of a condensed
/// graph, or the blocks of a graph.
/// </summary>
/// <remarks>
/// <para>
/// <c>Get(e)</c> returns the group of edge <c>e</c>, or -1 if it is in no group, and
/// <see cref="EdgesOf"/> the edge indices of a group in ascending order. The groups come from one
/// of two methods:
/// </para>
/// <list type="bullet">
///   <item><description>
///   <see cref="Graphs.EdgePartition"/> groups the edges of the graph of a
///   <see cref="NodePartition"/> by the edge of its condensed graph that they condense to: the
///   group IDs are the edge indices of the condensed graph. Edges within a group of nodes, and
///   edges from or to a node in no group, condense to no edge and are in no group.
///   </description></item>
///   <item><description>
///   <see cref="Biconnectivity.Blocks"/> groups the edges by block (biconnected component);
///   self-loops are in no block.
///   </description></item>
/// </list>
/// <para>
/// <c>Reduce</c> combines data on the edges into one value per group, such as the number of call
/// sites behind each call between packages.
/// </para>
/// <para>Instances are immutable and safe to share between threads.</para>
/// <para>
/// <b>Memory:</b> 4 x (EdgeCount + MappedCount + GroupCount) bytes, where MappedCount is the number
/// of edges that belong to a group.
/// </para>
/// </remarks>
/// <example>
/// Here, <c>byPackage</c> groups the methods of a program by package. Its condensed graph
/// <c>packageCalls</c> holds the calls between packages (see <see cref="NodePartition.Of"/>).
/// <c>byPackageCall</c> tells which calls between methods are behind the call from <c>service</c>
/// to <c>model</c>:
/// <!-- example: Partitions.cs#edgesOf -->
/// <code language="csharp">
/// EdgePartition byPackageCall = Graphs.EdgePartition(packageCalls, byPackage);
/// int serviceToModel = packageCalls.EdgeIndexOf(service, model);
/// SortedIntView behind = byPackageCall.EdgesOf(serviceToModel);
/// // behind: [5, 7] - the calls load -> parse and save -> format
/// </code>
/// </example>
public sealed class EdgePartition : IndexPartition
{
    // Takes the arrays as they are, neither copied nor checked: the caller guarantees the layout of
    // IndexPartition.
    internal EdgePartition(int[] mapping, int[] members, int[] memberStart)
        : base(mapping, members, memberStart)
    {
    }

    /// <summary>
    /// Gets the number of edges of the partitioned graph, in a group or not. Equivalent to
    /// <see cref="IndexMapping.IndexCount"/>.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public int EdgeCount => IndexCount;

    /// <summary>
    /// Returns the edge indices in group <paramref name="groupId"/>, in ascending order. Equivalent
    /// to <c>IndicesOf(groupId)</c>.
    /// </summary>
    /// <remarks>
    /// <para>The result is a view of this partition's own array, not a copy.</para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="groupId">Group ID in <c>0..GroupCount-1</c>.</param>
    /// <returns>The edge indices in the group.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="groupId"/> is out of range.</exception>
    public SortedIntView EdgesOf(int groupId) => IndicesOf(groupId);
}
