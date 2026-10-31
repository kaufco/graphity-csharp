namespace Graphity.Core.Tests;

/// <summary>
/// EdgePartition shares its CSR, Get, SizeOf and Reduce logic with NodePartition through
/// IndexPartition. Here: partial mapping (edges inside a group map to -1) and the
/// <c>EdgeCount</c>/<c>EdgesOf</c> aliases. The integration with Graphs.EdgePartition is covered in
/// GraphsTests and CondenseFuzzTests.
/// </summary>
public sealed class EdgePartitionTests
{
    /// <summary>
    /// Six original edges, three groups: edges 0 and 1 → 0, edge 2 unmapped, edge 3 → 1, edge 4
    /// unmapped, edge 5 → 2.
    /// </summary>
    private static EdgePartition SampleEdgePartition() => new([0, 0, -1, 1, -1, 2], [0, 1, 3, 5], [0, 2, 3, 4]);

    [Fact]
    public void GroupCountReportsNumberOfCondensedEdges() => Assert.Equal(3, SampleEdgePartition().GroupCount);

    [Fact]
    public void IndexCountReportsTotalOriginalEdges()
    {
        var p = SampleEdgePartition();
        Assert.Equal(6, p.IndexCount);
        Assert.Equal(6, p.EdgeCount);
    }

    [Fact]
    public void GetReturnsCondensedEdgeOrMinusOne()
    {
        var p = SampleEdgePartition();
        Assert.Equal(0, p.Get(0));
        Assert.Equal(0, p.Get(1));
        Assert.Equal(-1, p.Get(2));
        Assert.Equal(1, p.Get(3));
        Assert.Equal(-1, p.Get(4));
        Assert.Equal(2, p.Get(5));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(100)]
    public void GetOutOfRangeThrows(int edge)
    {
        var p = SampleEdgePartition();
        Assert.Throws<ArgumentOutOfRangeException>(() => p.Get(edge));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.IsMapped(edge));
    }

    [Fact]
    public void IsPartialTrueWhenIntraGroupEdgesPresent()
    {
        var p = SampleEdgePartition();
        Assert.True(p.IsPartial);
        Assert.Equal(4, p.MappedCount);
    }

    [Fact]
    public void IndicesOfReturnsSortedOriginalEdgesPerCondensedEdge()
    {
        var p = SampleEdgePartition();
        Assert.Equal([0, 1], p.IndicesOf(0).ToArray());
        Assert.Equal([3], p.IndicesOf(1).ToArray());
        Assert.Equal([5], p.IndicesOf(2).ToArray());
    }

    [Fact]
    public void EdgesOfDelegatesToIndicesOf()
    {
        var p = SampleEdgePartition();
        Assert.Equal([0, 1], p.EdgesOf(0).ToArray());
        Assert.Equal([3], p.EdgesOf(1).ToArray());
        Assert.Equal([5], p.EdgesOf(2).ToArray());
    }

    [Fact]
    public void SizeOfReportsEdgesPerGroup()
    {
        var p = SampleEdgePartition();
        Assert.Equal(2, p.SizeOf(0));
        Assert.Equal(1, p.SizeOf(1));
        Assert.Equal(1, p.SizeOf(2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(100)]
    public void IndicesOfOutOfRangeThrows(int groupId) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SampleEdgePartition().IndicesOf(groupId));

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(100)]
    public void SizeOfOutOfRangeThrows(int groupId) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SampleEdgePartition().SizeOf(groupId));

    // --- Reduce ---

    [Fact]
    public void ReduceFoldsTheMembersOfEachGroup()
    {
        var p = SampleEdgePartition();
        // groups: {0, 1}, {3}, {5}; edges 2 and 4 are in no group
        Assert.Equal([3, 4, 8], p.Reduce<int>([1, 2, 100, 4, 100, 8], 0, (a, b) => a + b));
        Assert.Equal([2L, 4L, 8L], p.Reduce<long>([1, 2, 100, 4, 100, 8], 1L, (a, b) => a * b));
        Assert.Equal([2.0, 4.0, 8.0], p.Reduce<double>([1, 2, 100, 4, 100, 8], 0.0, Math.Max));
        var groupValues = new string?[3];
        p.Reduce<string?>(["a", "b", "x", "c", "y", "d"], groupValues, string.Concat);
        Assert.Equal(new string?[] { "ab", "c", "d" }, groupValues);
    }

    [Fact]
    public void ReduceRejectsArraysOfTheWrongLength()
    {
        var p = SampleEdgePartition();
        Assert.Throws<ArgumentException>(() => p.Reduce<int>(new int[5], 0, (a, b) => a + b));
        Assert.Throws<ArgumentException>(() => p.Reduce<string?>(new string?[6], new string?[2], string.Concat));
    }

    [Fact]
    public void ReduceRejectsTheSameArrayForValuesAndGroupValues()
    {
        // Three edges in three singleton groups, so both arrays have the same length.
        var p = new EdgePartition([1, 0, 2], [1, 0, 2], [0, 1, 2, 3]);
        string[] values = ["a", "b", "c"];
        Assert.Throws<ArgumentException>(() => p.Reduce<string>(values, values, string.Concat));
        Assert.Equal(["a", "b", "c"], values);
    }
}
