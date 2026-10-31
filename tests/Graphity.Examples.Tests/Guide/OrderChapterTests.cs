using Examples.Guide;

namespace Examples.Tests.Guide;

/// <summary>The examples of the chapter "Order and cycles".</summary>
public sealed class OrderChapterTests
{
    [Fact]
    public void ThePackagesHaveTheCycleOfModelAndStorage()
    {
        FindingCycles.Found found = FindingCycles.Cycles();
        Assert.False(found.DependenciesAcyclic);
        Assert.Equal([4, 5], found.Cycle.AsSpan().ToArray());
    }

    [Fact]
    public void TopologicalOrderOfTheSteps()
    {
        Assert.Equal(
            ["lint", "generate", "compile", "docs", "compileTests", "test", "package", "sign", "publish"],
            SortingTopologically.RunOrder());
        Assert.Throws<ArgumentException>(() => TopOrder.Of(SmallProgram.Dependencies()));
    }

    [Fact]
    public void StagesOfTheSteps()
    {
        FindingLevels.StagesResult stages = FindingLevels.Stages();
        Assert.Equal(3, stages.LevelOfTest);
        Assert.Equal(7, stages.LevelCount);
        Assert.Equal(
            [
                ["generate", "lint"], ["compile"], ["compileTests", "docs"], ["test"], ["package"], ["sign"],
                ["publish"],
            ],
            stages.Stages);
        // The text: no path connects docs (4), on level 2, and test (5), on level 3.
        Assert.False(Graphs.ReachableFrom(SmallProgram.BuildSteps(), 4).Contains(5));
        Assert.False(Graphs.ReachableFrom(SmallProgram.BuildSteps(), 5).Contains(4));
    }

    [Fact]
    public void RootsOfTheSteps()
    {
        FindingRoots.RootsResult roots = FindingRoots.Roots();
        Assert.Equal("[0, 2]", roots.Roots.ToString());
        Assert.False(roots.CompileIsRoot);
    }

    [Fact]
    public void EdgeTypesOfThePackages()
    {
        ClassifyingEdges.Classified classified = ClassifyingEdges.Classify();
        Assert.Equal(EdgeType.Back, classified.StorageToModel);
        Assert.Equal(EdgeType.Forward, classified.AppToService);
        Assert.Equal(
            ["ui -> util", "storage -> util", "json -> util"],
            NamesOf(classified.CrossEdges, SmallProgram.PackageNames()));
        // The picture after: the type of every edge, by edge index from 0 to 13.
        EdgeTypes types = classified.Types;
        Assert.Equal(14, SmallProgram.Dependencies().EdgeCount);
        Assert.Equal(
            [
                EdgeType.Tree, EdgeType.Tree, EdgeType.Forward, EdgeType.Tree, EdgeType.Tree, EdgeType.Forward,
                EdgeType.Cross, EdgeType.Tree, EdgeType.Forward, EdgeType.Tree, EdgeType.Back, EdgeType.Tree,
                EdgeType.Cross, EdgeType.Cross,
            ],
            Enumerable.Range(0, 14).Select(types.TypeOf));
        string[] names = SmallProgram.PackageNames();
        Assert.Equal(
            [
                "app -> cli", "app -> ui", "cli -> util", "ui -> service", "service -> model",
                "model -> storage", "storage -> json",
            ],
            NamesOf(Edges.Of(types, EdgeType.Tree), names));
        Assert.Equal(
            ["app -> service", "ui -> model", "service -> storage"],
            NamesOf(Edges.Of(types, EdgeType.Forward), names));
        Assert.Equal(["storage -> model"], NamesOf(Edges.Of(types, EdgeType.Back), names));
    }

    [Fact]
    public void WithoutBackEdgesThePackagesAreADag()
    {
        BreakingCycles.Broken broken = BreakingCycles.WithoutBackEdges();
        Assert.Equal(["storage -> model"], NamesOf(broken.BackEdges, SmallProgram.PackageNames()));
        Assert.Equal(10, broken.BackEdges.EdgeIndices[0]);
        Assert.True(broken.IsDag);
        Assert.Equal(13, broken.Acyclic.EdgeCount);
        Assert.True(Graphs.IsDag(broken.Acyclic));
    }

    /// <summary>The edges of an edge list as <c>source -> target</c>, by the names of the nodes.</summary>
    internal static List<string> NamesOf(Edges edges, string[] names)
    {
        var result = new List<string>();
        for (int p = 0; p < edges.Count; p++)
        {
            result.Add($"{names[edges.SourceNodes[p]]} -> {names[edges.TargetNodes[p]]}");
        }
        return result;
    }
}
