using Graphity;
using Graphity.Mermaid;

namespace Examples;

/// <summary>Writing the dependencies between packages as a Mermaid flowchart.</summary>
internal static class WritingMermaid
{
    /// <summary>Returns the flowchart of the packages that form a cycle.</summary>
    internal static string Cycle()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        Sccs sccs = Components.SccsOf(dependencies);
        // @start region="cycle"
        // The packages are the nodes of dependencies, names[node] is the name of a
        // package, and sccs holds the strongly connected components of dependencies.

        // Write the packages that lie on a cycle as a Mermaid flowchart, each labeled
        // with its name.
        string mermaid = MermaidWriter.Of(dependencies)
            .NodeLabel(node => names[node])
            .NodeFilter(node => sccs.ContainsCycle(sccs.SccOf(node)))
            .WriteToString();
        // flowchart TD
        //   n4["model"]
        //   n5["storage"]
        //   n4 --> n5
        //   n5 --> n4
        // @end
        return mermaid;
    }

    /// <summary>
    /// Returns the flowchart of all packages, with the packages of the cycle in a class.
    /// </summary>
    internal static string CycleInRed()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        Sccs sccs = Components.SccsOf(dependencies);
        // @start region="nodeClass"
        string mermaid = MermaidWriter.Of(dependencies)
            .NodeLabel(node => names[node])
            .ClassDef("cycle", "stroke:red")
            .NodeClass(node => sccs.ContainsCycle(sccs.SccOf(node)) ? "cycle" : null)
            .WriteToString();
        // model and storage, the cycle, in red:
        //   classDef cycle stroke:red
        //   class n4 cycle
        //   class n5 cycle
        // @end
        return mermaid;
    }
}
