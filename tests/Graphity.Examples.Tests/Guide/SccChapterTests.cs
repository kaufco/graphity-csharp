using Examples.Guide;

namespace Examples.Tests.Guide;

/// <summary>The examples of the chapter "Strongly connected components" added after the pilot.</summary>
public sealed class SccChapterTests
{
    [Fact]
    public void BuildOrderOfThePackages()
    {
        OrderingDespiteCycles.Order order = OrderingDespiteCycles.BuildOrder();
        Assert.Equal(
            [["util"], ["cli"], ["json"], ["model", "storage"], ["service"], ["ui"], ["app"]],
            order.BuildOrder);
        Assert.True(order.JsonBeforeUi);
    }

    [Fact]
    public void TheControlFlowOfLoadIsStronglyConnectedOnlyWithTheEdgeFromExitToEntry()
    {
        CheckingStrongConnectivity.Answers answers = CheckingStrongConnectivity.Check();
        Assert.False(answers.CfgStronglyConnected);
        Assert.True(answers.LoadWellFormed);
    }

    [Fact]
    public void StrongBridgesAndArticulationPointsOfLoad()
    {
        SplittingGroups.Splitters splitters = SplittingGroups.OfLoad();
        Assert.Equal(
            ["header -> body", "body -> then", "then -> next", "next -> header"],
            OrderChapterTests.NamesOf(splitters.StrongBridges, SmallProgram.BlockNames()));
        Assert.Equal("[1, 2, 4]", splitters.StrongArticulationPoints.ToString());
    }
}
