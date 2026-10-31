namespace Examples.Tests;

/// <summary>
/// Checks that every C# example in the README is a region of the examples, so that it compiles and
/// does what its comments say. A marker <c>&lt;!-- example: File.cs#region --&gt;</c> names the
/// region, or several separated by spaces, directly before the code block. The using directives at
/// the top of a block must be using directives of the named files; the rest equals the regions,
/// without their common indentation, separated by a blank line.
/// </summary>
public sealed class ReadmeExamplesTests
{
    [Fact]
    public void EveryCSharpBlockOfTheReadmeIsARegionOfTheExamples()
    {
        List<ExampleBlock> blocks = Regions.MarkdownBlocks(Sources.Read("README.md"), "README.md");
        Assert.NotEmpty(blocks);
        Regions.AssertNone([.. blocks.SelectMany(block => Regions.Check(block, withUsings: true))]);
    }
}
