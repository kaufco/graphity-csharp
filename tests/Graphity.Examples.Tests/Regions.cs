using System.Text.RegularExpressions;

namespace Examples.Tests;

/// <summary>A code block of the documentation with the examples it shows.</summary>
/// <param name="Location">Where the block starts, as <c>file:line</c>.</param>
/// <param name="References">
/// The references of its marker, such as <c>Connectivity.cs#sccsOf</c>, or none without a marker.
/// </param>
/// <param name="Lines">The lines of the block, in the XML documentation with XML's escapes undone.</param>
internal sealed record ExampleBlock(string Location, IReadOnlyList<string> References, IReadOnlyList<string> Lines);

/// <summary>
/// The regions of the examples, as Java's Javadoc tool shows the regions of <c>{@snippet}</c>, and the
/// code blocks of the documentation that show them.
/// </summary>
/// <remarks>
/// A marker <c>&lt;!-- example: File.cs#region --&gt;</c> on the line right before a code block
/// names the region the block shows, or several, separated by spaces. In Markdown, the block is a
/// <c>```csharp</c> fence; in the XML documentation, a <c>&lt;code language="csharp"&gt;</c> element
/// on lines of its own, whose text has <c>&lt;</c> and <c>&amp;</c> escaped as XML requires.
/// </remarks>
internal static partial class Regions
{
    /// <summary>A line with nothing but the start or the end of a region.</summary>
    [GeneratedRegex("""^\s*// @(start|end)(?: region="([^"]*)")?\s*$""")]
    private static partial Regex Markup();

    [GeneratedRegex("""^\s*<!-- example: (.*?) -->\s*$""")]
    private static partial Regex Marker();

    // A line of an XML documentation comment, with what follows the slashes.
    [GeneratedRegex("""^\s*///(.*)$""")]
    private static partial Regex DocLine();

    // A using directive, of a namespace, a static class or an alias.
    [GeneratedRegex("""^using [\w.= ]+;$""")]
    private static partial Regex Using();

    /// <summary>
    /// The lines of a region: from <c>// @start region="name"</c> to the <c>// @end</c> that closes
    /// it, without the markup lines of regions nested in it and without their common indentation.
    /// <c>// @end</c> closes the region started last, <c>// @end region="name"</c> the region of that
    /// name.
    /// </summary>
    /// <returns>The region, or <see langword="null"/> if the source has none of that name.</returns>
    internal static string? Region(string source, string name)
    {
        var open = new List<string>();
        var lines = new List<string>();
        foreach (string line in source.Split('\n'))
        {
            Match markup = Markup().Match(line);
            if (!markup.Success)
            {
                if (open.Contains(name))
                {
                    lines.Add(line);
                }
                continue;
            }
            string? region = markup.Groups[2].Success ? markup.Groups[2].Value : null;
            if (markup.Groups[1].Value == "start")
            {
                open.Add(region ?? "");
            }
            else
            {
                string closed = region ?? open[^1];
                open.RemoveAt(open.LastIndexOf(closed));
                if (closed == name)
                {
                    return Dedent(string.Join("\n", lines));
                }
            }
        }
        return null;
    }

