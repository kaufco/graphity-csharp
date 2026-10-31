using Graphity;

namespace Examples.Guide;

/// <summary>
/// Whether graphs are trees, forests or paths, with their edges pointing away from the
/// root.
/// </summary>
internal static class CheckingTrees
{
    /// <summary>The answers of the example, and the line of descent it builds.</summary>
    internal sealed record ShapesResult(bool FamilyTree, bool FamilyPath, Graph Line,
        bool LineTree, bool LinePath, bool DependenciesForest);

    /// <summary>
    /// Returns the shapes of the family, of one line of descent in it, and of the
    /// packages.
    /// </summary>
    internal static ShapesResult Shapes()
    {
        Graph family = SmallFamily.Tree();
        Graph dependencies = SmallProgram.Dependencies();
        // @start region="isTree"
        // true - one root node, one parent per person
        bool familyTree = Graphs.IsTree(family);
        bool familyPath = Graphs.IsPath(family);   // false - Ada has two children

        // One line of descent, Ada, Cleo, Finn and Ida, as a graph of its own: the nodes
        // 0 to 3.
        var builder = new GraphBuilder();
        builder.AddNodes(4);
        builder.AddPath(0, 1, 2, 3);
        Graph line = builder.Build();
        bool lineTree = Graphs.IsTree(line);       // true
        bool linePath = Graphs.IsPath(line);       // true - one child per person at most

        // service has two in-nodes: app and ui. model and storage lie on a cycle.
        bool dependenciesForest = Graphs.IsForest(dependencies);   // false
        // @end
        return new ShapesResult(familyTree, familyPath, line, lineTree, linePath,
            dependenciesForest);
    }
}
