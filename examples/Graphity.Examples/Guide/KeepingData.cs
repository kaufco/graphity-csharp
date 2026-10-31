using Graphity;

namespace Examples.Guide;

/// <summary>
/// Data on the nodes and edges of a graph, in arrays of the caller's own.
/// </summary>
internal static class KeepingData
{
    /// <summary>The data the example keeps and reads.</summary>
    internal sealed record Data(string[] PackageOf, int[] SiteCount, List<string> Lines);

    /// <summary>
    /// Returns the package of each method and the call sites of each call, and prints the
    /// calls.
    /// </summary>
    internal static Data CallData()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        IReadOnlyList<CallSite> callSites = SmallProgram.CallSites();
        // @start region="data"
        // Data on nodes: one array per kind of data, indexed by node ID.
        string[] packageOf =
        [
            "app", "cli", "app", "service", "service",
            "ui", "model", "model", "storage", "storage",
        ];

        // Data on edges: one array per kind of data, indexed by edge index.
        int[] siteCount = new int[calls.EdgeCount];
        foreach (CallSite site in callSites)
        {
            siteCount[calls.EdgeIndexOf(site.Caller, site.Callee)]++;
        }
        // @end
        // @start region="forEachEdge"
        // The calls between packages, each with its number of call sites. The edge index
        // finds the data of an edge in its arrays.
        List<string> lines = [];
        calls.ForEachEdge((caller, callee, edge) =>
        {
            if (packageOf[caller] != packageOf[callee])
            {
                lines.Add($"{names[caller]} -> {names[callee]}: {siteCount[edge]}");
            }
        });
        // lines: [main -> parseArgs: 1, run -> load: 1, run -> save: 1, run -> show: 2,
        //         load -> parse: 1, load -> read: 1, save -> format: 1, save -> write: 1,
        //         show -> format: 1]
        // @end
        return new Data(packageOf, siteCount, lines);
    }
}
