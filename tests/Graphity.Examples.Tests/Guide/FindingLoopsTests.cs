using Examples.Guide;

namespace Examples.Tests.Guide;

public sealed class FindingLoopsTests
{
    [Fact]
    public void TheLoopOverTheLinesLiesInTheLoopOverTheNotes()
    {
        FindingLoops.Nesting nesting = FindingLoops.LoopsOfShow();
        Assert.Equal(2, nesting.LoopCount);
        Assert.Equal(0, nesting.NotesLoop);
        Assert.Equal(1, nesting.LinesLoop);
        Assert.Equal(3, nesting.Header);
        Assert.Equal([1, 2, 3, 4], nesting.Body);
        Assert.Equal(0, nesting.Parent);
        Assert.Equal(2, nesting.Depth);
    }
}
