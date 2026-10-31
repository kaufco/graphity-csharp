using Graphity;

namespace Examples.Guide;

/// <summary>
/// Subgraphs, renumbered and transposed graphs, groups of one's own, and their data.
/// </summary>
internal static class TransformingGraphs
{
    /// <summary>A subgraph with its mapping and data.</summary>
    internal sealed record SubgraphResult(Graph Loading, NodeMapping NewToOld,
        string[] LoadingNames, Graph SavingAndShowing);

    /// <summary>Renumbered graphs.</summary>
    internal sealed record RenumberedResult(Graph InSearchOrder, NodeMapping SearchOrder,
        Graph InRunOrder, NodeMapping RunOrder);

    /// <summary>Node data carried over, and node IDs translated.</summary>
    internal sealed record NodeDataResult(int[] LoadingLines, int NewIdOfParse,
        bool MainKept, bool Partial, int MappedCount, int IndexCount,
        NodeMapping SecondToOriginal, int[] CalleeLines);

    /// <summary>The transposed graph with its edge data.</summary>
    internal sealed record TransposedResult(BiAdjacentGraph CalledBy,
        int TransposedIndexOfLoadToRead, List<string> CallersOfFormat);

    /// <summary>Groups of the caller's own and the condensed graph.</summary>
    internal sealed record OwnGroupsResult(NodePartition ByPackage, Graph PackageCalls,
        int GroupCount, int SizeOfService, SortedIntView MethodsOfService);

    /// <summary>Data combined per group.</summary>
    internal sealed record CombinedResult(int[] LinesPerPackage,
        int[] SitesPerPackageCall, SortedIntView Behind);

    /// <summary>
    /// The number of call sites of each call, by edge index of the calls.
    /// </summary>
    internal static int[] SiteCount()
    {
        Graph calls = SmallProgram.Calls();
        int[] siteCount = new int[calls.EdgeCount];
        foreach (CallSite site in SmallProgram.CallSites())
        {
            siteCount[calls.EdgeIndexOf(site.Caller, site.Callee)]++;
        }
        return siteCount;
    }

    /// <summary>
    /// Returns the subgraph of the methods that load reaches, and that of four given
    /// methods.
    /// </summary>
    internal static SubgraphResult Subgraph()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int load = SmallProgram.NodeOf(names, "load");
        // @start region="subgraph"
        // The subgraph of the methods that load reaches:
        NodeMapping newToOld = NodeMapping.ForSubgraph(Graphs.ReachableFrom(calls, load));
        Graph loading = Graphs.Remapped(calls, newToOld);
        // loading: 0 -> [1, 2], 1 -> [1], 2 -> [] - load -> [parse, read],
        // parse -> [parse]

        // The names come along through the mapping, from new to old node IDs.
        string[] loadingNames = new string[loading.NodeCount];
        newToOld.Gather(names, loadingNames);   // [load, parse, read]

