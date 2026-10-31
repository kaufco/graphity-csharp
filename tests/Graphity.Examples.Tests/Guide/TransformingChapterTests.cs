using System.Text;
using Examples.Guide;

namespace Examples.Tests.Guide;

/// <summary>The examples of the chapter "Transforming graphs and carrying data along".</summary>
public sealed class TransformingChapterTests
{
    [Fact]
    public void Subgraphs()
    {
        TransformingGraphs.SubgraphResult subgraph = TransformingGraphs.Subgraph();
        Assert.Equal("""
            0 -> [1, 2]
            1 -> [1]
            2 -> []

            """, Graphs.AdjacencyListOf(subgraph.Loading));
        Assert.Equal(["load", "parse", "read"], subgraph.LoadingNames);
        Assert.Equal("""
            0 -> [2, 3]
            1 -> [2]
            2 -> [2]
            3 -> []

            """, Graphs.AdjacencyListOf(subgraph.SavingAndShowing));
    }

    [Fact]
    public void Renumbered()
    {
        TransformingGraphs.RenumberedResult renumbered = TransformingGraphs.Renumbered();
        var order = new StringBuilder();
        for (int i = 0; i < renumbered.SearchOrder.IndexCount; i++)
        {
            order.Append(renumbered.SearchOrder.Get(i)).Append(' ');
        }
        Assert.Equal("0 1 2 3 6 8 4 7 9 5 ", order.ToString());
        Assert.Equal("""
            0 -> [1, 2]
            1 -> []
            2 -> [3, 6, 9]
            3 -> [4, 5]
            4 -> [4]
            5 -> []
            6 -> [7, 8]
            7 -> [7]
            8 -> []
            9 -> [7]

            """, Graphs.AdjacencyListOf(renumbered.InSearchOrder));
        var runOrder = new StringBuilder();
        for (int i = 0; i < renumbered.RunOrder.IndexCount; i++)
        {
            runOrder.Append(renumbered.RunOrder.Get(i)).Append(' ');
        }
        Assert.Equal("2 0 1 4 3 5 6 7 8 ", runOrder.ToString());
        Assert.StartsWith("""
            0 -> [6]
            1 -> [2]
            2 -> [3, 4, 6]
            3 -> [8]
            4 -> [5]
            5 -> [6]
            6 -> [7]

            """, Graphs.AdjacencyListOf(renumbered.InRunOrder), StringComparison.Ordinal);
        Assert.True(Graphs.IsDag(renumbered.InRunOrder));
    }

    [Fact]
    public void NodeData()
    {
        TransformingGraphs.NodeDataResult data = TransformingGraphs.NodeData();
        Assert.Equal([25, 60, 10], data.LoadingLines);
        Assert.Equal(1, data.NewIdOfParse);
        Assert.False(data.MainKept);
        Assert.True(data.Partial);
        Assert.Equal(3, data.MappedCount);
        Assert.Equal(10, data.IndexCount);
        Assert.Equal(2, data.SecondToOriginal.IndexCount);
        Assert.Equal(6, data.SecondToOriginal.Get(0));
        Assert.Equal(8, data.SecondToOriginal.Get(1));
        Assert.Equal([60, 10], data.CalleeLines);
    }

    [Fact]
    public void EdgeData()
    {
        Assert.Equal([1, 1, 2], TransformingGraphs.EdgeData());
    }

    [Fact]
    public void Transposed()
    {
        TransformingGraphs.TransposedResult transposed = TransformingGraphs.Transposed();
        Assert.Equal("""
            0 -> []
            1 -> [0]
            2 -> [0]
            3 -> [2]
            4 -> [2]
            5 -> [2]
            6 -> [3, 6]
            7 -> [4, 5, 7]
            8 -> [3]
            9 -> [4]

            """, Graphs.AdjacencyListOf(transposed.CalledBy));
        Assert.Equal(10, transposed.TransposedIndexOfLoadToRead);
        Assert.Equal(["save: 1", "show: 1", "format: 1"], transposed.CallersOfFormat);
    }

    [Fact]
    public void OwnGroups()
    {
        TransformingGraphs.OwnGroupsResult own = TransformingGraphs.OwnGroups();
        Assert.Equal(8, own.GroupCount);
        Assert.Equal(2, own.SizeOfService);
        Assert.Equal("[3, 4]", own.MethodsOfService.ToString());
        Assert.Equal("""
            0 -> [1, 2, 3]
            1 -> []
            2 -> [4]
            3 -> [4, 5]
            4 -> []
            5 -> []
            6 -> []
            7 -> []

            """, Graphs.AdjacencyListOf(own.PackageCalls));
    }

    [Fact]
    public void Combined()
    {
        TransformingGraphs.CombinedResult combined = TransformingGraphs.Combined();
        Assert.Equal([32, 30, 15, 43, 100, 20, 0, 0], combined.LinesPerPackage);
        Assert.Equal("[5, 7]", combined.Behind.ToString());
        Assert.Equal([1, 2, 2, 1, 2, 2], combined.SitesPerPackageCall);
    }
}
