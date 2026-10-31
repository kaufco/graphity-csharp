using Graphity;

namespace Examples.Guide;

/// <summary>
/// The two kinds of lists that Graphity returns: sorted node lists and edge lists.
/// </summary>
internal static class ReadingResultLists
{
    /// <summary>What the example reads from sorted lists.</summary>
    internal sealed record Sorted(int Count, int First, int Last, int Position,
        bool HasFormat, SortedIntView Both, int[] Copy);

    /// <summary>
    /// Returns what the example reads from the out-nodes of two methods.
    /// </summary>
    internal static Sorted SortedLists()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int run = SmallProgram.NodeOf(names, "run");
        int save = SmallProgram.NodeOf(names, "save");
        int show = SmallProgram.NodeOf(names, "show");
        int format = SmallProgram.NodeOf(names, "format");
        // @start region="sortedLists"
        // [3, 4, 5] - load, save and show
        SortedIntView calledByRun = calls.OutNodes(run);
        int count = calledByRun.Count;                      // 3
        int first = calledByRun.First();                    // 3
        int last = calledByRun.Last();                      // 5
        int position = calledByRun.IndexOf(save);           // 1 - found by binary search
        bool hasFormat = calledByRun.Contains(format);      // false

        // Out-nodes come in ascending order and without duplicates, so they combine
        // cheaply: the methods that save or show call.
        SortedIntView both = calls.OutNodes(save).Union(calls.OutNodes(show));
        // both: [7, 9] - format and write

        // [3, 4, 5] - a copy that may be changed
        int[] copy = calledByRun.ToArray();
        // @end
        return new Sorted(count, first, last, position, hasFormat, both, copy);
    }

    /// <summary>Returns the recursive calls, read from an edge list.</summary>
    internal static List<string> EdgeLists()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        // @start region="edges"
        // An algorithm that returns edges returns an Edges: here the back edges of a
        // search, which are the recursive calls, since the only cycles of calls are
        // methods that call themselves.
        // Position p is the edge SourceNodes[p] -> TargetNodes[p], and EdgeIndices[p] is
        // its edge index in calls.
        Edges recursive = BackEdges.Of(calls);
        List<string> lines = [];
        for (int p = 0; p < recursive.Count; p++)
        {
            int caller = recursive.SourceNodes[p];
            int callee = recursive.TargetNodes[p];
            int edge = recursive.EdgeIndices[p];
            lines.Add($"{names[caller]} -> {names[callee]} (edge {edge})");
        }
        // lines: [parse -> parse (edge 10), format -> format (edge 11)]
        // @end
        return lines;
    }
}
