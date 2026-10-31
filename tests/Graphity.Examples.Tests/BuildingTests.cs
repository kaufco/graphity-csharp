namespace Examples.Tests;

public sealed class BuildingTests
{
    [Fact]
    public void OfBuildsTheControlFlowOfLoad() => Assert.Equal(SmallProgram.Cfg(), Building.CfgFromArrays());

    [Fact]
    public void ToStringGivesTheTextInTheComment() =>
        Assert.Equal(
            "Graph[nodes=6, edges=7] {0 -> [1], 1 -> [2, 5], 2 -> [3, 4], 3 -> [4], 4 -> [1], 5 -> []}",
            Building.CfgAsText());
}
