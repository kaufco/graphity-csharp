namespace Graphity.Core.Tests;

/// <summary>
/// SCCs and condensation, WCCs, edge types, reindexing, topological orders and levels, the hook
/// order of <see cref="DfsTraverser"/> and the binary format against the results of the Java
/// library, on the fixtures in <c>Conformance/</c>.
/// </summary>
public sealed class ConformanceTests
{
    public static IEnumerable<object[]> Cases() => Fixtures.Cases();

    [Fact]
    public void CoversTheFullFixtureSet() => Assert.True(Cases().Count() >= 28);

    [Theory]
    [MemberData(nameof(Cases))]
    public void SccsAndCondensationMatchJava(FixtureCase c)
    {
        var graph = Fixtures.Graph($"{c.Name}.gphy");
        var sccs = Components.SccsOf(graph);
        Assert.Equal(c.SccCount, sccs.SccCount);
        Assert.Equal(Fixtures.Ints(c, "scc.i32"), sccs.AsSpan().ToArray());
        Assert.Equal(Fixtures.Graph($"{c.Name}.condensed.gphy"), sccs.Condense());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void WccsMatchJava(FixtureCase c)
    {
        var wccs = Components.WccsOf(Fixtures.Graph($"{c.Name}.gphy"));
        Assert.Equal(c.WccCount, wccs.GroupCount);
        Assert.Equal(Fixtures.Ints(c, "wcc.i32"), wccs.AsSpan().ToArray());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EdgeTypesMatchJava(FixtureCase c)
    {
        var types = EdgeTypes.Of(Fixtures.Graph($"{c.Name}.gphy"));
        Assert.Equal(Fixtures.Bytes($"{c.Name}.edgetypes.u8"), types.AsBytes().ToArray());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ReindexMatchesJavaAndRemapsLosslessly(FixtureCase c)
    {
        var graph = Fixtures.Graph($"{c.Name}.gphy");
        var mapping = NodeMapping.Reindex(graph);
        Assert.Equal(Fixtures.Ints(c, "reindex.i32"), mapping.AsSpan().ToArray());
        var back = Graphs.Remapped(Graphs.Remapped(graph, mapping), mapping.Inverted(graph.NodeCount));
        Assert.Equal(graph, back);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void OrdersAndLevelsMatchJava(FixtureCase c)
    {
        var graph = Fixtures.Graph($"{c.Name}.gphy");
        Assert.Equal(c.IsDag, Graphs.IsDag(graph));
        if (!c.IsDag)
        {
            Assert.Throws<ArgumentException>(() => TopOrder.Of(graph));
            return;
        }
        Assert.Equal(Fixtures.Ints(c, "toporder.i32"), TopOrder.Of(graph));
        foreach (var g in new[] { graph, BiAdjacentGraph.Of(graph) })
        {
            var tol = TopOrderAndLevels.Of(g);
            Assert.Equal(Fixtures.Ints(c, "kahnorder.i32"), tol.Order);
            Assert.Equal(Fixtures.Ints(c, "levels.i32"), tol.Levels);
            Assert.Equal(c.LevelCount, tol.LevelCount);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TraverserHookOrderMatchesJava(FixtureCase c)
    {
        var trace = new Trace(Fixtures.Graph($"{c.Name}.gphy"));
        trace.Run();
        Assert.Equal(Fixtures.Ints(c, "dfspre.i32"), trace.Pre);
        Assert.Equal(Fixtures.Ints(c, "dfspost.i32"), trace.Post);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void FormatAndBuilderReproduceTheGraph(FixtureCase c)
    {
        var graph = Fixtures.Graph($"{c.Name}.gphy");
        using var output = new MemoryStream();
        graph.WriteTo(output);
        Assert.Equal(Fixtures.Bytes($"{c.Name}.gphy"), output.ToArray());

        var builder = new GraphBuilder();
        builder.AddNodes(graph.NodeCount);
        graph.ForEachEdge((s, t, _) => builder.AddEdge(t, s));
        var transposed = builder.Build();
        var bi = BiAdjacentGraph.Of(graph);
        for (int v = 0; v < graph.NodeCount; v++)
        {
            Assert.True(bi.InNodes(v) == transposed.OutNodes(v), $"in-nodes of {v}");
        }
        Assert.Equal(Graph.ReadFrom(new MemoryStream(output.ToArray())), graph);
    }

    private sealed class Trace(Graph graph) : DfsTraverser(graph)
    {
        public List<int> Pre { get; } = [];
        public List<int> Post { get; } = [];

        protected override bool EnterNode(int node)
        {
            Pre.Add(node);
            return true;
        }

        protected override void LeaveNode(int node) => Post.Add(node);
    }
}
