using Graphity;

namespace Examples.Guide;

/// <summary>Trees, paths and the edges of one node, each added in a few calls.</summary>
internal static class BuildingTreesAndPaths
{
    /// <summary>Returns the family tree, built person by person.</summary>
    internal static Graph FamilyTree()
    {
        // @start region="tree"
        // A family tree: an edge from each parent to each child. AddChild adds a new node
        // and the edge to it, and returns the new node ID.
        var builder = new GraphBuilder();
        int ada = builder.AddNode();         // 0
        int ben = builder.AddChild(ada);     // 1
        int cleo = builder.AddChild(ada);    // 2
        int dora = builder.AddChild(ben);    // 3
        builder.AddChild(ben);               // 4 - Emil
        int finn = builder.AddChild(cleo);   // 5
        builder.AddChildren(dora, 2);        // returns 6 - Greta, then Hugo as 7
        builder.AddChildren(finn, 2);        // returns 8 - Ida, then Jonas as 9
        Graph family = builder.Build();
        // @end
        return family;
    }

    /// <summary>
    /// Returns the steps that build and publish the program, from paths and batches of
    /// edges.
    /// </summary>
    internal static Graph Steps()
    {
        // @start region="path"
        // The steps that build and publish a program, as the nodes 0 to 8: generate,
        // compile, lint, compileTests, docs, test, package, sign and publish. An edge
        // a -> b means that step b may start only after step a.
        var builder = new GraphBuilder();
        builder.AddNodes(9);
        builder.AddPath(0, 1, 3, 5);     // generate -> compile -> compileTests -> test
        builder.AddOutEdges(1, 4, 6);    // compile -> docs, compile -> package
        // lint -> package, test -> package: the target comes first
        builder.AddInEdges(6, 2, 5);
        builder.AddPath(6, 7, 8);        // package -> sign -> publish
        builder.AddEdge(4, 8);           // docs -> publish
        Graph steps = builder.Build();
        // steps: 0 -> [1], 1 -> [3, 4, 6], 2 -> [6], 3 -> [5], 4 -> [8], 5 -> [6],
        //        6 -> [7], 7 -> [8], 8 -> []
        // @end
        return steps;
    }
}
