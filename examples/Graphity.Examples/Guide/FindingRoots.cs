using Graphity;

namespace Examples.Guide;

/// <summary>The build steps that need no other step: where a run starts.</summary>
internal static class FindingRoots
{
    /// <summary>The root nodes, and a test of a single node.</summary>
    internal sealed record RootsResult(SortedIntView Roots, bool CompileIsRoot);

    /// <summary>Returns the steps without an edge into them.</summary>
    internal static RootsResult Roots()
    {
        string[] names = SmallProgram.StepNames();
        int compile = SmallProgram.NodeOf(names, "compile");
        // @start region="rootNodes"
        // Root nodes have no in-nodes, so the graph needs its in-nodes.
        BiAdjacentGraph steps = BiAdjacentGraph.Of(SmallProgram.BuildSteps());
        SortedIntView roots = Graphs.RootNodes(steps);      // [0, 2] - generate and lint
        // false - it waits for generate
        bool compileIsRoot = steps.IsRootNode(compile);
        // @end
        return new RootsResult(roots, compileIsRoot);
    }
}
