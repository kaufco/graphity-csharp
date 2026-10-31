using Graphity;

namespace Examples;

/// <summary>
/// The data on the calls, and the subgraph that <c>load</c> reaches with its data.
/// </summary>
internal sealed record CallData(
    string[] Names,
    string[] Packages,
    int[] SiteCount,
    Graph Loading,
    string[] LoadingNames,
    int[] LoadingSiteCount);

/// <summary>
/// Data on nodes and edges in arrays of your own, one per attribute, and how a subgraph
/// takes it.
/// </summary>
/// <remarks>
/// Java declares a record <c>Method</c> inside the example; C# declares no types inside a
/// method, so the example holds the name and the package of each method in a named tuple
/// instead.
/// </remarks>
internal static class DataOnNodesAndEdges
{
    /// <summary>
    /// Returns a tuple per method and the call sites per call, for the calls and their
    /// subgraph.
    /// </summary>
    internal static CallData CallData()
    {
        Graph calls = SmallProgram.Calls();
        IReadOnlyList<CallSite> callSites = SmallProgram.CallSites();
        int load = SmallProgram.NodeOf(SmallProgram.MethodNames(), "load");
        // @start region="columns"
        (string Name, string Package)[] methods =      // methods[node]
        [
            ("main", "app"), ("parseArgs", "cli"), ("run", "app"), ("load", "service"),
            ("save", "service"), ("show", "ui"), ("parse", "model"), ("format", "model"),
            ("read", "storage"), ("write", "storage"),
        ];

        int[] siteCount = new int[calls.EdgeCount];    // siteCount[edgeIndex]
        foreach (CallSite site in callSites)
        {
            siteCount[calls.EdgeIndexOf(site.Caller, site.Callee)]++;
        }
        // siteCount: 2 for run -> show and for parse -> parse, 1 for every other call
        // @end
        // @start region="carryAlong"
        NodeMapping newToOld = NodeMapping.ForSubgraph(Graphs.ReachableFrom(calls, load));
        Graph loading = Graphs.Remapped(calls, newToOld);
        // loading: load -> parse, load -> read, parse -> parse

        var loadingMethods = new (string Name, string Package)[loading.NodeCount];
        newToOld.Gather(methods, loadingMethods);
        // loadingMethods: load, parse, read

        int[] loadingSiteCount =
            Graphs.EdgeMapping(loading, calls, newToOld).Gather(siteCount);
        // loadingSiteCount: 1 for load -> parse and for load -> read,
        //                   2 for parse -> parse
        // @end
        return new CallData(
            [.. methods.Select(method => method.Name)],
            [.. methods.Select(method => method.Package)],
            siteCount,
            loading,
            [.. loadingMethods.Select(method => method.Name)],
            loadingSiteCount);
    }
}
