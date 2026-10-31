namespace Graphity.Core.Tests;

public sealed class AncestryTests
{
    /// <summary>
    /// Standard fixture, a tree of depth 3:
    /// <code>
    ///        0
    ///      /   \
    ///     1     2
    ///    / \     \
    ///   3   4     5
    ///      /
    ///     6
    /// </code>
    /// </summary>
    private static Ancestry Tree() => Ancestry.Of(TestGraphs.Of(7, 0, 1, 0, 2, 1, 3, 1, 4, 2, 5, 4, 6));

    [Fact]
    public void LowestCommonAncestorOfSiblingsIsTheirParent() =>
        Assert.Equal(1, Tree().LowestCommonAncestorOf(3, 4));

    [Fact]
    public void LowestCommonAncestorAcrossBranchesIsTheRoot()
    {
        var a = Tree();
        Assert.Equal(0, a.LowestCommonAncestorOf(6, 5));
        Assert.Equal(0, a.LowestCommonAncestorOf(3, 2));
    }

    [Fact]
    public void LowestCommonAncestorOfAncestorAndDescendantIsTheAncestor()
    {
        var a = Tree();
        Assert.Equal(1, a.LowestCommonAncestorOf(1, 6));
        Assert.Equal(1, a.LowestCommonAncestorOf(6, 1));
    }

    [Fact]
    public void LowestCommonAncestorOfNodeWithItselfIsTheNode()
    {
        var a = Tree();
        for (int node = 0; node < 7; node++)
        {
            Assert.Equal(node, a.LowestCommonAncestorOf(node, node));
        }
    }

    [Fact]
    public void LowestCommonAncestorIsSymmetric()
    {
        var a = Tree();
        for (int x = 0; x < 7; x++)
        {
            for (int y = 0; y < 7; y++)
            {
                Assert.Equal(a.LowestCommonAncestorOf(y, x), a.LowestCommonAncestorOf(x, y));
            }
        }
    }

    [Fact]
    public void LowestCommonAncestorWithTheRootIsTheRoot()
    {
        var a = Tree();
        for (int node = 0; node < 7; node++)
        {
            Assert.Equal(0, a.LowestCommonAncestorOf(0, node));
        }
    }

    [Fact]
    public void LowestCommonAncestorAcrossTreesIsMinusOne()
    {
        // Two separate trees: 0 -> 1 -> 2 and 3 -> 4.
        var a = Ancestry.Of(TestGraphs.Of(5, 0, 1, 1, 2, 3, 4));
        Assert.Equal(-1, a.LowestCommonAncestorOf(2, 4));
        Assert.Equal(-1, a.LowestCommonAncestorOf(0, 3));
    }

    [Fact]
    public void LowestCommonAncestorOnAPathIsTheShallowerNode()
    {
        var a = Ancestry.Of(TestGraphs.Path(64));
        Assert.Equal(10, a.LowestCommonAncestorOf(10, 40));
        Assert.Equal(0, a.LowestCommonAncestorOf(63, 0));
    }

    [Fact]
    public void LowestCommonAncestorOnAStarIsTheCenter() =>
        Assert.Equal(0, Ancestry.Of(TestGraphs.OutStar(10)).LowestCommonAncestorOf(3, 7));

    [Fact]
    public void IsAncestorOfHoldsAlongTheRootPath()
    {
        var a = Tree();
        Assert.True(a.IsAncestorOf(0, 6));
        Assert.True(a.IsAncestorOf(1, 6));
        Assert.True(a.IsAncestorOf(4, 6));
        Assert.True(a.IsAncestorOf(6, 6));
        Assert.False(a.IsAncestorOf(6, 4));
        Assert.False(a.IsAncestorOf(2, 6));
        Assert.False(a.IsAncestorOf(3, 4));
    }

    [Fact]
    public void IsAncestorOfIsFalseAcrossTrees() =>
        Assert.False(Ancestry.Of(TestGraphs.Of(5, 0, 1, 1, 2, 3, 4)).IsAncestorOf(0, 4));

