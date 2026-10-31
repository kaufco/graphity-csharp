using Examples.Guide;

namespace Examples.Tests.Guide;

/// <summary>The examples of the chapter "Building and reading graphs".</summary>
public sealed class BuildingChapterTests
{
    [Fact]
    public void FromEdgesBuildsThePackagesOfTheSmallProgramAndMergesTheDuplicate()
    {
        BuildingFromEdges.Built built = BuildingFromEdges.Dependencies();
        Assert.Equal(8, built.NodeCount);
        Assert.Equal(14, built.EdgeCount);
        Assert.Equal(SmallProgram.Dependencies(), built.Dependencies);
    }

    [Fact]
    public void FromObjectsBuildsTheSameGraphAndKeepsThePackageOfEachNode()
    {
        BuildingFromObjects.Built built = BuildingFromObjects.FromObjects();
        Assert.Equal(SmallProgram.Dependencies(), built.Dependencies);
        Assert.Equal("storage", built.PackageOf[5].Name);
        Assert.Equal("ui", built.UsedByApp);
        // The comment: an unknown name gives -1, which the builder rejects.
        var builder = new GraphBuilder();
        builder.AddNodes(8);
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddEdge(0, -1));
    }

    [Fact]
    public void AndNodesAddsTheNodesUpToTheHighestNodeId()
    {
        Graph graph = BuildingFromObjects.FromNumberedInput();
        Assert.Equal(8, graph.NodeCount);
        Assert.Equal("""
            0 -> [3]
            1 -> []
            2 -> []
            3 -> [4]
            4 -> [0]
            5 -> []
            6 -> []
            7 -> [3]

            """, Graphs.AdjacencyListOf(graph));
    }

    [Fact]
    public void AddChildBuildsTheFamilyTreeOfTheDomain()
    {
        Assert.Equal(SmallFamily.Tree(), BuildingTreesAndPaths.FamilyTree());
    }

    [Fact]
    public void PathsAndBatchesBuildTheStepsOfTheDomain()
    {
        Graph steps = BuildingTreesAndPaths.Steps();
        Assert.Equal("""
            0 -> [1]
            1 -> [3, 4, 6]
            2 -> [6]
            3 -> [5]
            4 -> [8]
            5 -> [6]
            6 -> [7]
            7 -> [8]
            8 -> []

            """, Graphs.AdjacencyListOf(steps));
        Assert.Equal(SmallProgram.BuildSteps(), steps);
    }

    [Fact]
    public void AddGraphPutsTheLibraryAfterTheProgram()
    {
        CombiningGraphs.Combined combined = CombiningGraphs.ProgramWithLibrary();
        Assert.Equal(0, combined.ProgramOffset);
        Assert.Equal(8, combined.LibraryOffset);
        Assert.Equal(11, combined.Graph.NodeCount);
        Assert.Equal(17, combined.Graph.EdgeCount);
        Assert.Equal("[7, 8]", combined.Graph.OutNodes(6).ToString());
        Assert.Equal("[9, 10]", combined.Graph.OutNodes(8).ToString());
        // The picture after: the program as it was, the library after it, and json -> api.
        string program = Graphs.AdjacencyListOf(SmallProgram.Dependencies());
        Assert.Equal(
            program.Replace("6 -> [7]", "6 -> [7, 8]", StringComparison.Ordinal) + "8 -> [9, 10]\n9 -> []\n10 -> []\n",
            Graphs.AdjacencyListOf(combined.Graph));
    }

    [Fact]
    public void AddGraphExtendsAGraph()
    {
        Graph next = CombiningGraphs.NextVersion();
        Assert.Equal(8, next.NodeCount);
        Assert.Equal(15, next.EdgeCount);
        Assert.True(next.HasOutNode(2, 5));
        // The text: ui -> storage gets the edge index 6, and ui -> util moves up from 6 to 7.
        Assert.Equal(6, next.EdgeIndexOf(2, 5));
        Assert.Equal(6, SmallProgram.Dependencies().EdgeIndexOf(2, 7));
        Assert.Equal(7, next.EdgeIndexOf(2, 7));
    }

    [Fact]
    public void DataOnNodesAndEdgesGivesTheCallsBetweenPackages()
    {
        KeepingData.Data data = KeepingData.CallData();
        Assert.Equal(10, data.PackageOf.Length);
        Assert.Equal(14, data.SiteCount.Sum());
        Assert.Equal(
            [
                "main -> parseArgs: 1", "run -> load: 1", "run -> save: 1", "run -> show: 2",
                "load -> parse: 1", "load -> read: 1", "save -> format: 1", "save -> write: 1",
                "show -> format: 1",
            ],
            data.Lines);
    }

    [Fact]
    public void OutNodesOfRun()
    {
        FindingNeighbors.Out @out = FindingNeighbors.OutNodes();
        Assert.Equal("[3, 4, 5]", @out.CalledByRun.ToString());
        Assert.Equal(3, @out.OutDegree);
        Assert.True(@out.RunCallsSave);
        Assert.True(@out.Recursive);
    }

    [Fact]
    public void InNodesOfFormat()
    {
        FindingNeighbors.In @in = FindingNeighbors.InNodes();
        Assert.Equal("[4, 5, 7]", @in.CallersOfFormat.ToString());
        Assert.Equal(3, @in.InDegree);
        Assert.True(@in.SaveCallsFormat);
        Assert.Equal("[2, 7, 9]", @in.NeighborsOfSave.ToString());
        Assert.Equal(3, @in.DegreeOfSave);
        Assert.True(@in.MainIsRoot);
        // The text: degree counts edges, not neighbors - format has 3 neighbors but degree 4.
        BiAdjacentGraph both = BiAdjacentGraph.Of(SmallProgram.Calls());
        Assert.Equal(3, both.Neighbors(7).Count);
        Assert.Equal(4, both.Degree(7));
    }

    [Fact]
    public void SortedLists()
    {
        ReadingResultLists.Sorted sorted = ReadingResultLists.SortedLists();
        Assert.Equal(3, sorted.Count);
        Assert.Equal(3, sorted.First);
        Assert.Equal(5, sorted.Last);
        Assert.Equal(1, sorted.Position);
        Assert.False(sorted.HasFormat);
        Assert.Equal("[7, 9]", sorted.Both.ToString());
        Assert.Equal([3, 4, 5], sorted.Copy);
    }

    [Fact]
    public void EdgeLists()
    {
        Assert.Equal(["parse -> parse (edge 10)", "format -> format (edge 11)"], ReadingResultLists.EdgeLists());
    }

    [Fact]
    public void CsrArraysMakeTheControlFlowOfLoad()
    {
        UsingCsrArrays.FromArrays fromArrays = UsingCsrArrays.Load();
        Assert.Equal(6, fromArrays.Graph.NodeCount);
        Assert.Equal(7, fromArrays.Graph.EdgeCount);
        Assert.Equal(SmallProgram.Cfg(), fromArrays.Graph);
        Assert.Equal([0, 1, 3, 5, 6, 7, 7], fromArrays.Start);
        Assert.Equal([1, 2, 5, 3, 4, 4, 1], fromArrays.Adjacency);
    }
}
