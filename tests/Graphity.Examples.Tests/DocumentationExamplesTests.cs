namespace Examples.Tests;

/// <summary>
/// Checks that every C# example in the API documentation is a region of the examples, as Java's
/// Javadoc embeds them with <c>{@snippet}</c>, so that it compiles and does what its comments say:
/// the XML documentation of the packages, their overviews (<c>PACKAGE.md</c>) and the pages of the
/// API documentation site (<c>docs/</c>). A marker <c>&lt;!-- example: File.cs#region --&gt;</c>
/// directly before a code block names the region, which the block equals without its indentation.
/// The XML documentation escapes <c>&lt;</c> and <c>&amp;</c> in the block, and the comparison
/// undoes it; a Markdown block may start with using directives of the named files.
/// </summary>
public sealed class DocumentationExamplesTests
{
    /// <summary>The packages with code, whose XML documentation and overviews show examples.</summary>
    private static readonly string[] Packages =
        ["Graphity.Core", "Graphity.ControlFlow", "Graphity.Dot", "Graphity.Mermaid"];

    [Fact]
    public void EveryCSharpBlockOfTheXmlDocumentationIsARegionOfTheExamples()
    {
        var failures = new List<string>();
        int count = 0;
        foreach (string package in Packages)
        {
            string dir = Path.Combine(Sources.Root, "src", package);
            foreach (string file in Directory.GetFiles(dir, "*.cs").Order(StringComparer.Ordinal))
            {
                string name = $"src/{package}/{Path.GetFileName(file)}";
                foreach (ExampleBlock block in Regions.XmlDocumentationBlocks(Sources.Read(name), name, failures))
                {
                    count++;
                    failures.AddRange(Regions.Check(block, withUsings: false));
                }
            }
        }
        Assert.True(count > 0);
        Regions.AssertNone(failures);
    }

    [Fact]
    public void EveryCSharpBlockOfThePackageOverviewsIsARegionOfTheExamples()
    {
        var failures = new List<string>();
        int count = 0;
        foreach (string package in Packages.Append("Graphity.All"))
        {
            string name = $"src/{package}/PACKAGE.md";
            foreach (ExampleBlock block in Regions.MarkdownBlocks(Sources.Read(name), name))
            {
                count++;
                failures.AddRange(Regions.Check(block, withUsings: true));
            }
        }
        Assert.True(count > 0);
        Regions.AssertNone(failures);
    }

    [Fact]
    public void EveryCSharpBlockOfTheApiDocumentationPagesIsARegionOfTheExamples()
    {
        var failures = new List<string>();
        int count = 0;
        // The sources of the site; DocFX writes the site to docs/api/ and its metadata to docs/obj/.
        string docs = Path.Combine(Sources.Root, "docs");
        foreach (string file in Directory.GetFiles(docs, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string name = Path.GetRelativePath(Sources.Root, file).Replace('\\', '/');
            if (name.StartsWith("docs/api/", StringComparison.Ordinal) || name.StartsWith("docs/obj/", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (ExampleBlock block in Regions.MarkdownBlocks(Sources.Read(name), name))
            {
                count++;
                failures.AddRange(Regions.Check(block, withUsings: true));
            }
        }
        Assert.True(count > 0);
        Regions.AssertNone(failures);
    }
}
