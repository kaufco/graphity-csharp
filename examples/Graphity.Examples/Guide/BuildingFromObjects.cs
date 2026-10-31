using Graphity;

namespace Examples.Guide;

/// <summary>
/// A graph built from objects of the caller's own: packages that name the packages they
/// use.
/// </summary>
internal static class BuildingFromObjects
{
    /// <summary>A package of a program, with the names of the packages it uses.</summary>
    internal sealed record Package(string Name, IReadOnlyList<string> Uses);

    /// <summary>The graph and the package of each node.</summary>
    internal sealed record Built(Graph Dependencies, Package[] PackageOf,
        string UsedByApp);

    /// <summary>Returns the dependencies between packages given as objects.</summary>
    internal static Built FromObjects()
    {
        // @start region="packages"
        // The packages of a program as objects.
        List<Package> packages =
        [
            new Package("app", ["cli", "ui", "service"]),
            new Package("cli", ["util"]),
            new Package("ui", ["service", "model", "util"]),
            new Package("service", ["model", "storage"]),
            new Package("model", ["storage"]),
            new Package("storage", ["model", "json", "util"]),
            new Package("json", ["util"]),
            new Package("util", []),
        ];
        // @end
        // @start region="fromObjects"
        // Give each package a node ID, its position in the list, and keep the package of
        // each node ID in an array. A dictionary from name to node ID finds the node ID
        // of each name; read with GetValueOrDefault, an unknown name gives -1, which the
        // builder rejects.
        Package[] packageOf = [.. packages];
        var nodeOf = new Dictionary<string, int>();
        for (int node = 0; node < packageOf.Length; node++)
        {
            nodeOf[packageOf[node].Name] = node;
        }

        // Add an edge for each package another package uses.
        var builder = new GraphBuilder();
        builder.AddNodes(packages.Count);
        foreach (Package p in packages)
        {
            foreach (string used in p.Uses)
            {
                builder.AddEdge(nodeOf[p.Name], nodeOf.GetValueOrDefault(used, -1));
            }
        }
        Graph dependencies = builder.Build();
        string usedByApp = packageOf[dependencies.OutNodes(0)[1]].Name;   // ui
        // @end
        return new Built(dependencies, packageOf, usedByApp);
    }

    /// <summary>
    /// Returns a graph built from edges whose node IDs come from elsewhere, without a
    /// node count.
    /// </summary>
    internal static Graph FromNumberedInput()
    {
        // @start region="andNodes"
        // Edges between nodes numbered elsewhere, for example by a database: no node
        // count needed.
        int[] endpoints = [0, 3, 3, 4, 4, 0, 7, 3];   // 0 -> 3, 3 -> 4, 4 -> 0, 7 -> 3
        var builder = new GraphBuilder();
        builder.AddEdgePairsAndNodes(endpoints);
        Graph graph = builder.Build();
        // 8 - the nodes 0 to 7; 1, 2, 5 and 6 have no edges
        int nodeCount = graph.NodeCount;
        // @end
        return graph;
    }
}
