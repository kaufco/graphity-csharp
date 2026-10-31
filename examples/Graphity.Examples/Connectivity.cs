using Graphity;

namespace Examples;

/// <summary>The edges and nodes whose removal splits a component.</summary>
internal sealed record Splitters(Edges Edges, SortedIntView Nodes);

/// <summary>
/// The components of a graph, and the edges and nodes that keep them connected.
/// </summary>
internal static class Connectivity
{
    /// <summary>
    /// Returns the packages grouped by strongly connected component, in the order of
    /// their IDs.
    /// </summary>
    internal static List<List<string>> BuildOrder()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        // @start region="sccsOf"
        // The SCCs in reverse topological order, each as the names of its packages.
        Sccs sccs = Components.SccsOf(dependencies);
        List<List<string>> buildOrder = [];
        for (int scc = 0; scc < sccs.SccCount; scc++)
        {
            buildOrder.Add([.. sccs.NodesOf(scc).Select(node => names[node])]);
        }
        // buildOrder: [[util], [cli], [json], [model, storage], [service], [ui], [app]]
        // @end
        return buildOrder;
    }

    /// <summary>
    /// Returns the bridges and articulation points of the control flow of <c>load</c>.
    /// </summary>
    internal static Splitters BridgesOfLoad()
    {
        Graph cfg = SmallProgram.Cfg();
        // @start region="biconnectivity"
        Biconnectivity biconnectivity =
            Components.BiconnectivityOf(BiAdjacentGraph.Of(cfg));
        Edges bridges = biconnectivity.Bridges;
        // bridges: entry -> header, header -> exit
        SortedIntView articulationPoints = biconnectivity.ArticulationPoints;
        // articulationPoints: [1] - header
        // @end
        return new Splitters(bridges, articulationPoints);
    }

    /// <summary>
    /// Returns the strong bridges and strong articulation points of the control flow of
    /// <c>load</c>.
    /// </summary>
    internal static Splitters StrongBridgesOfLoad()
    {
        Graph cfg = SmallProgram.Cfg();
        // @start region="strongConnectivity"
        StrongConnectivity strong =
            Components.StrongConnectivityOf(BiAdjacentGraph.Of(cfg));
        Edges strongBridges = strong.StrongBridges;
        // strongBridges: header -> body, body -> then, then -> next, next -> header
        SortedIntView strongArticulationPoints = strong.StrongArticulationPoints;
        // strongArticulationPoints: [1, 2, 4] - header, body and next
        // @end
        return new Splitters(strongBridges, strongArticulationPoints);
    }
}
