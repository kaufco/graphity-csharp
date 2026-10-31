using Examples.Guide;

namespace Examples.Tests.Guide;

/// <summary>The examples of the chapter "Connectivity when direction does not matter".</summary>
public sealed class UndirectedChapterTests
{
    [Fact]
    public void WithoutMainAndRunTheCallsFallIntoFiveComponents()
    {
        FindingParts.WccsResult wccs = FindingParts.Wccs();
        Assert.Equal("""
            0 -> []
            1 -> []
            2 -> []
            3 -> [6, 8]
            4 -> [7, 9]
            5 -> [7]
            6 -> [6]
            7 -> [7]
            8 -> []
            9 -> []

            """, Graphs.AdjacencyListOf(wccs.Rest));
        Assert.Equal(5, wccs.ComponentCount);
        Assert.Equal(
            [["main"], ["parseArgs"], ["run"], ["load", "parse", "read"], ["save", "show", "format", "write"]],
            wccs.Components);
        Assert.True(wccs.CallsConnected);
        Assert.False(wccs.RestConnected);
    }

    [Fact]
    public void BridgesAndArticulationPointsOfTheCalls()
    {
        FindingBridges.SplittersResult splitters = FindingBridges.Splitters();
        Assert.Equal(
            ["main -> parseArgs", "main -> run", "run -> load", "load -> parse", "load -> read", "save -> write"],
            OrderChapterTests.NamesOf(splitters.Bridges, SmallProgram.MethodNames()));
        Assert.Equal("[0, 2, 3, 4]", splitters.ArticulationPoints.ToString());
    }

    [Fact]
    public void BlocksOfTheCalls()
    {
        FindingBridges.BlocksResult blocks = FindingBridges.Blocks();
        Assert.Equal(7, blocks.BlockCount);
        Assert.Equal(["run -> save", "run -> show", "save -> format", "show -> format"], blocks.LargeBlock);
        Assert.Equal(["parse -> parse", "format -> format"], blocks.InNoBlock);
    }

    [Fact]
    public void Bipartite()
    {
        CheckingUndirectedShapes.BipartiteResult bipartite = CheckingUndirectedShapes.Bipartite();
        Assert.True(bipartite.Family);
        Assert.False(bipartite.Dependencies);
    }

    [Fact]
    public void TreesAndForests()
    {
        CheckingUndirectedShapes.ShapesResult shapes = CheckingUndirectedShapes.Shapes();
        Assert.True(shapes.FamilyTree);
        Assert.True(shapes.RestForest);
        Assert.False(shapes.RestTree);
        Assert.False(shapes.CallsForest);
    }
}
