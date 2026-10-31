using Graphity;
using Graphity.Dot;

namespace Examples;

/// <summary>
/// Writing the dependencies between packages in DOT (the language of Graphviz).
/// </summary>
internal static class WritingDot
{
    /// <summary>Returns the DOT text of the packages that form a cycle.</summary>
    internal static string Cycle()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        Sccs sccs = Components.SccsOf(dependencies);
        // @start region="cycle"
        // The packages are the nodes of dependencies, names[node] is the name of a
        // package, and sccs holds the strongly connected components of dependencies.

        // Write the packages that lie on a cycle in DOT, each labeled with its name.
        string dot = DotWriter.Of(dependencies)
            .NodeLabel(node => names[node])
            .NodeFilter(node => sccs.ContainsCycle(sccs.SccOf(node)))
            .WriteToString();
        // digraph {
        //   4 [label="model"];
        //   5 [label="storage"];
        //   4 -> 5;
        //   5 -> 4;
        // }
        // @end
        return dot;
    }

    /// <summary>
    /// Returns the DOT text of all packages, with the packages of the cycle in red.
    /// </summary>
    internal static string CycleInRed()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        Sccs sccs = Components.SccsOf(dependencies);
        // @start region="nodeAttributes"
        string dot = DotWriter.Of(dependencies)
            .NodeLabel(node => names[node])
            .NodeAttributes((node, attributes) =>
            {
                if (sccs.ContainsCycle(sccs.SccOf(node)))
                {
                    attributes["color"] = "red";
                }
            })
            .WriteToString();
        // model and storage, the cycle, in red:
        //   4 [label="model", color="red"];
        //   5 [label="storage", color="red"];
        // @end
        return dot;
    }
}
