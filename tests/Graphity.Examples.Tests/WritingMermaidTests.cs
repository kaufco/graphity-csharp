namespace Examples.Tests;

public sealed class WritingMermaidTests
{
    private static readonly string[] PackageNames = SmallProgram.PackageNames();

    [Fact]
    public void TheCycleIsModelAndStorageWithTheirNodeIds()
    {
        Assert.Equal("model", PackageNames[4]);
        Assert.Equal("storage", PackageNames[5]);
        Assert.Equal("""
            flowchart TD
              n4["model"]
              n5["storage"]
              n4 --> n5
              n5 --> n4

            """, WritingMermaid.Cycle());
    }

    [Fact]
    public void OnlyModelAndStorageAreInTheRedClass()
    {
        string mermaid = WritingMermaid.CycleInRed();
        Assert.Contains("  classDef cycle stroke:red\n", mermaid, StringComparison.Ordinal);
        Assert.Contains("  class n4 cycle\n", mermaid, StringComparison.Ordinal);
        Assert.Contains("  class n5 cycle\n", mermaid, StringComparison.Ordinal);
        Assert.Equal(2, Names.LinesOf(mermaid).Count(line => line.StartsWith("  class ", StringComparison.Ordinal)));
    }
}
