using System.Collections.Immutable;
using Graphity;

namespace Examples;

/// <summary>Operations on whole graphs: a cycle, and the graph as text.</summary>
internal static class GraphOperations
{
    /// <summary>Returns the cycle in the dependencies between the packages.</summary>
    internal static ImmutableArray<int> CycleInDependencies()
    {
        Graph dependencies = SmallProgram.Dependencies();
        // @start region="findCycle"
        ImmutableArray<int> cycle = Graphs.FindCycle(dependencies);
        // cycle: [4, 5] - model and storage, closed by the edge storage -> model
        // @end
        return cycle;
    }

    /// <summary>Returns the whole control flow of <c>load</c> as text.</summary>
    internal static string CfgAsAdjacencyList()
    {
        Graph cfg = SmallProgram.Cfg();
        // @start region="adjacencyListOf"
        string text = Graphs.AdjacencyListOf(cfg);
        // 0 -> [1]
        // 1 -> [2, 5]
        // 2 -> [3, 4]
        // 3 -> [4]
        // 4 -> [1]
        // 5 -> []
        // @end
        return text;
    }
}
