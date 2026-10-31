using Graphity;

namespace Examples.Guide;

/// <summary>Whether every node of a graph reaches every other.</summary>
internal static class CheckingStrongConnectivity
{
    /// <summary>The two answers of the example.</summary>
    internal sealed record Answers(bool CfgStronglyConnected, bool LoadWellFormed);

    /// <summary>
    /// Returns whether the control flow of load is strongly connected, without and with
    /// an edge from its exit back to its entry.
    /// </summary>
    internal static Answers Check()
    {
        Graph cfg = SmallProgram.Cfg();
        string[] blocks = SmallProgram.BlockNames();
        int entry = SmallProgram.NodeOf(blocks, "entry");
        int exit = SmallProgram.NodeOf(blocks, "exit");
        // @start region="stronglyConnected"
        // No edge leads to entry, so no other block reaches it.
        bool cfgStronglyConnected = Graphs.IsStronglyConnected(cfg);   // false

        // With an edge from exit back to entry, the graph is strongly connected exactly
        // when every block can be reached from entry and can reach exit.
        var builder = new GraphBuilder();
        builder.AddGraph(cfg);
        builder.AddEdge(exit, entry);
        bool loadWellFormed = Graphs.IsStronglyConnected(builder.Build());   // true
        // @end
        return new Answers(cfgStronglyConnected, loadWellFormed);
    }
}
