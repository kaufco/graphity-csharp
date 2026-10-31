namespace Examples.Tests;

public sealed class DominanceTests
{
    [Fact]
    public void BodyIsTheImmediateDominatorOfNextAndDominatesItWhileThenDoesNot()
    {
        string[] names = SmallProgram.BlockNames();
        DominatorsOfNext result = Dominance.DominatorsOfNext();
        Assert.Equal(2, result.Idom);
        Assert.Equal("body", names[result.Idom]);
        Assert.True(result.BodyDominatesNext);
        Assert.False(result.ThenDominatesNext);
    }

    [Fact]
    public void NextIsTheImmediatePostDominatorOfBody()
    {
        Assert.Equal(4, Dominance.PostDominatorOfBody());
        Assert.Equal("next", SmallProgram.BlockNames()[Dominance.PostDominatorOfBody()]);
    }

    [Fact]
    public void BodyDominatesItselfThenAndNext()
    {
        Assert.Equal([2, 3, 4], Dominance.DominatedByBody());
        Assert.Equal(["body", "then", "next"], Names.Of(Dominance.DominatedByBody(), SmallProgram.BlockNames()));
    }
}
