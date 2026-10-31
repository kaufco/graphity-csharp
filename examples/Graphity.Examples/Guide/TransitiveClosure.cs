using Graphity;

namespace Examples.Guide;

/// <summary>All reachability between the methods at once, as a graph.</summary>
internal static class TransitiveClosure
{
    /// <summary>What the example reads from the closure.</summary>
    internal sealed record Read(Graph Closure, int EdgeCount, bool MainReachesWrite,
        SortedIntView FromRun, bool ParseOnCycle, bool LoadOnCycle);

    /// <summary>
    /// Returns the transitive closure of the calls and what the example reads from it.
    /// </summary>
    internal static Read Closure()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int main = SmallProgram.NodeOf(names, "main");
        int write = SmallProgram.NodeOf(names, "write");
        int run = SmallProgram.NodeOf(names, "run");
        int parse = SmallProgram.NodeOf(names, "parse");
        int load = SmallProgram.NodeOf(names, "load");
        // @start region="closure"
        Graph closure = Graphs.TransitiveClosure(calls);
        // 23 - 12 calls and 11 new edges
        int edgeCount = closure.EdgeCount;
        bool mainReachesWrite = closure.HasOutNode(main, write);   // true
        SortedIntView fromRun = closure.OutNodes(run);
        // fromRun: [3, 4, 5, 6, 7, 8, 9] - load, save, show, parse, format, read and
        // write

        // true - parse calls itself
        bool parseOnCycle = closure.ContainsSelfLoop(parse);
        bool loadOnCycle = closure.ContainsSelfLoop(load);         // false
        // @end
        return new Read(closure, edgeCount, mainReachesWrite, fromRun, parseOnCycle,
            loadOnCycle);
    }
}
