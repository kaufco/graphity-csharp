using Graphity;

namespace Examples.Guide;

/// <summary>
/// The out-nodes and in-nodes of single nodes: the methods a method calls, and its
/// callers.
/// </summary>
internal static class FindingNeighbors
{
    /// <summary>What the example reads from the out-nodes.</summary>
    internal sealed record Out(SortedIntView CalledByRun, int OutDegree,
        bool RunCallsSave, bool Recursive);

    /// <summary>What the example reads from the in-nodes.</summary>
    internal sealed record In(SortedIntView CallersOfFormat, int InDegree,
        bool SaveCallsFormat, SortedIntView NeighborsOfSave, int DegreeOfSave,
        bool MainIsRoot);

    /// <summary>Returns what <c>run</c> calls.</summary>
    internal static Out OutNodes()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int run = SmallProgram.NodeOf(names, "run");
        int save = SmallProgram.NodeOf(names, "save");
        int parse = SmallProgram.NodeOf(names, "parse");
        // @start region="outNodes"
        // [3, 4, 5] - load, save and show
        SortedIntView calledByRun = calls.OutNodes(run);
        int outDegree = calls.OutDegree(run);                // 3
        bool runCallsSave = calls.HasOutNode(run, save);     // true
        bool recursive = calls.ContainsSelfLoop(parse);      // true - parse calls itself
        // @end
        return new Out(calledByRun, outDegree, runCallsSave, recursive);
    }

    /// <summary>Returns who calls <c>format</c>.</summary>
    internal static In InNodes()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int format = SmallProgram.NodeOf(names, "format");
        int save = SmallProgram.NodeOf(names, "save");
        int main = SmallProgram.NodeOf(names, "main");
        // @start region="inNodes"
        // A Graph stores the out-nodes of every node; BiAdjacentGraph adds the in-nodes.
        // Build it once and keep it.
        BiAdjacentGraph both = BiAdjacentGraph.Of(calls);
        // [4, 5, 7] - save, show and format
        SortedIntView callers = both.InNodes(format);
        int inDegree = both.InDegree(format);                      // 3
        bool saveCallsFormat = both.HasInNode(format, save);       // true
        // [2, 7, 9] - run, format, write
        SortedIntView neighbors = both.Neighbors(save);
        // 3 - 1 in-node, 2 out-nodes
        int degree = both.Degree(save);
        // true - nobody calls main
        bool mainIsRoot = both.IsRootNode(main);
        // @end
        return new In(callers, inDegree, saveCallsFormat, neighbors, degree, mainIsRoot);
    }
}
