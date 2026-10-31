using Graphity;

namespace Examples;

/// <summary>
/// The third example domain: a family tree that records one parent per person, over four
/// generations.
/// </summary>
/// <remarks>
/// <para>
/// Each person has the one parent the tree records, so the graph is a tree: an edge from
/// each parent to each child, rooted at <c>Ada</c>.
/// </para>
/// <code>
/// Ada
/// +-- Ben
/// |   +-- Dora
/// |   |   +-- Greta
/// |   |   +-- Hugo
/// |   +-- Emil
/// +-- Cleo
///     +-- Finn
///         +-- Ida
///         +-- Jonas
/// </code>
/// <para>
/// Node IDs follow the order of the names, which is the order in which <see cref="Tree"/>
/// adds the people, generation by generation. The graph never changes: the comments of
/// the examples, and the documentation that shows them, depend on every node and edge.
/// </para>
/// </remarks>
public static class SmallFamily
{
    private static readonly string[] People =
        ["Ada", "Ben", "Cleo", "Dora", "Emil", "Finn", "Greta", "Hugo", "Ida", "Jonas"];

    // A person, then the person's parent.
    private static readonly string[][] Parent =
    [
        ["Ben", "Ada"], ["Cleo", "Ada"],
        ["Dora", "Ben"], ["Emil", "Ben"], ["Finn", "Cleo"],
        ["Greta", "Dora"], ["Hugo", "Dora"], ["Ida", "Finn"], ["Jonas", "Finn"],
    ];

    /// <summary>
    /// Returns the family tree: an edge from each parent to each child.
    /// </summary>
    /// <returns>10 nodes, 9 edges.</returns>
    public static Graph Tree()
    {
        var builder = new GraphBuilder();
        builder.AddNodes(People.Length);
        foreach (string[] row in Parent)
        {
            builder.AddEdge(PersonOf(row[1]), PersonOf(row[0]));
        }
        return builder.Build();
    }

    /// <summary>
    /// Returns the names of the people, by node ID of <see cref="Tree"/>.
    /// </summary>
    /// <returns>Ada, Ben, Cleo, Dora, Emil, Finn, Greta, Hugo, Ida, Jonas.</returns>
    public static string[] Names() => [.. People];

    /// <summary>
    /// Returns the node ID of the person named <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the person.</param>
    /// <returns>The node ID.</returns>
    /// <exception cref="ArgumentException">If nobody has this name.</exception>
    public static int PersonOf(string name) => SmallProgram.NodeOf(People, name);
}
