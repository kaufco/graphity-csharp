namespace Examples.Tests;

public sealed class MappingsTests
{
    [Fact]
    public void SubgraphKeepsTheMethodsThatLoadReachesWithTheirNames()
    {
        Loading loading = Mappings.Loading();
        Assert.Equal(["load", "parse", "read"], loading.Names);
        Assert.Equal(
            ["load -> parse", "load -> read", "parse -> parse"],
            Names.EdgesByName(loading.Graph, loading.Names));
    }

    [Fact]
    public void EdgeMappingCarriesTheCallSitesOverToTheSubgraph()
    {
        Loading loading = Mappings.Loading();
        Graph graph = loading.Graph;
        string[] names = loading.Names;
        Assert.Equal(graph.EdgeCount, loading.SiteCount.Length);
        var counted = new List<string>();
        graph.ForEachEdge((caller, callee, edge) =>
            counted.Add($"{names[caller]} -> {names[callee]}: {loading.SiteCount[edge]}"));
        Assert.Equivalent(new[] { "load -> parse: 1", "load -> read: 1", "parse -> parse: 2" }, counted, strict: true);
    }

    [Fact]
    public void TransposedEdgeMappingWritesTheCallersOfParseWithTheirCallSites() =>
        Assert.Equal(["load: 1", "parse: 2"], Names.LinesWrittenBy(Mappings.CallersOfParse));
}
