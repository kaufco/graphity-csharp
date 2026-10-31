namespace Graphity.Core.Tests;

/// <summary>
/// Covers the Sccs-specific members (<see cref="Sccs.ContainsCycle"/>,
/// <see cref="Sccs.IsStronglyConnected"/>, <see cref="Sccs.SccOf"/>, <see cref="Sccs.SccCount"/>).
/// </summary>
public sealed class SccsTests
{
    [Fact]
    public void ContainsCycleTrueForMultiNodeScc()
    {
        var sccs = Components.SccsOf(TestGraphs.Cycle(3));
        Assert.Equal(1, sccs.GroupCount);
        Assert.True(sccs.ContainsCycle(0));
    }

    [Fact]
    public void ContainsCycleTrueForSingletonWithSelfLoop()
    {
        var sccs = Components.SccsOf(TestGraphs.Of(1, 0, 0));
        Assert.True(sccs.ContainsCycle(0));
    }

    [Fact]
    public void ContainsCycleFalseForSingletonWithoutSelfLoop()
    {
        // 0 -> 1: two singleton SCCs, no cycle.
        var sccs = Components.SccsOf(TestGraphs.Of(2, 0, 1));
        Assert.False(sccs.ContainsCycle(sccs.Get(0)));
        Assert.False(sccs.ContainsCycle(sccs.Get(1)));
    }

    [Fact]
    public void ContainsCycleMixedComponents()
    {
        // 0 -> 0 (self-loop), 1 -> 2 (DAG), 3 -> 4 -> 3 (cycle).
        var g = TestGraphs.Of(5, 0, 0, 1, 2, 3, 4, 4, 3);
        var sccs = Components.SccsOf(g);
        Assert.True(sccs.ContainsCycle(sccs.Get(0)));
        Assert.False(sccs.ContainsCycle(sccs.Get(1)));
        Assert.False(sccs.ContainsCycle(sccs.Get(2)));
        Assert.True(sccs.ContainsCycle(sccs.Get(3)));
        Assert.True(sccs.ContainsCycle(sccs.Get(4)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(1000)]
    public void ContainsCycleOutOfRangeThrows(int sccId)
    {
        var sccs = Components.SccsOf(TestGraphs.Path(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => sccs.ContainsCycle(sccId));
    }

    [Fact]
    public void IsStronglyConnectedTrueForEmptyGraph() =>
        Assert.True(Components.SccsOf(TestGraphs.Empty()).IsStronglyConnected);

    [Fact]
    public void IsStronglyConnectedTrueForSingleNode() =>
        Assert.True(Components.SccsOf(TestGraphs.Isolated(1)).IsStronglyConnected);

    [Fact]
    public void IsStronglyConnectedTrueForPureCycle() =>
        Assert.True(Components.SccsOf(TestGraphs.Cycle(4)).IsStronglyConnected);

    [Fact]
    public void IsStronglyConnectedFalseForDisconnectedNodes() =>
        Assert.False(Components.SccsOf(TestGraphs.Isolated(3)).IsStronglyConnected);

    [Fact]
    public void IsStronglyConnectedFalseForOneWayChain() =>
        Assert.False(Components.SccsOf(TestGraphs.Path(3)).IsStronglyConnected);

    [Fact]
    public void SccOfAndSccCountDelegateToThePartitionMembers()
    {
        // Cycle {0, 1} feeding sink 2.
        var sccs = Components.SccsOf(TestGraphs.Of(3, 0, 1, 1, 0, 1, 2));
        Assert.Equal(sccs.GroupCount, sccs.SccCount);
        Assert.Equal(2, sccs.SccCount);
        for (int node = 0; node < 3; node++)
        {
            Assert.Equal(sccs.Get(node), sccs.SccOf(node));
        }
        Assert.Equal(sccs.SccOf(0), sccs.SccOf(1));
        Assert.NotEqual(sccs.SccOf(0), sccs.SccOf(2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(1000)]
    public void SccOfOutOfRangeThrows(int node)
    {
        var sccs = Components.SccsOf(TestGraphs.Path(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => sccs.SccOf(node));
    }
}
