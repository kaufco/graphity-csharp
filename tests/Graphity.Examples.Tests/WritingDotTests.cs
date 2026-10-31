namespace Examples.Tests;

public sealed class WritingDotTests
{
    private static readonly string[] PackageNames = SmallProgram.PackageNames();

    [Fact]
    public void TheCycleIsModelAndStorageWithTheirNodeIds()
    {
        Assert.Equal("model", PackageNames[4]);
        Assert.Equal("storage", PackageNames[5]);
        Assert.Equal("""
            digraph {
              4 [label="model"];
              5 [label="storage"];
              4 -> 5;
              5 -> 4;
            }

            """, WritingDot.Cycle());
    }

    [Fact]
    public void OnlyModelAndStorageAreRed()
    {
        string dot = WritingDot.CycleInRed();
        Assert.Contains("  4 [label=\"model\", color=\"red\"];\n", dot, StringComparison.Ordinal);
        Assert.Contains("  5 [label=\"storage\", color=\"red\"];\n", dot, StringComparison.Ordinal);
        Assert.Equal(2, Names.LinesOf(dot).Count(line => line.Contains("color", StringComparison.Ordinal)));
    }
}
