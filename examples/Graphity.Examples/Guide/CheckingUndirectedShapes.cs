using Graphity;

namespace Examples.Guide;

/// <summary>Bipartite graphs, trees and forests, all read as undirected.</summary>
internal static class CheckingUndirectedShapes
{
    /// <summary>Whether two graphs are bipartite.</summary>
    internal sealed record BipartiteResult(bool Family, bool Dependencies);

    /// <summary>Whether graphs are trees or forests when read as undirected.</summary>
    internal sealed record ShapesResult(bool FamilyTree, bool RestForest, bool RestTree,
        bool CallsForest);

    /// <summary>Returns whether the family and the packages are bipartite.</summary>
    internal static BipartiteResult Bipartite()
    {
        Graph family = SmallFamily.Tree();
        Graph dependencies = SmallProgram.Dependencies();
        // @start region="isBipartite"
        // Every edge joins a parent and a child, whose depths differ by one: one even,
        // one odd.
        bool familyBipartite = Graphs.IsBipartite(family);           // true

        // Read as undirected, app, ui and service form a cycle of three edges.
        bool packagesBipartite = Graphs.IsBipartite(dependencies);   // false
        // @end
        return new BipartiteResult(familyBipartite, packagesBipartite);
    }

    /// <summary>
    /// Returns whether the family, the calls and the rest of the calls are trees or
    /// forests.
    /// </summary>
    internal static ShapesResult Shapes()
    {
        Graph family = SmallFamily.Tree();
        Graph calls = SmallProgram.Calls();
        Graph rest = FindingParts.CallsBelowRun();
        // @start region="undirectedTree"
        bool familyTree = Graphs.IsUndirectedTree(family);     // true

        // The self-loops of parse and format do not count as cycles here.
        bool restForest = Graphs.IsUndirectedForest(rest);     // true - five trees
        bool restTree = Graphs.IsUndirectedTree(rest);         // false - not connected

        // Read as undirected, run, save, format and show lie on a cycle.
        bool callsForest = Graphs.IsUndirectedForest(calls);   // false
        // @end
        return new ShapesResult(familyTree, restForest, restTree, callsForest);
    }
}
