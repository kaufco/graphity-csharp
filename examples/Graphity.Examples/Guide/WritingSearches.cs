using Graphity;

namespace Examples.Guide;

/// <summary>
/// Searches of one's own, on the traversers of Graphity and on the arrays of a graph.
/// </summary>
/// <remarks>
/// Java declares each traverser as a local class in the method that runs it. C# has no
/// local classes, so each region here holds a nested class and, after it, the method that
/// runs it, with the variables of the Java method as its parameters; a method without
/// parameters supplies them.
/// </remarks>
internal static class WritingSearches
{
    /// <summary>
    /// The methods reached without the storage package, and the recursive calls on the
    /// way.
    /// </summary>
    internal sealed record WithoutStorageResult(List<string> Reached,
        List<string> Recursive);

    /// <summary>
    /// Per-method sums and counts, computed on the arrays of the graph.
    /// </summary>
    internal sealed record FromArraysResult(int[] SitesIn, int[] CallerCount,
        int[] SitesFromOthers);

    /// <summary>
    /// Returns the number of descendants of every person, computed bottom-up.
    /// </summary>
    internal static int[] CountDescendants() =>
        CountDescendants(SmallFamily.Tree(), SmallFamily.PersonOf("Ada"));

    // @start region="dfs"
    // Graph, a property of DfsTraverser, is the graph it searches.
    private sealed class Descendants(Graph family) : DfsTraverser(family)
    {
        public int[] Count { get; } = new int[family.NodeCount];

        protected override bool EnterNode(int person)
        {
            return true;   // search from the children of every person
        }

        protected override void LeaveNode(int person)
        {
            // Called after the whole subtree of the person: the counts of the children
            // are complete.
            foreach (int child in Graph.OutNodes(person))
            {
                Count[person] += 1 + Count[child];
            }
        }
    }

    private static int[] CountDescendants(Graph family, int ada)
    {
        var descendants = new Descendants(family);
        descendants.Run(ada);
        // descendants.Count: [9, 4, 3, 2, 0, 2, 0, 0, 0, 0] - Ada has 9, Ben 4, Cleo 3,
        // ...
        return descendants.Count;
    }
    // @end

    /// <summary>Returns what main reaches without calling into storage.</summary>
    internal static WithoutStorageResult WithoutStorage()
    {
        string[] names = SmallProgram.MethodNames();
        return WithoutStorage(SmallProgram.Calls(), names, SmallProgram.PackageOf(),
            SmallProgram.NodeOf(SmallProgram.PackageNames(), "storage"),
            SmallProgram.NodeOf(names, "main"));
    }

    // @start region="dfsEdges"
    private sealed class AvoidingStorage(
        Graph calls, string[] names, int[] packageOf, int storage)
        : DfsEdgeTraverser(calls)
    {
        public List<string> Reached { get; } = [];

        public List<string> Recursive { get; } = [];

        protected override bool EnterNode(int method)
        {
            Reached.Add(names[method]);
            return true;
        }

        protected override EdgeAction VisitEdge(
            int caller, int callee, int edge, TargetState state)
        {
            if (state == TargetState.OnStack)
            {
                Recursive.Add($"{names[caller]} -> {names[callee]}");   // closes a cycle
            }
            if (packageOf[callee] == storage)
            {
                return EdgeAction.Skip;   // do not follow calls into storage
            }
            return base.VisitEdge(caller, callee, edge, state);
        }
    }

    private static WithoutStorageResult WithoutStorage(
        Graph calls, string[] names, int[] packageOf, int storage, int main)
    {
        var search = new AvoidingStorage(calls, names, packageOf, storage);
        search.Run(main);
        // search.Reached: [main, parseArgs, run, load, parse, save, format, show]
        // search.Recursive: [parse -> parse, format -> format]
        return new WithoutStorageResult(search.Reached, search.Recursive);
    }
    // @end

    /// <summary>Returns the methods by call depth from main.</summary>
    internal static List<List<string>> MethodsByDepth()
    {
        string[] names = SmallProgram.MethodNames();
        return MethodsByDepth(
            SmallProgram.Calls(), names, SmallProgram.NodeOf(names, "main"));
    }

    // @start region="frontier"
    private sealed class ByDepth(Graph calls, string[] names) : FrontierTraverser(calls)
    {
        private List<string> _current = [];

        public List<List<string>> Levels { get; } = [];

        protected override void VisitNode(int method)
        {
            _current.Add(names[method]);
            EnqueueChildren(method);   // the out-nodes form the next level
        }

        protected override void LeaveLevel(int level)
        {
            Levels.Add(_current);      // after the last node of each level
            _current = [];
        }
    }

    private static List<List<string>> MethodsByDepth(
        Graph calls, string[] names, int main)
    {
        var byDepth = new ByDepth(calls, names);
        byDepth.Run(main);
        // byDepth.Levels: [[main], [parseArgs, run], [load, save, show],
        //                  [parse, read, format, write]]
        return byDepth.Levels;
    }
    // @end

    /// <summary>
    /// Returns the call sites in each method, its callers, and where other methods call
    /// it.
    /// </summary>
    internal static FromArraysResult FromArrays()
    {
        Graph calls = SmallProgram.Calls();
        int[] siteCount = TransformingGraphs.SiteCount();
        // @start region="arrays"
        ReadOnlySpan<int> start = Csr.OutAdjacencyStart(calls);
        ReadOnlySpan<int> callees = Csr.OutAdjacency(calls);
        int[] sitesIn = new int[calls.NodeCount];
        int[] callerCount = new int[calls.NodeCount];
        for (int method = 0; method < calls.NodeCount; method++)
        {
            // positions = edges
            for (int edge = start[method]; edge < start[method + 1]; edge++)
            {
                sitesIn[method] += siteCount[edge];
                callerCount[callees[edge]]++;
            }
        }
        // sitesIn: [2, 0, 4, 2, 2, 1, 2, 1, 0, 0] - run calls from 4 places in its code
        // callerCount: [0, 1, 1, 1, 1, 1, 2, 3, 1, 1] - format has 3 callers, itself
        // included

        // The in-nodes of a BiAdjacentGraph have the same layout, but a position there is
        // an edge index of the transposed graph: the edge data has to follow first.
        BiAdjacentGraph both = BiAdjacentGraph.Of(calls);
        ReadOnlySpan<int> inStart = Csr.InAdjacencyStart(both);
        ReadOnlySpan<int> callers = Csr.InAdjacency(both);
        int[] calledBySiteCount = Graphs.TransposedEdgeMapping(calls).Gather(siteCount);
        int[] sitesFromOthers = new int[calls.NodeCount];
        for (int method = 0; method < calls.NodeCount; method++)
        {
            for (int edge = inStart[method]; edge < inStart[method + 1]; edge++)
            {
                if (callers[edge] != method)
                {
                    sitesFromOthers[method] += calledBySiteCount[edge];
                }
            }
        }
        // sitesFromOthers: [0, 1, 1, 1, 1, 2, 1, 2, 1, 1] - show is called from 2 places
        // @end
        return new FromArraysResult(sitesIn, callerCount, sitesFromOthers);
    }
}
