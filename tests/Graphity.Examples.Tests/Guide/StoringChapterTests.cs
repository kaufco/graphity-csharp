using Examples.Guide;

namespace Examples.Tests.Guide;

/// <summary>The examples of the chapter "Storing and comparing graphs".</summary>
public sealed class StoringChapterTests
{
    [Fact]
    public void RoundTrip()
    {
        DirectoryInfo dir = Directory.CreateTempSubdirectory();
        try
        {
            StoringAndComparing.Stored stored = StoringAndComparing.RoundTrip(Path.Combine(dir.FullName, "deps.bin"));
            Assert.True(stored.Same);
            Assert.Equal(100, stored.Size);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Hashes()
    {
        StoringAndComparing.HashesResult hashes = StoringAndComparing.Hashes();
        Assert.Equal(SmallProgram.Dependencies(), hashes.Rebuilt);
        Assert.True(hashes.SameForEqualGraphs);
        Assert.True(hashes.DifferentForNextVersion);
    }

    [Fact]
    public void Diff()
    {
        GraphDiff diff = StoringAndComparing.Diff();
        string[] names = SmallProgram.PackageNames();
        Assert.Equal(["ui -> storage"], OrderChapterTests.NamesOf(diff.AddedEdges, names));
        Assert.Equal(6, diff.AddedEdges.EdgeIndices[0]);
        Assert.Equal(["storage -> model"], OrderChapterTests.NamesOf(diff.RemovedEdges, names));
        Assert.Equal(10, diff.RemovedEdges.EdgeIndices[0]);
        Assert.False(diff.IsEmpty);
    }
}
