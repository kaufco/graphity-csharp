using Graphity;

namespace Examples;

/// <summary>
/// The methods that <c>load</c> reaches, with their names and the call sites of each
/// call.
/// </summary>
internal sealed record Loading(Graph Graph, string[] Names, int[] SiteCount);

/// <summary>
/// Renumbering the nodes of a graph, and carrying the data on its nodes and edges along.
/// </summary>
internal static class Mappings
{
    /// <summary>
    /// Returns the subgraph of the calls that <c>load</c> reaches, with its data.
    /// </summary>
    internal static Loading Loading()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int[] siteCount = EdgeData.CallSitesPerEdge();
        int load = SmallProgram.NodeOf(names, "load");
        // @start region="subgraph"
        NodeMapping newToOld = NodeMapping.ForSubgraph(Graphs.ReachableFrom(calls, load));
        Graph loading = Graphs.Remapped(calls, newToOld);
        string[] loadingNames = new string[loading.NodeCount];
        newToOld.Gather(names, loadingNames);
        // loadingNames: [load, parse, read]
        // loading: load -> parse, load -> read, parse -> parse
        // @end
        // @start region="edgeMapping"
        int[] loadingSiteCount =
            Graphs.EdgeMapping(loading, calls, newToOld).Gather(siteCount);
        // 1 for load -> parse and for load -> read, 2 for parse -> parse
        // @end
        return new Loading(loading, loadingNames, loadingSiteCount);
    }

    /// <summary>
    /// Writes each method that calls <c>parse</c>, with the number of call sites.
    /// </summary>
    internal static void CallersOfParse(TextWriter output)
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int[] siteCount = EdgeData.CallSitesPerEdge();
        int parse = SmallProgram.NodeOf(names, "parse");
        // @start region="transposedEdges"
        BiAdjacentGraph calledBy = BiAdjacentGraph.Of(calls).Transposed();
        int[] calledBySiteCount = Graphs.TransposedEdgeMapping(calls).Gather(siteCount);
        for (int edge = calledBy.OutEdgeStart(parse);
             edge < calledBy.OutEdgeEnd(parse);
             edge++)
        {
            output.WriteLine(
                $"{names[calledBy.EdgeTarget(edge)]}: {calledBySiteCount[edge]}");
        }
        // load: 1
        // parse: 2
        // @end
    }
}
