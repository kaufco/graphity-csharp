namespace Graphity.Core.Tests;

/// <remarks>
/// Java's <c>viewsAreReadOnly</c> (an <c>UnsupportedOperationException</c> on <c>set</c> and
/// <c>removeInt</c>) has no counterpart beyond the types: <see cref="PreorderAncestry.Preorder"/> is an
/// <see cref="System.Collections.Immutable.ImmutableArray{T}"/> and <see cref="PreorderAncestry.SubtreeOf"/>
/// a <see cref="ReadOnlySpan{T}"/>, neither with a mutator; <c>SubtreeIsAViewIntoThePreorder</c> checks
/// that the subtree is a view, not a copy.
/// </remarks>
public sealed class PreorderAncestryTests
{
    /// <summary>
    /// Same fixture as <see cref="AncestryTests"/>, a tree of depth 3:
    /// <code>
    ///        0
    ///      /   \
    ///     1     2
    ///    / \     \
    ///   3   4     5
    ///      /
    ///     6
    /// </code>
    /// Preorder: 0, 1, 3, 4, 6, 2, 5.
    /// </summary>
    private static PreorderAncestry Tree() =>
        PreorderAncestry.Of(Ancestry.Of(TestGraphs.Of(7, 0, 1, 0, 2, 1, 3, 1, 4, 2, 5, 4, 6)));

    [Fact]
    public void PreorderVisitsChildrenInAscendingOrder() =>
        Assert.Equal([0, 1, 3, 4, 6, 2, 5], Tree().Preorder.ToArray());

    [Fact]
    public void PreorderIndexIsThePositionInPreorder()
    {
        var a = Tree();
        for (int i = 0; i < 7; i++)
        {
            Assert.Equal(i, a.PreorderIndexOf(a.Preorder[i]));
        }
    }

    [Fact]
    public void SubtreeSizeCountsTheNodeAndAllDescendants()
    {
        var a = Tree();
        Assert.Equal(7, a.SubtreeSizeOf(0));
        Assert.Equal(4, a.SubtreeSizeOf(1));
        Assert.Equal(2, a.SubtreeSizeOf(4));
        Assert.Equal(1, a.SubtreeSizeOf(5));
    }

    [Fact]
    public void SubtreeStartsWithTheNodeAndFollowsPreorder()
    {
        var a = Tree();
        Assert.Equal([1, 3, 4, 6], a.SubtreeOf(1).ToArray());
        Assert.Equal([2, 5], a.SubtreeOf(2).ToArray());
        Assert.Equal([6], a.SubtreeOf(6).ToArray());
    }

    [Fact]
    public void IsAncestorOfIsAnIntervalTest()
    {
        var a = Tree();
        Assert.True(a.IsAncestorOf(0, 6));
        Assert.True(a.IsAncestorOf(1, 6));
        Assert.True(a.IsAncestorOf(4, 6));
        Assert.True(a.IsAncestorOf(6, 6));
        Assert.False(a.IsAncestorOf(2, 6));
        Assert.False(a.IsAncestorOf(6, 4));
        Assert.False(a.IsAncestorOf(3, 4));
    }

    [Fact]
    public void InheritedQueriesStillWork()
    {
        var a = Tree();
        Assert.Equal(1, a.LowestCommonAncestorOf(3, 6));
        Assert.Equal(3, a.DepthOf(6));
        Assert.Equal(4, a.ParentOf(6));
    }

    [Fact]
    public void IsAncestorOfThroughTheBaseTypeUsesTheIntervalTest()
    {
        Ancestry a = Tree();
        Assert.True(a.IsAncestorOf(1, 6));
        Assert.False(a.IsAncestorOf(2, 6));
    }

    [Fact]
    public void ForestRootsComeInAscendingOrder()
    {
        // Two trees, 3 -> {0, 4} and 1 -> 2; the roots are 1 and 3.
        var a = PreorderAncestry.Of(Ancestry.Of(TestGraphs.Of(5, 3, 0, 3, 4, 1, 2)));
        Assert.Equal([1, 2, 3, 0, 4], a.Preorder.ToArray());
        Assert.False(a.IsAncestorOf(1, 4));
        Assert.True(a.IsAncestorOf(3, 4));
    }

    [Fact]
    public void IsolatedNodesAreSingletonTrees()
    {
        var a = PreorderAncestry.Of(Ancestry.Of(TestGraphs.Isolated(3)));
        Assert.Equal([0, 1, 2], a.Preorder.ToArray());
        Assert.Equal(1, a.SubtreeSizeOf(1));
        Assert.False(a.IsAncestorOf(0, 1));
    }

    [Fact]
    public void EmptyForest() => Assert.Empty(PreorderAncestry.Of(Ancestry.Of(TestGraphs.Empty())).Preorder);

    [Fact]
    public void OfAPreorderAncestryReturnsItUnchanged()
    {
        var a = Tree();
        Assert.Same(a, PreorderAncestry.Of(a));
    }

    // Java's viewsAreReadOnly (UnsupportedOperationException on set/removeInt) has no counterpart:
    // Preorder is an ImmutableArray<int> and SubtreeOf a ReadOnlySpan<int>; neither has mutators. What
    // remains to check is that the subtree view shares the preorder instead of copying it.
    [Fact]
    public void SubtreeIsAViewIntoThePreorder()
    {
        var a = Tree();
        ReadOnlySpan<int> subtree = a.SubtreeOf(1);
        Assert.True(a.Preorder.AsSpan().Overlaps(subtree, out int offset));
        Assert.Equal(a.PreorderIndexOf(1), offset);
    }

    [Fact]
    public void OutOfRangeNodesAreRejected()
    {
        var a = Tree();
        Assert.Throws<ArgumentOutOfRangeException>(() => a.IsAncestorOf(7, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => a.IsAncestorOf(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => a.SubtreeSizeOf(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => a.SubtreeOf(-1).Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => a.PreorderIndexOf(7));
    }
}
