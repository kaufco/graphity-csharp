using Examples.Guide;

namespace Examples.Tests.Guide;

/// <summary>The examples of the chapter "Reachability and distances".</summary>
public sealed class ReachabilityChapterTests
{
    [Fact]
    public void ReachableFromLoadAndFromLoadOrSave()
    {
        FindingReachable.ForwardResult forward = FindingReachable.Forward();
        Assert.Equal("[3, 6, 8]", forward.FromLoad.ToString());
        Assert.Equal("[3, 4, 6, 7, 8, 9]", forward.FromLoadOrSave.ToString());
    }

    [Fact]
    public void MethodsThatMayEndUpCallingFormat()
    {
        Assert.Equal("[0, 2, 4, 5, 7]", FindingReachable.CallersOfFormat().ToString());
    }

    [Fact]
    public void ShortestChainFromMainToWrite()
    {
        ShortestPaths.FromMainResult fromMain = ShortestPaths.FromMain();
        Assert.Equal(3, fromMain.DistanceToWrite);
        Assert.Equal([0, 2, 4, 9], fromMain.PathToWrite.AsSpan().ToArray());
        Assert.True(fromMain.ReachesParseArgs);
    }

    [Fact]
    public void EveryMethodIsAtMostThreeCallsAwayFromMain()
    {
        Assert.Equal(3, ShortestPaths.FarthestCall());
    }

    [Fact]
    public void NearestOfLoadAndSave()
    {
        ShortestPaths.FromTwoResult fromTwo = ShortestPaths.FromTwo();
        Assert.Equal(1, fromTwo.DistanceToFormat);
        Assert.Equal([4, 7], fromTwo.PathToFormat.AsSpan().ToArray());
        Assert.False(fromTwo.ReachesMain);
        // The picture after: the reached methods and the edges of the shortest paths to them.
        BfsDistances fromEither = fromTwo.FromEither;
        string[] names = SmallProgram.MethodNames();
        Assert.Equal([3, 6], fromEither.PathTo(SmallProgram.NodeOf(names, "parse")).AsSpan().ToArray());
        Assert.Equal([3, 8], fromEither.PathTo(SmallProgram.NodeOf(names, "read")).AsSpan().ToArray());
        Assert.Equal([4, 9], fromEither.PathTo(SmallProgram.NodeOf(names, "write")).AsSpan().ToArray());
        Assert.Equal([3, 4, 6, 7, 8, 9], Enumerable.Range(0, names.Length).Where(fromEither.IsReachable));
    }

    [Fact]
    public void ClosureOfTheCalls()
    {
        TransitiveClosure.Read read = TransitiveClosure.Closure();
        Assert.Equal(23, read.EdgeCount);
        Assert.True(read.MainReachesWrite);
        Assert.Equal("[3, 4, 5, 6, 7, 8, 9]", read.FromRun.ToString());
        Assert.True(read.ParseOnCycle);
        Assert.False(read.LoadOnCycle);
        // The picture after: the calls and the edges the closure adds.
        Assert.Equal("""
            0 -> [1, 2, 3, 4, 5, 6, 7, 8, 9]
            1 -> []
            2 -> [3, 4, 5, 6, 7, 8, 9]
            3 -> [6, 8]
            4 -> [7, 9]
            5 -> [7]
            6 -> [6]
            7 -> [7]
            8 -> []
            9 -> []

            """, Graphs.AdjacencyListOf(read.Closure));
    }

    [Fact]
    public void ReductionOfTheStepsLeavesOutCompileToPackage()
    {
        TransitiveReduction.Reduced reduced = TransitiveReduction.Reduction();
        Assert.Equal(9, reduced.EdgeCount);
        Assert.Equal([1], reduced.LeftOut.SourceNodes.AsSpan().ToArray());
        Assert.Equal([6], reduced.LeftOut.TargetNodes.AsSpan().ToArray());
        Assert.Equal([3], reduced.LeftOut.EdgeIndices.AsSpan().ToArray());
        string[] names = SmallProgram.StepNames();
        int compile = SmallProgram.NodeOf(names, "compile");
        int pkg = SmallProgram.NodeOf(names, "package");
        Assert.False(reduced.Reduction.HasOutNode(compile, pkg));
        Assert.True(SmallProgram.BuildSteps().HasOutNode(compile, pkg));
        // The picture after: the steps without compile -> package.
        string steps = Graphs.AdjacencyListOf(SmallProgram.BuildSteps());
        Assert.Equal(
            steps.Replace("1 -> [3, 4, 6]", "1 -> [3, 4]", StringComparison.Ordinal),
            Graphs.AdjacencyListOf(reduced.Reduction));
    }
}
