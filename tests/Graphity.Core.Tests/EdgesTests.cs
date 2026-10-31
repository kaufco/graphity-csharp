using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Graphity.Core.Tests;

/// <summary>
/// The three lists of <see cref="Edges"/> are <see cref="ImmutableArray{T}"/>s; Java's
/// "all lists are unmodifiable" test has no counterpart, as the type offers no mutator.
/// </summary>
public sealed class EdgesTests
{
    [Fact]
    public void ConstructorStoresAllThreeLists()
    {
        int[] src = [0, 1, 2];
        int[] tgt = [3, 4, 5];
        int[] idx = [6, 7, 8];
        var edges = new Edges(ImmutableCollectionsMarshal.AsImmutableArray(src),
            ImmutableCollectionsMarshal.AsImmutableArray(tgt), ImmutableCollectionsMarshal.AsImmutableArray(idx));
        Assert.Same(src, ImmutableCollectionsMarshal.AsArray(edges.SourceNodes));
        Assert.Same(tgt, ImmutableCollectionsMarshal.AsArray(edges.TargetNodes));
        Assert.Same(idx, ImmutableCollectionsMarshal.AsArray(edges.EdgeIndices));
    }

    [Fact]
    public void WrapSharesTheArrays()
    {
        int[] src = [0, 1];
        var edges = Edges.Wrap(src, [2, 3], [0, 1]);
        Assert.Same(src, ImmutableCollectionsMarshal.AsArray(edges.SourceNodes));
        Assert.Equal([2, 3], edges.TargetNodes.ToArray());
        Assert.Equal([0, 1], edges.EdgeIndices.ToArray());
    }

    [Fact]
    public void CountReturnsNumberOfEdges() =>
        Assert.Equal(3, Edges.Wrap([0, 1, 2], [3, 4, 5], [0, 1, 2]).Count);

    [Fact]
    public void IsEmptyOnEmptyListsTrue()
    {
        var edges = Edges.Wrap([], [], []);
        Assert.True(edges.IsEmpty);
        Assert.Equal(0, edges.Count);
    }

    [Fact]
    public void IsEmptyOnNonEmptyListsFalse()
    {
        var edges = Edges.Wrap([0], [1], [0]);
        Assert.False(edges.IsEmpty);
        Assert.Equal(1, edges.Count);
    }

    [Fact]
    public void EmptyInstanceIsEmpty()
    {
        Assert.True(Edges.Empty.IsEmpty);
        Assert.Equal(0, Edges.Empty.Count);
        Assert.Empty(Edges.Empty.SourceNodes);
        Assert.Empty(Edges.Empty.TargetNodes);
        Assert.Empty(Edges.Empty.EdgeIndices);
        Assert.False(Edges.Empty.SourceNodes.IsDefault);
    }

    [Fact]
    public void EmptyInstanceIsCached() => Assert.Same(Edges.Empty, Edges.Empty);

    [Fact]
    public void OfCollectsOneTypeInEdgeIndexOrder()
    {
        // 0 -> 1, 1 -> 2, 2 -> 0: one back edge, 2 -> 0, edge index 2.
        var back = Edges.Of(EdgeTypes.Of(TestGraphs.Cycle(3)), EdgeType.Back);
        Assert.Equal([2], back.SourceNodes.ToArray());
        Assert.Equal([0], back.TargetNodes.ToArray());
        Assert.Equal([2], back.EdgeIndices.ToArray());
        Assert.True(Edges.Of(EdgeTypes.Of(TestGraphs.Cycle(3)), EdgeType.Cross).IsEmpty);
    }

    [Fact]
    public void OfRejectsAnUndefinedEdgeType()
    {
        var types = EdgeTypes.Of(TestGraphs.Cycle(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Edges.Of(types, (EdgeType)4));
    }
}
