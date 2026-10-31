namespace Graphity;

/// <summary>
/// The type of an edge in a DFS (depth-first search), as <see cref="EdgeTypes"/> classifies the
/// edges of a graph.
/// </summary>
/// <remarks>
/// <para>
/// A depth-first search through all nodes reaches each node once, either by a tree edge or as the
/// root of a new search tree; the tree edges form the DFS forest. Every other edge gets its type
/// from where its target lies relative to its source in that forest, so each edge has exactly one
/// type. <see cref="Edges.Of"/> collects the edges of one type.
/// </para>
/// <para>
/// The underlying type is <see cref="byte"/>, with the values 0 to 3 in the order of the members,
/// so that <see cref="EdgeTypes"/> keeps one byte per edge.
/// </para>
/// </remarks>
public enum EdgeType : byte
{
    /// <summary>An edge by which the search reaches its target for the first time: an edge of the forest.</summary>
    Tree,

    /// <summary>
    /// An edge to an ancestor of its source in the DFS forest, or to the source itself. A graph has
    /// a cycle exactly when it has a back edge.
    /// </summary>
    Back,

    /// <summary>An edge to a descendant of its source in the DFS forest, other than a tree edge.</summary>
    Forward,

    /// <summary>
    /// An edge between two nodes of which neither is an ancestor of the other in the DFS forest:
    /// the search had finished the target before it reached the source.
    /// </summary>
    Cross,
}
