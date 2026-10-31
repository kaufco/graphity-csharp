using Graphity;

namespace Examples.Guide;

/// <summary>
/// The bridges and articulation points of the calls between methods, read as undirected:
/// the single calls and methods whose removal splits a component; and the blocks of the
/// calls.
/// </summary>
internal static class FindingBridges
{
    /// <summary>The bridges and articulation points.</summary>
    internal sealed record SplittersResult(Edges Bridges,
        SortedIntView ArticulationPoints);

    /// <summary>The blocks, and the edges in no block.</summary>
    internal sealed record BlocksResult(int BlockCount, List<string> LargeBlock,
        List<string> InNoBlock);

    /// <summary>
    /// Returns the bridges and articulation points of the calls, read as undirected.
    /// </summary>
    internal static SplittersResult Splitters()
    {
        Graph calls = SmallProgram.Calls();
        // @start region="biconnectivity"
        // The analysis reads the edges as undirected and follows them both ways, so it
        // takes a BiAdjacentGraph.
        Biconnectivity biconnectivity =
            Components.BiconnectivityOf(BiAdjacentGraph.Of(calls));
        Edges bridges = biconnectivity.Bridges;
        // bridges: main -> parseArgs, main -> run, run -> load, load -> parse,
        //          load -> read, save -> write
        SortedIntView articulationPoints = biconnectivity.ArticulationPoints;
        // articulationPoints: [0, 2, 3, 4] - main, run, load and save
        // @end
        return new SplittersResult(bridges, articulationPoints);
    }

    /// <summary>
    /// Returns the blocks of the calls: their number, the edges of the large block, those
    /// in none.
    /// </summary>
    internal static BlocksResult Blocks()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        // @start region="blocks"
        // The blocks come from the same analysis as the bridges.
        Biconnectivity biconnectivity =
            Components.BiconnectivityOf(BiAdjacentGraph.Of(calls));
        EdgePartition blocks = biconnectivity.Blocks;
        // 7 - six blocks of one edge, one of four edges
        int blockCount = blocks.GroupCount;

        // The edges other than self-loops that lie on a cycle, read as undirected: those
        // of the blocks of three edges or more. A bridge is a block of one edge, or of
        // two if it is stored in both directions.
        List<string> largeBlock = [];
        for (int block = 0; block < blockCount; block++)
        {
            if (blocks.SizeOf(block) > 2)
            {
                foreach (int edge in blocks.EdgesOf(block))
                {
                    largeBlock.Add(
                        $"{names[calls.EdgeSource(edge)]} -> "
                        + $"{names[calls.EdgeTarget(edge)]}");
                }
            }
        }
        // largeBlock: [run -> save, run -> show, save -> format, show -> format]
        // @end
        List<string> inNoBlock = [];
        for (int edge = 0; edge < calls.EdgeCount; edge++)
        {
            if (blocks.Get(edge) == -1)
            {
                inNoBlock.Add(
                    $"{names[calls.EdgeSource(edge)]} -> "
                    + $"{names[calls.EdgeTarget(edge)]}");
            }
        }
        return new BlocksResult(blockCount, largeBlock, inNoBlock);
    }
}
