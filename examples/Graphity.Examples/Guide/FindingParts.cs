using Graphity;

namespace Examples.Guide;

/// <summary>
/// The weakly connected components of the calls between methods: the groups of methods
/// that calls connect when their direction does not matter.
/// </summary>
internal static class FindingParts
{
    /// <summary>The components, by name, and two checks.</summary>
    internal sealed record WccsResult(Graph Rest, int ComponentCount,
        List<List<string>> Components, bool CallsConnected, bool RestConnected);

    /// <summary>Returns the calls without those made by main and run.</summary>
    internal static Graph CallsBelowRun()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int main = SmallProgram.NodeOf(names, "main");
        int run = SmallProgram.NodeOf(names, "run");
        // @start region="rest"
        // Leave out the calls of main and run, which call into every area of the program.
        var builder = new GraphBuilder();
        builder.AddNodes(calls.NodeCount);
        calls.ForEachEdge((caller, callee, _) =>
        {
            if (caller != main && caller != run)
            {
                builder.AddEdge(caller, callee);
            }
        });
        Graph rest = builder.Build();
        // @end
        return rest;
    }

    /// <summary>
    /// Returns the weakly connected components of the calls without main and run.
    /// </summary>
    internal static WccsResult Wccs()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        Graph rest = CallsBelowRun();
        // @start region="wccsOf"
        NodePartition components = Components.WccsOf(rest);
        int componentCount = components.GroupCount;   // 5
        List<List<string>> byName = [];
        for (int component = 0; component < componentCount; component++)
        {
            byName.Add([.. components.NodesOf(component).Select(m => names[m])]);
        }
        // byName: [[main], [parseArgs], [run], [load, parse, read],
        //          [save, show, format, write]]

        bool callsConnected = Graphs.IsWeaklyConnected(calls);   // true
        bool restConnected = Graphs.IsWeaklyConnected(rest);     // false
        // @end
        return new WccsResult(rest, componentCount, byName, callsConnected,
            restConnected);
    }
}
