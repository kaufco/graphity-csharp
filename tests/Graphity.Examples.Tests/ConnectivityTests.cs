namespace Examples.Tests;

public sealed class ConnectivityTests
{
    [Fact]
    public void SccsOfGivesThePackagesInBuildOrder()
    {
        List<List<string>> expected =
            [["util"], ["cli"], ["json"], ["model", "storage"], ["service"], ["ui"], ["app"]];
        Assert.Equal(expected, Connectivity.BuildOrder());
    }

    [Fact]
    public void BiconnectivityOfLoadHasTheEdgesOutsideTheLoopAsBridgesAndHeaderAsArticulationPoint()
    {
        string[] names = SmallProgram.BlockNames();
        Splitters splitters = Connectivity.BridgesOfLoad();
        Assert.Equal(["entry -> header", "header -> exit"], Names.EdgesOf(splitters.Edges, names));
        Assert.Equal("[1]", splitters.Nodes.ToString());
        Assert.Equal(["header"], Names.Of(splitters.Nodes, names));
    }

    [Fact]
    public void StrongConnectivityOfLoadSparesOnlyTheBranchThroughThen()
    {
        string[] names = SmallProgram.BlockNames();
        Splitters splitters = Connectivity.StrongBridgesOfLoad();
        Assert.Equal(
            ["header -> body", "body -> then", "then -> next", "next -> header"],
            Names.EdgesOf(splitters.Edges, names));
        Assert.Equal("[1, 2, 4]", splitters.Nodes.ToString());
        Assert.Equal(["header", "body", "next"], Names.Of(splitters.Nodes, names));
    }
}
