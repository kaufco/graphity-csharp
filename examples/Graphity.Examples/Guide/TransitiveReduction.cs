using Graphity;

namespace Examples.Guide;

/// <summary>The build steps without the edges that other paths make redundant.</summary>
internal static class TransitiveReduction
{
    /// <summary>The reduction, its edge count and the edges it leaves out.</summary>
    internal sealed record Reduced(Graph Reduction, int EdgeCount, Edges LeftOut);

    /// <summary>Returns the transitive reduction of the build steps.</summary>
    internal static Reduced Reduction()
    {
        Graph steps = SmallProgram.BuildSteps();
        // @start region="reduction"
        // steps has no cycles.

        // Leave out every edge that a longer path makes redundant.
        Graph reduction = Graphs.TransitiveReduction(steps);
        int edgeCount = reduction.EdgeCount;   // 9 - one fewer than the 10 of steps

        // Which edges are redundant? Those the reduction leaves out.
        Edges leftOut = Graphs.Diff(steps, reduction).RemovedEdges;
        // leftOut: compile -> package, edge 3 of steps -
        // compile -> compileTests -> test -> package
        // @end
        return new Reduced(reduction, edgeCount, leftOut);
    }
}
