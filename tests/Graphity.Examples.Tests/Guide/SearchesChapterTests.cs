using Examples.Guide;

namespace Examples.Tests.Guide;

/// <summary>The examples of the chapter "Writing searches of your own".</summary>
public sealed class SearchesChapterTests
{
    [Fact]
    public void DescendantsBottomUp()
    {
        Assert.Equal([9, 4, 3, 2, 0, 2, 0, 0, 0, 0], WritingSearches.CountDescendants());
    }

    [Fact]
    public void WithoutStorage()
    {
        WritingSearches.WithoutStorageResult search = WritingSearches.WithoutStorage();
        Assert.Equal(["main", "parseArgs", "run", "load", "parse", "save", "format", "show"], search.Reached);
        Assert.Equal(["parse -> parse", "format -> format"], search.Recursive);
        // The pictures: the grey frame of the package storage holds read and write.
        string[] names = SmallProgram.MethodNames();
        int[] packageOf = SmallProgram.PackageOf();
        int storage = SmallProgram.NodeOf(SmallProgram.PackageNames(), "storage");
        Assert.Equal(
            ["read", "write"],
            Enumerable.Range(0, names.Length).Where(method => packageOf[method] == storage).Select(method => names[method]));
    }

    [Fact]
    public void ByDepth()
    {
        Assert.Equal(
            [["main"], ["parseArgs", "run"], ["load", "save", "show"], ["parse", "read", "format", "write"]],
            WritingSearches.MethodsByDepth());
    }

    [Fact]
    public void FromArrays()
    {
        WritingSearches.FromArraysResult fromArrays = WritingSearches.FromArrays();
        Assert.Equal([2, 0, 4, 2, 2, 1, 2, 1, 0, 0], fromArrays.SitesIn);
        Assert.Equal([0, 1, 1, 1, 1, 1, 2, 3, 1, 1], fromArrays.CallerCount);
        Assert.Equal([0, 1, 1, 1, 1, 2, 1, 2, 1, 1], fromArrays.SitesFromOthers);
    }
}
