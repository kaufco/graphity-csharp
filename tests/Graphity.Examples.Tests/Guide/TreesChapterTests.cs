using Examples.Guide;

namespace Examples.Tests.Guide;

/// <summary>The examples of the chapter "Trees and hierarchies".</summary>
public sealed class TreesChapterTests
{
    [Fact]
    public void Shapes()
    {
        CheckingTrees.ShapesResult shapes = CheckingTrees.Shapes();
        Assert.True(shapes.FamilyTree);
        Assert.False(shapes.FamilyPath);
        Assert.Equal("""
            0 -> [1]
            1 -> [2]
            2 -> [3]
            3 -> []

            """, Graphs.AdjacencyListOf(shapes.Line));
        Assert.True(shapes.LineTree);
        Assert.True(shapes.LinePath);
        Assert.False(shapes.DependenciesForest);
    }

    [Fact]
    public void ParentsAndGenerations()
    {
        ReadingAncestry.ParentsResult parents = ReadingAncestry.Parents();
        Assert.Equal(3, parents.ParentOfGreta);
        Assert.Equal(3, parents.GenerationOfGreta);
        Assert.Equal(-1, parents.ParentOfAda);
        Assert.Equal([0, 1, 1, 2, 2, 2, 3, 3, 3, 3], parents.Generations);
    }

    [Fact]
    public void LowestCommonAncestors()
    {
        ReadingAncestry.CommonResult common = ReadingAncestry.Common();
        Assert.Equal(0, common.GretaAndIda);
        Assert.Equal(1, common.GretaAndEmil);
        Assert.Equal(3, common.HugoAndGreta);
        Assert.Equal(3, common.DoraAndHugo);
    }

    [Fact]
    public void AncestorTests()
    {
        ReadingAncestry.TestsResult tests = ReadingAncestry.Tests();
        Assert.True(tests.BenIsAncestor);
        Assert.False(tests.CleoIsAncestor);
        Assert.True(tests.HugoIsAncestor);
        Assert.True(tests.FastBenIsAncestor);
    }

    [Fact]
    public void Subtrees()
    {
        ReadingAncestry.SubtreesResult subtrees = ReadingAncestry.Subtrees();
        Assert.Equal([1, 3, 6, 7, 4], subtrees.SubtreeOfBen);
        Assert.Equal(5, subtrees.SubtreeSizeOfBen);
        Assert.Equal([0, 1, 3, 6, 7, 4, 2, 5, 8, 9], subtrees.Preorder.AsSpan().ToArray());
        Assert.Equal(6, subtrees.PositionOfCleo);
    }
}
