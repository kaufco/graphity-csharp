using Graphity;

namespace Examples;

/// <summary>Data on edges, in arrays indexed by edge index.</summary>
internal static class EdgeData
{
    /// <summary>
    /// Returns the number of call sites of each call, by edge index of <c>calls</c>.
    /// </summary>
    internal static int[] CallSitesPerEdge()
    {
        Graph calls = SmallProgram.Calls();
        IReadOnlyList<CallSite> callSites = SmallProgram.CallSites();
        // @start region="edgeIndexOf"
        int[] siteCount = new int[calls.EdgeCount];
        foreach (CallSite site in callSites)
        {
            siteCount[calls.EdgeIndexOf(site.Caller, site.Callee)]++;
        }
        // 2 for run -> show and for parse -> parse, 1 for every other edge
        // @end
        return siteCount;
    }

    /// <summary>
    /// Writes each method that <c>run</c> calls, with the number of call sites.
    /// </summary>
    internal static void CalleesOfRun(TextWriter output)
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int run = SmallProgram.NodeOf(names, "run");
        int[] siteCount = CallSitesPerEdge();
        // @start region="outEdges"
        for (int edge = calls.OutEdgeStart(run); edge < calls.OutEdgeEnd(run); edge++)
        {
            output.WriteLine($"{names[calls.EdgeTarget(edge)]}: {siteCount[edge]}");
        }
        // load: 1
        // save: 1
        // show: 2
        // @end
    }
}
