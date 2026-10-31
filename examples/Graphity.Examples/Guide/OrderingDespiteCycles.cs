using Graphity;

namespace Examples.Guide;

/// <summary>
/// The packages in an order in which each comes after the packages it uses, despite a
/// cycle.
/// </summary>
internal static class OrderingDespiteCycles
{
    /// <summary>The build order, and a comparison of two packages in it.</summary>
    internal sealed record Order(List<List<string>> BuildOrder, bool JsonBeforeUi);

    /// <summary>
    /// Returns the packages grouped by SCC, in the order of the SCC IDs.
    /// </summary>
    internal static Order BuildOrder()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        int json = SmallProgram.NodeOf(names, "json");
        int ui = SmallProgram.NodeOf(names, "ui");
        // @start region="buildOrder"
        // model and storage use each other.
        Sccs sccs = Components.SccsOf(dependencies);

        // The SCCs in the order of their IDs: each after the SCCs it uses.
        List<List<string>> buildOrder = [];
        for (int scc = 0; scc < sccs.SccCount; scc++)
        {
            buildOrder.Add([.. sccs.NodesOf(scc).Select(p => names[p])]);
        }
        // buildOrder: [[util], [cli], [json], [model, storage], [service], [ui], [app]]

        // Which of two packages comes first? Compare their SCC IDs.
        bool jsonBeforeUi = sccs.SccOf(json) < sccs.SccOf(ui);   // true
        // @end
        return new Order(buildOrder, jsonBeforeUi);
    }
}
