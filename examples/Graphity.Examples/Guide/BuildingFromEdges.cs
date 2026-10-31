using Graphity;

namespace Examples.Guide;

/// <summary>
/// A graph built from a list of edges: the dependencies between the packages of a
/// program.
/// </summary>
internal static class BuildingFromEdges
{
    /// <summary>The graph with its counts.</summary>
    internal sealed record Built(Graph Dependencies, int NodeCount, int EdgeCount);

    /// <summary>
    /// Returns the dependencies between the packages, built from a list of edges.
    /// </summary>
    internal static Built Dependencies()
    {
        // @start region="fromEdges"
        // The packages of a program, numbered from 0 to 7, and the dependencies between
        // them as pairs of package numbers: an edge p -> q means that package p uses
        // package q.
        string[] names =
            ["app", "cli", "ui", "service", "model", "storage", "json", "util"];
        int[][] uses =
        [
            [0, 1], [0, 2], [0, 3], [1, 7], [2, 3], [2, 4], [2, 7], [3, 4], [3, 5],
            [4, 5], [5, 4], [5, 6], [5, 7], [6, 7], [2, 4],
        ];

        // One node per package, numbered as in names, and one edge per pair.
        var builder = new GraphBuilder();
        builder.AddNodes(names.Length);
        foreach (int[] pair in uses)
        {
            builder.AddEdge(pair[0], pair[1]);
        }
        Graph dependencies = builder.Build();
        int nodeCount = dependencies.NodeCount;   // 8
        int edgeCount = dependencies.EdgeCount;   // 14 - the pair [2, 4] was given twice
        // @end
        return new Built(dependencies, nodeCount, edgeCount);
    }
}
