namespace Examples.Tests;

/// <summary>Pins the graphs of the example domain, on which every example comment depends.</summary>
public sealed class SmallProgramTests
{
    [Fact]
    public void DependenciesHaveOneCycleBetweenModelAndStorage()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        Assert.Equal(["app", "cli", "ui", "service", "model", "storage", "json", "util"], names);
        Assert.Equal(names.Length, dependencies.NodeCount);
        Assert.Equal(14, dependencies.EdgeCount);
        Assert.Equal("""
            0 -> [1, 2, 3]
            1 -> [7]
            2 -> [3, 4, 7]
            3 -> [4, 5]
            4 -> [5]
            5 -> [4, 6, 7]
            6 -> [7]
            7 -> []

            """, Graphs.AdjacencyListOf(dependencies));

        Assert.Equal([["model", "storage"]], CyclesOf(dependencies, names));
    }

    [Fact]
    public void CallsMergeTheCallSitesAndHaveTwoRecursiveMethods()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        Assert.Equal(["main", "parseArgs", "run", "load", "save", "show", "parse", "format", "read", "write"], names);
        Assert.Equal(names.Length, calls.NodeCount);
        Assert.Equal(12, calls.EdgeCount);
        Assert.Equal(14, SmallProgram.CallSites().Count);
        Assert.Equal("""
            0 -> [1, 2]
            1 -> []
            2 -> [3, 4, 5]
            3 -> [6, 8]
            4 -> [7, 9]
            5 -> [7]
            6 -> [6]
            7 -> [7]
            8 -> []
            9 -> []

            """, Graphs.AdjacencyListOf(calls));
        foreach (CallSite site in SmallProgram.CallSites())
        {
            Assert.True(calls.HasOutNode(site.Caller, site.Callee));
        }
        Assert.Equivalent(new[] { new[] { "parse" }, ["format"] }, CyclesOf(calls, names), strict: true);
    }

    [Fact]
    public void CfgIsALoopWithABranchFromEntryToExit()
    {
        Graph cfg = SmallProgram.Cfg();
        string[] names = SmallProgram.BlockNames();
        Assert.Equal(["entry", "header", "body", "then", "next", "exit"], names);
        Assert.Equal(names.Length, cfg.NodeCount);
        Assert.Equal("""
            0 -> [1]
            1 -> [2, 5]
            2 -> [3, 4]
            3 -> [4]
            4 -> [1]
            5 -> []

            """, Graphs.AdjacencyListOf(cfg));
        Assert.Equal(["entry"], Names.Of(Graphs.RootNodes(BiAdjacentGraph.Of(cfg)), names));
        Assert.Equal([["header", "body", "then", "next"]], CyclesOf(cfg, names));
    }

    [Fact]
    public void PackageOfPutsEveryCallBetweenTwoPackagesOnADependency()
    {
        Graph calls = SmallProgram.Calls();
        Graph dependencies = SmallProgram.Dependencies();
        string[] packageNames = SmallProgram.PackageNames();
        int[] packageOf = SmallProgram.PackageOf();
        Assert.Equal(calls.NodeCount, packageOf.Length);
        Assert.Equal(
            ["app", "cli", "app", "service", "service", "ui", "model", "model", "storage", "storage"],
            Names.Of(packageOf, packageNames));
        calls.ForEachEdge((caller, callee, edge) =>
        {
            if (packageOf[caller] != packageOf[callee])
            {
                Assert.True(dependencies.HasOutNode(packageOf[caller], packageOf[callee]));
            }
        });
    }

    // The strongly connected components that contain a cycle, each as the names of its nodes.
    private static List<string[]> CyclesOf(Graph graph, string[] names)
    {
        Sccs sccs = Components.SccsOf(graph);
        var cycles = new List<string[]>();
        for (int scc = 0; scc < sccs.SccCount; scc++)
        {
            if (sccs.ContainsCycle(scc))
            {
                cycles.Add(Names.Of(sccs.NodesOf(scc), names));
            }
        }
        return cycles;
    }

    [Fact]
    public void NamesReturnCopiesAndLookUpNodes()
    {
        string[] names = SmallProgram.PackageNames();
        names[0] = "changed";
        Assert.Equal("app", SmallProgram.PackageNames()[0]);
        Assert.Equal(5, SmallProgram.NodeOf(SmallProgram.BlockNames(), "exit"));
        Assert.Throws<ArgumentException>(() => SmallProgram.NodeOf(SmallProgram.BlockNames(), "loop"));
    }
}
