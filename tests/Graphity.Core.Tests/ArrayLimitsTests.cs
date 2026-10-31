namespace Graphity.Core.Tests;

/// <summary>
/// The limits derived from the largest array, against the thresholds the documentation states. A
/// graph at a limit takes gigabytes, so the checks themselves are exercised on small numbers. .NET's
/// largest array is <see cref="Array.MaxLength"/> (2^31 - 57), not the JDK's 2^31 - 9, so the derived
/// limits are lower than in Java; the derivations are the same.
/// </summary>
public sealed class ArrayLimitsTests
{
    [Fact]
    public void TheLargestArrayIsTheDotNetMaximum()
    {
        Assert.Equal(ArrayLimits.MaxArrayLength, Array.MaxLength);
        Assert.True(ArrayLimits.MaxArrayLength == int.MaxValue - 56);
    }

    [Fact]
    public void AGraphHoldsOneNodeFewerThanTheLargestArray()
    {
        // GraphBuilder, NodePartition.Of and the header of Graph.ReadFrom: nodeCount + 1 offsets.
        int limit = ArrayLimits.MaxNodeCount;
        Assert.Equal(int.MaxValue - 57, limit);
        Assert.Equal(ArrayLimits.MaxArrayLength, limit + 1);
    }

    [Fact]
    public void TwoIntsPerNodeAllowTwoToTheThirtyMinusTwentyNineNodes()
    {
        // SccsOf, IsBipartite and the depth of a depth-first path.
        int limit = ArrayLimits.MaxNodeCountTwoInts;
        Assert.Equal((1 << 30) - 29, limit);
        Assert.True(2L * limit <= ArrayLimits.MaxArrayLength);
        Assert.True(2L * (limit + 1) > ArrayLimits.MaxArrayLength);
    }

    [Fact]
    public void TheControlFlowAnalysesAllowOneNodeLessForTheVirtualRoot()
    {
        // Dominators, Loops and StrongConnectivityOf.
        int limit = DfsPreorder.MaxNodeCount;
        Assert.Equal((1 << 30) - 30, limit);
        Assert.True(2L * (limit + 1) <= ArrayLimits.MaxArrayLength);
        Assert.True(2L * (limit + 2) > ArrayLimits.MaxArrayLength);
    }

    [Fact]
    public void CheckNodeCountRejectsOnlyCountsAboveTheLimit()
    {
        ArrayLimits.CheckNodeCount(10, 10, "Some analysis");
        var ex = Assert.Throws<ArgumentException>(() => ArrayLimits.CheckNodeCount(11, 10, "Some analysis"));
        Assert.Equal("Some analysis keeps two ints per node in one array, counting a virtual"
            + " root if it adds one, and so requires nodeCount <= 10, but was 11", ex.Message);
    }
}