        // Or a subgraph of nodes given by their node IDs: save, show, format and write.
        Graph savingAndShowing =
            Graphs.Remapped(calls, NodeMapping.ForSubgraph(4, 5, 7, 9));
        // savingAndShowing: 0 -> [2, 3], 1 -> [2], 2 -> [2], 3 -> [] - save, show,
        // format, write
        // @end
        return new SubgraphResult(loading, newToOld, loadingNames, savingAndShowing);
    }

    /// <summary>
    /// Returns the calls numbered in depth-first order, and the steps in topological
    /// order.
    /// </summary>
    internal static RenumberedResult Renumbered()
    {
        Graph calls = SmallProgram.Calls();
        Graph steps = SmallProgram.BuildSteps();
        // @start region="renumber"
        // In the order of a depth-first search: nodes visited one after another get
        // adjacent IDs.
        NodeMapping searchOrder = NodeMapping.Reindex(calls);
        Graph inSearchOrder = Graphs.Remapped(calls, searchOrder);
        // searchOrder, new to old: [0, 1, 2, 3, 6, 8, 4, 7, 9, 5] - parse and read follow
        // load

        // In an order of your own, here a topological one: every edge then goes to a
        // higher ID.
        NodeMapping runOrder = NodeMapping.ForPermutation(TopOrder.Of(steps).AsSpan());
        Graph inRunOrder = Graphs.Remapped(steps, runOrder);
        // runOrder, new to old: [2, 0, 1, 4, 3, 5, 6, 7, 8] - lint, generate, compile,
        // docs, ...
        // @end
        return new RenumberedResult(inSearchOrder, searchOrder, inRunOrder, runOrder);
    }

    /// <summary>
    /// Returns node data of the subgraph of load and translations between old and new
    /// IDs.
    /// </summary>
    internal static NodeDataResult NodeData()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int load = SmallProgram.NodeOf(names, "load");
        int parse = SmallProgram.NodeOf(names, "parse");
        int main = SmallProgram.NodeOf(names, "main");
        NodeMapping newToOld = NodeMapping.ForSubgraph(Graphs.ReachableFrom(calls, load));
        Graph loading = Graphs.Remapped(calls, newToOld);
        // @start region="nodeData"
        // The lines of code of each method:
        int[] linesOf = [12, 30, 20, 25, 18, 15, 60, 40, 10, 10];
        // [25, 60, 10] - load, parse, read
        int[] loadingLines = newToOld.Gather(linesOf);

        // From old to new node IDs: the inverse mapping. Not every old node has a new
        // one.
        NodeMapping oldToNew = newToOld.Inverted(calls.NodeCount);
        int newIdOfParse = oldToNew.Get(parse);          // 1 - parse is node 1 of loading
        bool mainKept = oldToNew.IsMapped(main);         // false - main is not in loading
        // true - 7 of 10 old nodes are not kept
        bool partial = oldToNew.IsPartial;
        int mappedCount = oldToNew.MappedCount;          // 3 - load, parse, read
        int indexCount = oldToNew.IndexCount;            // 10 - the methods of calls

        // Transformed twice - loading, then its subgraph of the out-nodes of its node 0,
        // load: compose the two mappings, and gather from the data of calls in one step.
        NodeMapping secondToFirst = NodeMapping.ForSubgraph(loading.OutNodes(0));
        NodeMapping secondToOriginal = newToOld.Compose(secondToFirst);
        int[] calleeLines = secondToOriginal.Gather(linesOf);   // [60, 10] - parse, read
        // @end
        return new NodeDataResult(loadingLines, newIdOfParse, mainKept, partial,
            mappedCount, indexCount, secondToOriginal, calleeLines);
    }

    /// <summary>Returns the call sites of each call of the subgraph of load.</summary>
    internal static int[] EdgeData()
    {
        Graph calls = SmallProgram.Calls();
        int load = SmallProgram.NodeOf(SmallProgram.MethodNames(), "load");
        int[] siteCount = SiteCount();
        NodeMapping newToOld = NodeMapping.ForSubgraph(Graphs.ReachableFrom(calls, load));
        Graph loading = Graphs.Remapped(calls, newToOld);
        // @start region="edgeData"
        EdgeMapping edges = Graphs.EdgeMapping(loading, calls, newToOld);
        int[] loadingSiteCount = edges.Gather(siteCount);
        // loadingSiteCount: [1, 1, 2] - load -> parse, load -> read, parse -> parse
        // @end
        return loadingSiteCount;
    }

    /// <summary>
    /// Returns the callers of format with their call sites, through the transposed graph.
    /// </summary>
    internal static TransposedResult Transposed()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int format = SmallProgram.NodeOf(names, "format");
        int[] siteCount = SiteCount();
        int loadToRead = calls.EdgeIndexOf(
            SmallProgram.NodeOf(names, "load"), SmallProgram.NodeOf(names, "read"));
        // @start region="transposed"
        BiAdjacentGraph both = BiAdjacentGraph.Of(calls);
        BiAdjacentGraph calledBy = both.Transposed();   // without copying
        int transposedIndex = both.TransposedEdgeIndex(loadToRead);   // 10 - read -> load

        // The edge data, by edge index of the transposed graph:
        int[] calledBySiteCount = Graphs.TransposedEdgeMapping(calls).Gather(siteCount);
        List<string> callersOfFormat = [];
        for (int edge = calledBy.OutEdgeStart(format);
             edge < calledBy.OutEdgeEnd(format);
             edge++)
        {
            callersOfFormat.Add(
                $"{names[calledBy.EdgeTarget(edge)]}: {calledBySiteCount[edge]}");
        }
        // callersOfFormat: [save: 1, show: 1, format: 1]
        // @end
        return new TransposedResult(calledBy, transposedIndex, callersOfFormat);
    }

    /// <summary>Returns the calls grouped by package and condensed.</summary>
    internal static OwnGroupsResult OwnGroups()
    {
        Graph calls = SmallProgram.Calls();
        int[] packageOf = SmallProgram.PackageOf();
        int service = SmallProgram.NodeOf(SmallProgram.PackageNames(), "service");
        // @start region="ownGroups"
        NodePartition byPackage = NodePartition.Of(calls, packageOf, 8);
        // 8 - json and util are empty
        int groupCount = byPackage.GroupCount;
        int sizeOfService = byPackage.SizeOf(service);                 // 2
        // [3, 4] - load and save
        SortedIntView methodsOfService = byPackage.NodesOf(service);

        // The calls between packages: an edge wherever a method of one calls a method of
        // another.
        Graph packageCalls = byPackage.Condense();
        // packageCalls: app -> [cli, ui, service], ui -> [model],
        //               service -> [model, storage]
        // @end
        return new OwnGroupsResult(byPackage, packageCalls, groupCount, sizeOfService,
            methodsOfService);
    }

    /// <summary>Returns node and edge data combined per package.</summary>
    internal static CombinedResult Combined()
    {
        Graph calls = SmallProgram.Calls();
        NodePartition byPackage = NodePartition.Of(calls, SmallProgram.PackageOf(), 8);
        Graph packageCalls = byPackage.Condense();
        string[] packages = SmallProgram.PackageNames();
        int service = SmallProgram.NodeOf(packages, "service");
        int model = SmallProgram.NodeOf(packages, "model");
        int[] siteCount = SiteCount();
        // @start region="reduce"
        // The lines of code of each method:
        int[] linesOf = [12, 30, 20, 25, 18, 15, 60, 40, 10, 10];
        int[] linesPerPackage = byPackage.Reduce(linesOf, 0, (a, b) => a + b);
        // linesPerPackage: [32, 30, 15, 43, 100, 20, 0, 0] - model has 100, json and util
        // none

        // Edge data: which calls are behind each call between packages, and their sum.
        EdgePartition byPackageCall = Graphs.EdgePartition(packageCalls, byPackage);
        SortedIntView behind =
            byPackageCall.EdgesOf(packageCalls.EdgeIndexOf(service, model));
        // behind: [5, 7] - load -> parse and save -> format
        int[] sitesPerPackageCall = byPackageCall.Reduce(siteCount, 0, (a, b) => a + b);
        // sitesPerPackageCall: [1, 2, 2, 1, 2, 2] - app -> cli has 1, service -> model 2
        // @end
        return new CombinedResult(linesPerPackage, sitesPerPackageCall, behind);
    }
}
