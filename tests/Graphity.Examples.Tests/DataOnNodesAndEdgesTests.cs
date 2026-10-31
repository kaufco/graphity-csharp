namespace Examples.Tests;

public sealed class DataOnNodesAndEdgesTests
{
    private static readonly CallData Data = DataOnNodesAndEdges.CallData();

    [Fact]
    public void MethodsHoldTheNameAndThePackageOfEachMethodByNodeId()
    {
        string[] packageNames = SmallProgram.PackageNames();
        int[] packageOf = SmallProgram.PackageOf();
        Assert.Equal(SmallProgram.MethodNames(), Data.Names);
        Assert.Equal(packageOf.Length, Data.Packages.Length);
        for (int node = 0; node < packageOf.Length; node++)
        {
            Assert.True(packageNames[packageOf[node]] == Data.Packages[node], Data.Names[node]);
        }
    }

    [Fact]
    public void SiteCountIsTwoForRunToShowAndParseToParse()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        Assert.Equal(calls.EdgeCount, Data.SiteCount.Length);
        calls.ForEachEdge((caller, callee, edge) =>
        {
            string call = $"{names[caller]} -> {names[callee]}";
            int expected = call is "run -> show" or "parse -> parse" ? 2 : 1;
            Assert.True(expected == Data.SiteCount[edge], call);
        });
    }

    [Fact]
    public void TheSubgraphOfLoadTakesTheMethodsAndTheCallSitesAlong()
    {
        Graph loading = Data.Loading;
        string[] names = Data.LoadingNames;
        Assert.Equal(["load", "parse", "read"], names);
        Assert.Equal(["load -> parse", "load -> read", "parse -> parse"], Names.EdgesByName(loading, names));
        Assert.Equal(loading.EdgeCount, Data.LoadingSiteCount.Length);
        var counted = new List<string>();
        loading.ForEachEdge((caller, callee, edge) =>
            counted.Add($"{names[caller]} -> {names[callee]}: {Data.LoadingSiteCount[edge]}"));
        Assert.Equivalent(new[] { "load -> parse: 1", "load -> read: 1", "parse -> parse: 2" }, counted, strict: true);
    }
}
