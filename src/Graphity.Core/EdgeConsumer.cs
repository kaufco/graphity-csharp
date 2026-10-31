namespace Graphity;

/// <summary>
/// An action on an edge, given its source, target and edge index: the callback of
/// <see cref="Graph.ForEachEdge"/>.
/// </summary>
/// <remarks>
/// <para>
/// The parameters come in the order that <see cref="DfsEdgeTraverser.VisitEdge"/> and the lists of
/// <see cref="Edges"/> use as well.
/// </para>
/// <para>
/// Performs this action on edge <paramref name="edgeIndex"/>, which is <c>source -> target</c>.
/// </para>
/// </remarks>
/// <param name="source">Node ID of the source, in <c>0..NodeCount-1</c>.</param>
/// <param name="target">Node ID of the target, in <c>0..NodeCount-1</c>.</param>
/// <param name="edgeIndex">Edge index in <c>0..EdgeCount-1</c>.</param>
public delegate void EdgeConsumer(int source, int target, int edgeIndex);
