using Graphity;

namespace Examples;

/// <summary>
/// Building a graph with <see cref="GraphBuilder"/>, and reading its edges in bulk.
/// </summary>
internal static class BuildingAndReading
{
    /// <summary>
    /// Builds the calls between the methods from the call sites, one edge per call site.
    /// </summary>
    internal static Graph CallsFromCallSites()
    {
        string[] names = SmallProgram.MethodNames();
        IReadOnlyList<CallSite> callSites = SmallProgram.CallSites();
        // @start region="build"
        var builder = new GraphBuilder();
        builder.AddNodes(names.Length);    // one node per method, numbered as in names
        foreach (CallSite site in callSites)
        {
            builder.AddEdge(site.Caller, site.Callee);
        }
        Graph calls = builder.Build();
        // 14 call sites, 12 edges: run -> show and parse -> parse were added twice
        // @end
        return calls;
    }

    /// <summary>
    /// Returns the dependencies of a next version of the program, in which ui also uses
    /// storage.
    /// </summary>
    internal static Graph NextVersion()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        int ui = SmallProgram.NodeOf(names, "ui");
        int storage = SmallProgram.NodeOf(names, "storage");
        // @start region="addGraph"
        var builder = new GraphBuilder();
        // into an empty builder: the node IDs stay the same
        builder.AddGraph(dependencies);
        builder.AddEdge(ui, storage);
        Graph nextVersion = builder.Build();
        // @end
        return nextVersion;
    }

    /// <summary>
    /// Returns the number of callers of each method, by node ID of <c>calls</c>.
    /// </summary>
    internal static int[] CallerCounts()
    {
        Graph calls = SmallProgram.Calls();
        // @start region="loop"
        ReadOnlySpan<int> start = Csr.OutAdjacencyStart(calls);
        ReadOnlySpan<int> callees = Csr.OutAdjacency(calls);
        int[] callerCount = new int[calls.NodeCount];
        for (int method = 0; method < calls.NodeCount; method++)
        {
            for (int edge = start[method], end = start[method + 1]; edge < end; edge++)
            {
                callerCount[callees[edge]]++;
            }
        }
        // callerCount: 3 for format, 2 for parse, 0 for main, 1 for every other method
        // @end
        return callerCount;
    }

    /// <summary>
    /// Writes the recursive calls, with the number of call sites of each.
    /// </summary>
    internal static void RecursiveCalls(TextWriter output)
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int[] siteCount = EdgeData.CallSitesPerEdge();
        // @start region="edges"
        Edges recursive = BackEdges.Of(calls);
        for (int p = 0; p < recursive.Count; p++)
        {
            int caller = recursive.SourceNodes[p];
            int callee = recursive.TargetNodes[p];
            int edge = recursive.EdgeIndices[p];
            output.WriteLine($"{names[caller]} -> {names[callee]}: {siteCount[edge]}");
        }
        // parse -> parse: 2
        // format -> format: 1
        // @end
    }
}
