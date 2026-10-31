using Examples.Guide;

namespace Examples.Tests.Guide;

/// <summary>The examples of the chapter "Dominance".</summary>
public sealed class DominanceChapterTests
{
    [Fact]
    public void ImmediateDominators()
    {
        FindingDominators.ImmediateResult immediate = FindingDominators.Immediate();
        Assert.Equal(2, immediate.ImmediateDominatorOfNext);
        Assert.Equal(1, immediate.ImmediateDominatorOfExit);
        Assert.True(immediate.ExitReachable);
        // The green blocks of the picture after: next, body, header, entry.
        Assert.Equal([4, 2, 1, 0], immediate.DominatorsOfNext);
    }

    [Fact]
    public void DominatorTree()
    {
        FindingDominators.TreeResult tree = FindingDominators.Tree();
        Assert.Equal("""
            0 -> [1]
            1 -> [2, 5]
            2 -> [3, 4]
            3 -> []
            4 -> []
            5 -> []

            """, Graphs.AdjacencyListOf(tree.Tree));
        Assert.True(tree.BodyDominatesNext);
        Assert.False(tree.ThenDominatesNext);
        Assert.Equal(2, tree.LowestCommonDominator);
    }

    [Fact]
    public void PostDominators()
    {
        FindingDominators.PostResult post = FindingDominators.Post();
        Assert.Equal(4, post.JoinOfBody);
        Assert.Equal(5, post.JoinOfHeader);
        // header -> entry, header -> next, next -> body, next -> then, exit -> header
        Assert.Equal("""
            0 -> []
            1 -> [0, 4]
            2 -> []
            3 -> []
            4 -> [2, 3]
            5 -> [1]

            """, Graphs.AdjacencyListOf(post.Tree));
    }

    [Fact]
    public void TwoEntries()
    {
        FindingDominators.TwoEntriesResult two = FindingDominators.TwoEntries();
        Assert.Equal(-1, two.ImmediateDominatorOfLoad);
        Assert.Equal(3, two.ImmediateDominatorOfParse);
        Assert.Equal(2, two.ImmediateDominatorOfFormat);
        // main -> parseArgs, main -> run, run -> save, run -> show, run -> format, load -> parse,
        // load -> read, save -> write
        Assert.Equal("""
            0 -> [1, 2]
            1 -> []
            2 -> [4, 5, 7]
            3 -> [6, 8]
            4 -> [9]
            5 -> []
            6 -> []
            7 -> []
            8 -> []
            9 -> []

            """, Graphs.AdjacencyListOf(two.Forest));
    }
}
