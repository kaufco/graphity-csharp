using Graphity;

namespace Examples;

/// <summary>Building a graph, and looking at it.</summary>
internal static class Building
{
    /// <summary>
    /// Builds the control flow of <c>load</c> from the two arrays of its CSR layout.
    /// </summary>
    internal static Graph CfgFromArrays()
    {
        // @start region="of"
        // blocks 0..5: entry, header, body, then, next, exit
        int[] outAdjacencyStart = [0, 1, 3, 5, 6, 7, 7];
        int[] outAdjacency =
        [
            1,       // entry  -> header
            2, 5,    // header -> body, exit
            3, 4,    // body   -> then, next
            4,       // then   -> next
            1,       // next   -> header
        ];
        Graph cfg = Graph.Of(outAdjacencyStart, outAdjacency);
        // @end
        return cfg;
    }

    /// <summary>Returns the text of the control flow of <c>load</c>.</summary>
    internal static string CfgAsText()
    {
        Graph cfg = SmallProgram.Cfg();
        // @start region="toString"
        string text = cfg.ToString();
        // Graph[nodes=6, edges=7] {0 -> [1], 1 -> [2, 5], 2 -> [3, 4], 3 -> [4],
        //                          4 -> [1], 5 -> []}
        // @end
        return text;
    }
}
