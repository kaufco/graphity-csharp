using Graphity;

namespace Examples.Guide;

/// <summary>
/// A graph made from the two arrays of its CSR layout, and the arrays read back.
/// </summary>
internal static class UsingCsrArrays
{
    /// <summary>The graph and the arrays read back from it, copied.</summary>
    internal sealed record FromArrays(Graph Graph, int[] Start, int[] Adjacency);

    /// <summary>
    /// Returns the control flow of the method Load, made from its two arrays.
    /// </summary>
    internal static FromArrays Load()
    {
        // @start region="csr"
        // The control flow of the method Load, as another tool might hand it over: the
        // out-nodes of all its blocks, block after block, and where the out-nodes of each
        // block start. The blocks entry, header, body, then, next and exit are the nodes
        // 0 to 5.
        int[] start = [0, 1, 3, 5, 6, 7, 7];
        int[] adjacency = [1, 2, 5, 3, 4, 4, 1];
        Graph cfg = Graph.Of(start, adjacency);     // copies and checks both arrays
        // cfg: 6 nodes, 7 edges

        // The other way round: the arrays of a graph, for code of your own that reads
        // every edge. They are the graph's own arrays, not copies, handed out as
        // read-only spans.

        // [0, 1, 3, 5, 6, 7, 7]
        ReadOnlySpan<int> outStart = Csr.OutAdjacencyStart(cfg);
        // [1, 2, 5, 3, 4, 4, 1]
        ReadOnlySpan<int> outNodes = Csr.OutAdjacency(cfg);
        // @end
        return new FromArrays(cfg, outStart.ToArray(), outNodes.ToArray());
    }
}
