using System.Collections.Immutable;
using Graphity;

namespace Examples;

/// <summary>
/// The first program of the README: the dependencies between packages, and a cycle in
/// them.
/// </summary>
// @start region="program"
public static class GettingStarted
{
    public static void Main()
    {
        // @start region="firstResult"
        // The packages are the nodes 0..7, and uses[p] lists the packages that package p
        // uses.
        string[] names =
            ["app", "cli", "ui", "service", "model", "storage", "json", "util"];
        int[][] uses = [[1, 2, 3], [7], [3, 4, 7], [4, 5], [5], [4, 6, 7], [7], []];

        // Build the graph: an edge p -> q for every package q that p uses.
        var builder = new GraphBuilder();
        builder.AddNodes(uses.Length);
        for (int pkg = 0; pkg < uses.Length; pkg++)
        {
            builder.AddOutEdges(pkg, uses[pkg]);
        }
        Graph dependencies = builder.Build();

        // Find a cycle.
        ImmutableArray<int> cycle = Graphs.FindCycle(dependencies);
        // cycle: [4, 5] - model and storage use each other
        // @end region="firstResult"

        // Print the cycle with the names of its packages.
        Console.WriteLine($"[{string.Join(", ", cycle.Select(pkg => names[pkg]))}]");
        // [model, storage]
    }
}
// @end region="program"
