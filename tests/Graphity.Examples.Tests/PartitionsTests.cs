namespace Examples.Tests;

public sealed class PartitionsTests
{
    [Fact]
    public void OfCondensesTheCallsToTheCallsBetweenPackages()
    {
        Graph packageCalls = Partitions.PackageCalls();
        string[] packageNames = SmallProgram.PackageNames();
        Assert.Equal(packageNames.Length, packageCalls.NodeCount);
        Assert.Equal(
            [
                "app -> cli", "app -> ui", "app -> service",
                "ui -> model",
                "service -> model", "service -> storage",
            ],
            Names.EdgesByName(packageCalls, packageNames));
    }

    [Fact]
    public void EdgesOfListsTheCallsFromServiceToModel()
    {
        string[] names = SmallProgram.MethodNames();
        Graph calls = SmallProgram.Calls();
        Assert.Equal("[5, 7]", Partitions.CallsFromServiceToModel().ToString());
        var behind = new List<string>();
        foreach (int edge in Partitions.CallsFromServiceToModel())
        {
            behind.Add($"{names[calls.EdgeSource(edge)]} -> {names[calls.EdgeTarget(edge)]}");
        }
        Assert.Equal(["load -> parse", "save -> format"], behind);
    }

    [Fact]
    public void ReduceSumsTheCallSitesPerCallBetweenPackages()
    {
        Graph packageCalls = Partitions.PackageCalls();
        string[] packageNames = SmallProgram.PackageNames();
        int[] packageSiteCount = Partitions.CallSitesPerPackageCall();
        Assert.Equal(packageCalls.EdgeCount, packageSiteCount.Length);
        var counted = new List<string>();
        packageCalls.ForEachEdge((source, target, edge) =>
            counted.Add($"{packageNames[source]} -> {packageNames[target]}: {packageSiteCount[edge]}"));
        Assert.Equivalent(
            new[]
            {
                "app -> ui: 2", "app -> service: 2", "service -> model: 2", "service -> storage: 2",
                "app -> cli: 1", "ui -> model: 1",
            },
            counted,
            strict: true);
    }
}
