namespace Examples.Tests;

public sealed class TraversalsTests
{
    [Fact]
    public void LeaveNodeGivesTheCalleesBeforeTheirCallers()
    {
        Assert.Equal([1, 6, 8, 3, 7, 9, 4, 5, 2, 0], Traversals.CalleesFirstFromMain());
        Assert.Equal(
            ["parseArgs", "parse", "read", "load", "format", "write", "save", "show", "run", "main"],
            Names.Of(Traversals.CalleesFirstFromMain(), SmallProgram.MethodNames()));
    }

    [Fact]
    public void VisitEdgeStopsAtTheDependencyOfStorageOnModel()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        int edge = Traversals.FirstCycleEdge();
        Assert.Equal(10, edge);
        Assert.Equal("storage", names[dependencies.EdgeSource(edge)]);
        Assert.Equal("model", names[dependencies.EdgeTarget(edge)]);
    }

    [Fact]
    public void CurrentLevelStopsTwoCallsAwayFromMain()
    {
        Assert.Equal([0, 1, 2, 3, 4, 5], Traversals.WithinTwoCallsOfMain());
        Assert.Equal(
            ["main", "parseArgs", "run", "load", "save", "show"],
            Names.Of(Traversals.WithinTwoCallsOfMain(), SmallProgram.MethodNames()));
    }

    [Fact]
    public void EdgeTypesFindTheBackEdgeOfTheLoopAndTheForwardEdgePastThen()
    {
        string[] names = SmallProgram.BlockNames();
        Classified classified = Traversals.EdgeTypesOfLoad();
        Assert.Equal(EdgeType.Back, classified.LoopEdge);
        Edges forward = classified.ForwardEdges;
        Assert.Equal(1, forward.Count);
        Assert.Equal("body", names[forward.SourceNodes[0]]);
        Assert.Equal("next", names[forward.TargetNodes[0]]);
    }

    [Fact]
    public void PathToGivesTheShortestCallChainFromMainToParse()
    {
        Chain chain = Traversals.CallChainToParse();
        Assert.Equal(3, chain.Depth);
        Assert.Equal([0, 2, 3, 6], chain.Path.AsSpan().ToArray());
        Assert.Equal(["main", "run", "load", "parse"], Names.Of(chain.Path, SmallProgram.MethodNames()));
    }
}