    /// <summary>
    /// <paramref name="text"/> without the indentation its lines share, trailing whitespace and blank
    /// lines at either end.
    /// </summary>
    internal static string Dedent(string text)
    {
        string[] lines = string.IsNullOrWhiteSpace(text) ? [] : text.Split('\n');
        int indent = int.MaxValue;
        foreach (string line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                indent = Math.Min(indent, line.Length - line.TrimStart().Length);
            }
        }
        return string.Join("\n", lines.Select(line => string.IsNullOrWhiteSpace(line) ? "" : line[indent..].TrimEnd()))
            .Trim();
    }

    /// <summary>The C# code blocks of a Markdown text, each with the references of its marker.</summary>
    /// <param name="text">The text.</param>
    /// <param name="file">The name of the file, for the locations.</param>
    internal static List<ExampleBlock> MarkdownBlocks(string text, string file)
    {
        string[] lines = text.Split('\n');
        var blocks = new List<ExampleBlock>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() != "```csharp")
            {
                continue;
            }
            int start = i;
            var body = new List<string>();
            for (i++; i < lines.Length && lines[i].Trim() != "```"; i++)
            {
                body.Add(lines[i]);
            }
            blocks.Add(new ExampleBlock($"{file}:{start + 1}", ReferencesOf(lines, start), body));
        }
        return blocks;
    }

    /// <summary>
    /// The C# code blocks of the XML documentation comments of a source file, each with the
    /// references of its marker, and with the escapes of XML undone.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="file">The name of the file, for the locations.</param>
    /// <param name="failures">Receives the blocks that do not stand on lines of their own.</param>
    internal static List<ExampleBlock> XmlDocumentationBlocks(string source, string file, List<string> failures)
    {
        // The documentation lines without their slashes and the space after them, and blank lines in
        // place of the other lines of the source, so that an index is a line number minus one.
        string[] lines = [.. source.Split('\n').Select(line =>
        {
            Match doc = DocLine().Match(line);
            return !doc.Success ? "" : doc.Groups[1].Value.StartsWith(' ') ? doc.Groups[1].Value[1..] : doc.Groups[1].Value;
        })];
        var blocks = new List<ExampleBlock>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("language=\"csharp\"", StringComparison.Ordinal))
            {
                continue;
            }
            if (lines[i].Trim() != "<code language=\"csharp\">")
            {
                failures.Add($"{file}:{i + 1}: a C# block that does not start on a line of its own");
                continue;
            }
            int start = i;
            var body = new List<string>();
            for (i++; i < lines.Length && lines[i].Trim() != "</code>"; i++)
            {
                body.Add(Unescape(lines[i]));
            }
            blocks.Add(new ExampleBlock($"{file}:{start + 1}", ReferencesOf(lines, start), body));
        }
        return blocks;
    }

    /// <summary>
    /// Compares a block with the regions it names: its leading using directives, if
    /// <paramref name="withUsings"/>, must be using directives of the named files, and the rest must
    /// equal the regions, without their common indentation, separated by a blank line.
    /// </summary>
    /// <returns>What is wrong, or nothing.</returns>
    internal static List<string> Check(ExampleBlock block, bool withUsings)
    {
        if (block.References.Count == 0)
        {
            return [$"{block.Location}: a C# block without an example marker"];
        }
        var failures = new List<string>();
        var regions = new List<string>();
        var sources = new List<string>();
        foreach (string reference in block.References)
        {
            string[] parts = reference.Split('#');
            string? source = Sources.Example(parts[0]);
            if (source is not null)
            {
                sources.Add(source);
            }
            string? text = source is null || parts.Length != 2 ? null : Region(source, parts[1]);
            if (text is null)
            {
                failures.Add($"{block.Location}: no region {reference}");
            }
            else
            {
                regions.Add(text);
            }
        }
        var usings = new List<string>();
        var body = new List<string>();
        foreach (string line in block.Lines)
        {
            (withUsings && body.Count == 0 && Using().IsMatch(line) ? usings : body).Add(line);
        }
        foreach (string line in usings)
        {
            if (!sources.Any(source => source.Split('\n').Contains(line)))
            {
                failures.Add($"{block.Location}: {line} is not a using directive of {string.Join(" ", block.References)}");
            }
        }
        string expected = string.Join("\n\n", regions);
        string actual = Dedent(string.Join("\n", body));
        if (regions.Count == block.References.Count && actual != expected)
        {
            failures.Add($"{block.Location}: differs from {string.Join(" ", block.References)}\n"
                + $"--- documentation\n{actual}\n--- examples\n{expected}");
        }
        return failures;
    }

    /// <summary>Fails with every failure in full, if there is any.</summary>
    internal static void AssertNone(List<string> failures)
    {
        if (failures.Count > 0)
        {
            Assert.Fail(string.Join("\n\n", failures));
        }
    }

    // The references of the marker on the line before the block at start, or none.
    private static string[] ReferencesOf(string[] lines, int start)
    {
        Match marker = start > 0 ? Marker().Match(lines[start - 1]) : Match.Empty;
        return marker.Success ? marker.Groups[1].Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];
    }

    // The text of an XML line: the five predefined entities replaced by their characters, the
    // ampersand last.
    private static string Unescape(string line) => line
        .Replace("&lt;", "<", StringComparison.Ordinal)
        .Replace("&gt;", ">", StringComparison.Ordinal)
        .Replace("&quot;", "\"", StringComparison.Ordinal)
        .Replace("&apos;", "'", StringComparison.Ordinal)
        .Replace("&amp;", "&", StringComparison.Ordinal);
}
