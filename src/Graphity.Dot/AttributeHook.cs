namespace Graphity.Dot;

/// <summary>
/// A hook that puts the attributes of one node or edge, by node ID or edge index, for
/// <see cref="DotWriter.NodeAttributes"/> and <see cref="DotWriter.EdgeAttributes"/>.
/// </summary>
/// <remarks>
/// <para>
/// The attributes, such as <c>color</c> or <c>shape</c>, are those of DOT (the language of Graphviz).
/// The writer passes a dictionary from attribute name to value. It holds the label from
/// <see cref="DotWriter.NodeLabel"/> or <see cref="DotWriter.EdgeLabel"/> as the attribute
/// <c>label</c>, if there is one, and nothing else; the hook can add attributes, and replace or
/// remove the label. The writer writes the attributes in the order they were put and escapes their
/// values; a name that is not an identifier or is a DOT keyword makes <see cref="DotWriter.Write"/>
/// throw an <see cref="ArgumentException"/>. The writer reuses the dictionary for every node and
/// edge, so the hook must not keep it.
/// </para>
/// <para>
/// Puts the attributes of the node or edge <paramref name="id"/> into
/// <paramref name="attributes"/>. Put an attribute through the indexer:
/// <c>attributes["color"] = "red"</c>. Setting an existing name keeps its place in the order, while
/// <c>Add</c> throws for an existing name, as <see cref="IDictionary{TKey, TValue}"/> specifies.
/// </para>
/// </remarks>
/// <param name="id">The node ID or edge index.</param>
/// <param name="attributes">The attributes to write, by name.</param>
public delegate void AttributeHook(int id, IDictionary<string, string> attributes);
