namespace Examples.Tests;

public sealed class AnalyzingControlFlowTests
{
    private static readonly string[] BlockNames = SmallProgram.BlockNames();

    [Fact]
    public void LoopsFindTheForLoopWithItsHeaderAndBody()
    {
        LoopOfBlock found = AnalyzingControlFlow.LoopOfThen();
        Assert.Equal(1, found.Loops.LoopCount); // the only loop
        Assert.Equal(0, found.Loop);
        Assert.Equal(1, found.Header);
        Assert.Equal("header", BlockNames[found.Header]);
        Assert.Equal([1, 2, 3, 4], found.Blocks);
        Assert.Equal(["header", "body", "then", "next"], Names.Of(found.Blocks, BlockNames));
    }

    [Fact]
    public void TheVariableIOfTheLoopNeedsAPhiNodeInTheHeader()
    {
        Assert.Equal("[1]", AnalyzingControlFlow.PhiBlocksOfI().ToString());
        Assert.Equal(["header"], Names.Of(AnalyzingControlFlow.PhiBlocksOfI(), BlockNames));
    }

    [Fact]
    public void TheLoopConditionAndTheIfDecideWhichBlocksRun()
    {
        Graph controlDependence = AnalyzingControlFlow.ControlDependenceOfLoad();
        Assert.Equal(BlockNames.Length, controlDependence.NodeCount);
        for (int block = 0; block < BlockNames.Length; block++)
        {
            string[] decides = Names.Of(controlDependence.OutNodes(block), BlockNames);
            switch (BlockNames[block])
            {
                case "header":
                    Assert.Equal(["header", "body", "next"], decides);
                    break;
                case "body":
                    Assert.Equal(["then"], decides);
                    break;
                default:
                    Assert.Empty(decides);
                    break;
            }
        }
    }
}
