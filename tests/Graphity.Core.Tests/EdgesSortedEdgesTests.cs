namespace Graphity.Core.Tests;

public sealed class EdgesSortedEdgesTests
{
    /// <summary>Fills the slots past count, as a growing list leaves them; never a valid node.</summary>
    private const int Junk = 1_000_000;

    [Fact]
    public void OrdersRandomAscendingAndDescendingInputAlike()
    {
        var random = new JavaRandom(5);
        for (int iteration = 0; iteration < 500; iteration++)
        {
            int nodeCount = 1 + random.NextInt(20);
            int count = random.NextInt(40);
            var keys = new long[count];
            for (int i = 0; i < count; i++)
            {
                keys[i] = ((long)random.NextInt(nodeCount) << 32) | (uint)random.NextInt(nodeCount);
            }
            switch (iteration % 3)
            {
                case 1:
                    Array.Sort(keys);
                    break;
                case 2:
                    Array.Sort(keys);
                    Array.Reverse(keys);
                    break;
            }
            foreach (bool dropDuplicates in new[] { false, true })
            {
                AssertSorted(keys, nodeCount, dropDuplicates, iteration);
            }
        }
    }

    [Fact]
    public void ReadsOnlyTheFirstCountEntriesAndLeavesTheArraysUntouched()
    {
        // 0 -> 1 (edge 0), 0 -> 2 (1), 1 -> 2 (2), 2 -> 0 (3), 3 -> 1 (4).
        var graph = TestGraphs.Of(4, 0, 1, 0, 2, 1, 2, 2, 0, 3, 1);
        int[][] mixed = [[2, 0, 3, 0, 1], [0, 2, 1, 1, 2], [3, -1, 4, 0, -1]];
        int[][] ascending = [[0, 0, 1, 2, 3], [1, 2, 2, 0, 1], [-1, 1, 2, -1, 4]];
        int[][] descending = [[3, 2, 1, 0, 0], [1, 0, 2, 2, 1], [4, 3, -1, 1, 0]];
        foreach (int[][] input in new[] { mixed, ascending, descending })
        {
            int[] sources = WithJunkTail(input[0]);
            int[] targets = WithJunkTail(input[1]);
            int[] edgeIndices = WithJunkTail(input[2]);
            int[] sourcesBefore = (int[])sources.Clone();
            int[] targetsBefore = (int[])targets.Clone();
            int[] edgeIndicesBefore = (int[])edgeIndices.Clone();

            var edges = Edges.SortedEdges(graph, sources, targets, edgeIndices, 5, false);

            Assert.Equal([0, 0, 1, 2, 3], edges.SourceNodes.ToArray());
            Assert.Equal([1, 2, 2, 0, 1], edges.TargetNodes.ToArray());
            Assert.Equal([0, 1, 2, 3, 4], edges.EdgeIndices.ToArray());
            Assert.Equal(sourcesBefore, sources);
            Assert.Equal(targetsBefore, targets);
            Assert.Equal(edgeIndicesBefore, edgeIndices);
        }
    }

    [Fact]
    public void DropsDuplicatesFromParallelArrays()
    {
        // 0 -> 1 (edge 0), 1 -> 0 (1), 1 -> 2 (2).
        var graph = TestGraphs.Of(3, 0, 1, 1, 0, 1, 2);
        int[] sources = WithJunkTail([1, 0, 1, 1, 0]);
        int[] targets = WithJunkTail([2, 1, 0, 2, 1]);
        int[] edgeIndices = WithJunkTail([-1, 0, 1, 2, -1]);

        var edges = Edges.SortedEdges(graph, sources, targets, edgeIndices, 5, true);

        Assert.Equal([0, 1, 1], edges.SourceNodes.ToArray());
        Assert.Equal([1, 0, 2], edges.TargetNodes.ToArray());
        Assert.Equal([0, 1, 2], edges.EdgeIndices.ToArray());
    }

    [Fact]
    public void NoEdgesGiveTheSharedEmptyList() =>
        Assert.Same(Edges.Empty, Edges.SortedEdges(TestGraphs.Path(3), [Junk], [Junk], [Junk], 0, true));

    private static int[] WithJunkTail(int[] values)
    {
        var array = new int[values.Length + 3];
        values.CopyTo(array, 0);
        array.AsSpan(values.Length).Fill(Junk);
        return array;
    }

    private static void AssertSorted(long[] keys, int nodeCount, bool dropDuplicates, int iteration)
    {
        int count = keys.Length;
        var pairs = new int[2 * count];
        for (int i = 0; i < count; i++)
        {
            pairs[2 * i] = (int)(keys[i] >>> 32);
            pairs[(2 * i) + 1] = (int)keys[i];
        }
        var builder = new GraphBuilder();
        builder.AddNodes(nodeCount);
        builder.AddEdgePairs(pairs);
        Graph graph = builder.Build();
        long[] expected = [.. keys.Order()];
        if (dropDuplicates)
        {
            expected = [.. expected.Distinct()];
        }
        // Half the iterations pass the edge indices, half leave them to be looked up.
        var sources = new int[count];
        var targets = new int[count];
        var edgeIndices = new int[count];
        for (int i = 0; i < count; i++)
        {
            sources[i] = pairs[2 * i];
            targets[i] = pairs[(2 * i) + 1];
            edgeIndices[i] = iteration % 2 == 0 ? graph.EdgeIndexOf(sources[i], targets[i]) : -1;
        }
        var edges = Edges.SortedEdges(graph, sources, targets, edgeIndices, count, dropDuplicates);
        var actual = new long[edges.Count];
        for (int p = 0; p < actual.Length; p++)
        {
            int source = edges.SourceNodes[p];
            int target = edges.TargetNodes[p];
            actual[p] = ((long)source << 32) | (uint)target;
            Assert.Equal(graph.EdgeIndexOf(source, target), edges.EdgeIndices[p]);
        }
        Assert.True(expected.SequenceEqual(actual), $"iteration {iteration}, dropDuplicates {dropDuplicates}");
    }
}
