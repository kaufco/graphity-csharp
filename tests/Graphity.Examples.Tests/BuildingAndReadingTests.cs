namespace Examples.Tests;

public sealed class BuildingAndReadingTests
{
    [Fact]
    public void BuildMergesTheCallSitesOfRunToShowAndOfParseToParse()
    {
        string[] names = SmallProgram.MethodNames();
        IReadOnlyList<CallSite> callSites = SmallProgram.CallSites();
        Graph calls = BuildingAndReading.CallsFromCallSites();

        Assert.Equal(SmallProgram.Calls(), calls);
        Assert.Equal(names.Length, calls.NodeCount);
        Assert.Equal(14, callSites.Count);
        Assert.Equal(12, calls.EdgeCount);

        var timesAdded = callSites
            .GroupBy(site => $"{names[site.Caller]} -> {names[site.Callee]}")
            .Where(group => group.Count() != 1)
            .ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(
            new Dictionary<string, int> { ["run -> show"] = 2, ["parse -> parse"] = 2 },
            timesAdded);
    }

    [Fact]
    public void AddGraphKeepsTheNodeIdsAndTheEdgeFromUiToStorageIsAdded()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        int ui = SmallProgram.NodeOf(names, "ui");
        int storage = SmallProgram.NodeOf(names, "storage");
        Graph nextVersion = BuildingAndReading.NextVersion();

        Assert.False(dependencies.HasOutNode(ui, storage));
        Assert.Equal(dependencies.NodeCount, nextVersion.NodeCount);
        Assert.Equal(dependencies.EdgeCount + 1, nextVersion.EdgeCount);
        dependencies.ForEachEdge((source, target, edge) => Assert.True(nextVersion.HasOutNode(source, target)));
        Assert.True(nextVersion.HasOutNode(ui, storage));
    }

    [Fact]
    public void LoopCountsTheCallersOfEachMethod()
    {
        string[] names = SmallProgram.MethodNames();
        int[] callerCount = BuildingAndReading.CallerCounts();

        Assert.Equal(names.Length, callerCount.Length);
        for (int method = 0; method < names.Length; method++)
        {
            int expected = names[method] switch
            {
                "format" => 3,
                "parse" => 2,
                "main" => 0,
                _ => 1,
            };
            Assert.True(expected == callerCount[method], names[method]);
        }
    }

    [Fact]
    public void EdgesWriteTheRecursiveCallsWithTheirCallSites() =>
        Assert.Equal(
            ["parse -> parse: 2", "format -> format: 1"],
            Names.LinesWrittenBy(BuildingAndReading.RecursiveCalls));
}