    [Fact]
    public void DepthOfCountsEdgesFromTheRoot()
    {
        var a = Tree();
        Assert.Equal(0, a.DepthOf(0));
        Assert.Equal(1, a.DepthOf(1));
        Assert.Equal(2, a.DepthOf(5));
        Assert.Equal(3, a.DepthOf(6));
    }

    [Fact]
    public void DepthOfIsZeroForEveryTreeRoot()
    {
        var a = Ancestry.Of(TestGraphs.Of(5, 0, 1, 1, 2, 3, 4));
        Assert.Equal(0, a.DepthOf(0));
        Assert.Equal(0, a.DepthOf(3));
    }

    [Fact]
    public void ParentOfIsMinusOneForEveryTreeRoot()
    {
        var a = Ancestry.Of(TestGraphs.Of(5, 0, 1, 1, 2, 3, 4));
        Assert.Equal(-1, a.ParentOf(0));
        Assert.Equal(-1, a.ParentOf(3));
        Assert.Equal(3, a.ParentOf(4));
    }

    [Fact]
    public void ParentOfReportsTheTreeParent()
    {
        var a = Tree();
        Assert.Equal(4, a.ParentOf(6));
        Assert.Equal(1, a.ParentOf(4));
        Assert.Equal(0, a.ParentOf(1));
    }

    [Fact]
    public void OfAcceptsTheEmptyGraph()
    {
        var a = Ancestry.Of(TestGraphs.Empty());
        Assert.Throws<ArgumentOutOfRangeException>(() => a.DepthOf(0));
    }

    [Fact]
    public void OfAcceptsIsolatedNodes()
    {
        var a = Ancestry.Of(TestGraphs.Isolated(3));
        Assert.Equal(0, a.LowestCommonAncestorOf(0, 0));
        Assert.Equal(-1, a.LowestCommonAncestorOf(0, 1));
        Assert.Equal(0, a.DepthOf(2));
        Assert.Equal(-1, a.ParentOf(2));
    }

    [Fact]
    public void OfRejectsANodeWithTwoParents()
    {
        // Diamond: node 3 has parents 1 and 2.
        var diamond = TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3);
        var e = Assert.Throws<ArgumentException>(() => Ancestry.Of(diamond));
        Assert.Contains("node 3", e.Message);
    }

    [Fact]
    public void OfRejectsACycle()
    {
        var e = Assert.Throws<ArgumentException>(() => Ancestry.Of(TestGraphs.Cycle(4)));
        Assert.Contains("cycle", e.Message);
    }

    [Fact]
    public void OfNamesANodeOnTheCycleAndNotOneBelowIt()
    {
        // Node 0 hangs below the cycle 2 -> 3 -> 2, so it is unreachable from any root as well.
        var cycleWithATail = TestGraphs.Of(4, 2, 0, 2, 3, 3, 2);
        var e = Assert.Throws<ArgumentException>(() => Ancestry.Of(cycleWithATail));
        Assert.Matches(".*node [23] lies on a cycle.*", e.Message);
    }

    [Fact]
    public void OfRejectsASelfLoop()
    {
        // A self-loop makes its node unreachable from any root, just like any other cycle.
        var e = Assert.Throws<ArgumentException>(() => Ancestry.Of(TestGraphs.Of(2, 0, 0, 0, 1)));
        Assert.Contains("cycle", e.Message);
    }

    [Fact]
    public void OfAcceptsAnInTreeForestViaTheTranspose()
    {
        // In-tree: 1 -> 0 and 2 -> 0. Transposed it becomes the out-star rooted at 0.
        var a = Ancestry.Of(BiAdjacentGraph.Of(TestGraphs.InStar(3)).Transposed());
        Assert.Equal(0, a.LowestCommonAncestorOf(1, 2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(1000)]
    public void QueriesOutOfRangeThrow(int node)
    {
        var a = Tree();
        Assert.Throws<ArgumentOutOfRangeException>(() => a.LowestCommonAncestorOf(node, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => a.LowestCommonAncestorOf(0, node));
        Assert.Throws<ArgumentOutOfRangeException>(() => a.IsAncestorOf(node, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => a.DepthOf(node));
        Assert.Throws<ArgumentOutOfRangeException>(() => a.ParentOf(node));
    }
}
