using System.Collections.Immutable;

namespace Graphity.Core.Tests;

/// <remarks>
/// Java's <c>pathIsReadOnly</c> (an <c>UnsupportedOperationException</c> on <c>set</c>) has no
/// counterpart: <see cref="BfsDistances.PathTo"/> returns an <see cref="ImmutableArray{T}"/>, which has
/// no mutator.
/// </remarks>
public sealed class BfsDistancesTests
{
    [Fact]
    public void DistancesAndPathsFromOneSource()
    {
        // 0 -> 1 -> 2 -> 3, 0 -> 2; node 4 unreachable.
        var g = TestGraphs.Of(5, 0, 1, 1, 2, 2, 3, 0, 2);
        var d = BfsDistances.Of(g, 0);
        Assert.Equal(0, d.DistanceOf(0));
        Assert.Equal(1, d.DistanceOf(1));
        Assert.Equal(1, d.DistanceOf(2));
        Assert.Equal(2, d.DistanceOf(3));
        Assert.Equal(-1, d.DistanceOf(4));
        Assert.True(d.IsReachable(3));
        Assert.False(d.IsReachable(4));
        Assert.Equal([0, 2, 3], d.PathTo(3).ToArray());
        Assert.Equal([0], d.PathTo(0).ToArray());
        Assert.Empty(d.PathTo(4));
        Assert.Equal(2, d.MaxDistance);
    }

    [Fact]
    public void SeveralSourcesGiveTheDistanceToTheNearest()
    {
        // Chain 0 -> 1 -> 2 -> 3 -> 4, sources 0 and 3.
        var d = BfsDistances.Of(TestGraphs.Path(5), [0, 3, 3]);
        Assert.Equal(2, d.DistanceOf(2));
        Assert.Equal(1, d.DistanceOf(4));
        Assert.Equal([3, 4], d.PathTo(4).ToArray());
        Assert.Equal(2, d.MaxDistance);
    }

    [Fact]
    public void CyclesAndSelfLoopsAreHarmless()
    {
        var d = BfsDistances.Of(TestGraphs.Of(3, 0, 0, 0, 1, 1, 2, 2, 0), 1);
        Assert.Equal(2, d.DistanceOf(0));
        Assert.Equal([1, 2, 0], d.PathTo(0).ToArray());
    }

    [Fact]
    public void EmptySourcesAndEmptyGraph()
    {
        var d = BfsDistances.Of(TestGraphs.Of(2, 0, 1), ReadOnlySpan<int>.Empty);
        Assert.False(d.IsReachable(0));
        Assert.Equal(-1, d.MaxDistance);
        Assert.Equal(-1, BfsDistances.Of(TestGraphs.Empty(), ReadOnlySpan<int>.Empty).MaxDistance);
    }

    [Fact]
    public void OutOfRangeArgumentsAreRejected()
    {
        var g = TestGraphs.Path(3);
        Assert.Equal("source", Assert.Throws<ArgumentOutOfRangeException>(() => BfsDistances.Of(g, 3)).ParamName);
        Assert.Equal("sources", Assert.Throws<ArgumentOutOfRangeException>(() => BfsDistances.Of(g, [0, -1])).ParamName);
        var d = BfsDistances.Of(g, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => d.DistanceOf(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => d.IsReachable(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => d.PathTo(3));
    }

    // ---- Graphs.ReachableFrom ----

    [Fact]
    public void ReachableFromReturnsTheSortedReachableSet()
    {
        var g = TestGraphs.Of(6, 3, 1, 1, 5, 5, 3, 0, 2);
        Assert.Equal([1, 3, 5], Graphs.ReachableFrom(g, 3).ToArray());
        Assert.Equal([0, 2, 4], Graphs.ReachableFrom(g, [4, 0, 4]).ToArray());
        Assert.Empty(Graphs.ReachableFrom(g, ReadOnlySpan<int>.Empty).ToArray());
        Assert.Empty(Graphs.ReachableFrom(TestGraphs.Empty(), ReadOnlySpan<int>.Empty).ToArray());
    }

    [Fact]
    public void ReachableFromExtractsASubgraph()
    {
        // Two functions in one graph: 0 -> 1 -> 2 and 3 -> 4.
        var g = TestGraphs.Of(5, 0, 1, 1, 2, 3, 4);
        var function = Graphs.Remapped(g, NodeMapping.ForSubgraph(Graphs.ReachableFrom(g, 3)));
        Assert.Equal(2, function.NodeCount);
        Assert.True(function.HasOutNode(0, 1));
    }

    [Fact]
    public void ReachableFromRejectsOutOfRangeSources()
    {
        var g = TestGraphs.Path(3);
        Assert.Equal("source", Assert.Throws<ArgumentOutOfRangeException>(() => Graphs.ReachableFrom(g, 3)).ParamName);
        Assert.Equal("sources", Assert.Throws<ArgumentOutOfRangeException>(() => Graphs.ReachableFrom(g, [-1])).ParamName);
    }
}
