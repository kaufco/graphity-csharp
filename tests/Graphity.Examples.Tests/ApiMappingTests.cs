using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Examples.Tests;

/// <summary>
/// Checks the API mapping, <c>examples/api-csharp.json</c>: the C# counterpart of each type,
/// constructor and method of the Java API, which <c>examples/java-refs.txt</c> lists by its
/// declaration. The guide links each name of the Java API through it to this API documentation. Each
/// entry has a name and the url of its page and anchor, relative to the root of the API documentation,
/// or, where C# has no counterpart, a reason. Where one name has several overloads, each entry of it
/// also has a signature, the name with its parameter list. Where the API documentation is built, each
/// url must lead to a page of it and to an anchor on that page, and each signature must be the one it
/// shows for that url.
/// </summary>
public sealed partial class ApiMappingTests
{
    [Fact]
    public void TheMappingHasAnEntryForEachRefOfTheJavaApiAndNoOther()
    {
        var failures = new List<string>();
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (string reference in ApiMapping.JavaRefs)
        {
            if (!expected.Add(reference))
            {
                failures.Add($"{ApiMapping.JavaRefsFile}: {reference} is listed twice");
            }
        }
        var mapped = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string reference, _) in ApiMapping.Refs())
        {
            if (!mapped.Add(reference))
            {
                failures.Add($"{reference} has two entries");
            }
            else if (!expected.Contains(reference))
            {
                failures.Add($"{reference} is not a ref of {ApiMapping.JavaRefsFile}");
            }
        }
        failures.AddRange(ApiMapping.JavaRefs.Where(reference => !mapped.Contains(reference)).Distinct()
            .Select(reference => $"{reference} has no entry"));
        Assert.True(expected.Count > 0);
        AssertNone(failures);
    }

    [Fact]
    public void EachEntryHasANameAndAUrlOrAReason()
    {
        var failures = new List<string>();
        foreach ((string reference, JsonElement entry) in ApiMapping.Refs())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                failures.Add($"{reference}: the entry is not an object");
                continue;
            }
            foreach (JsonProperty property in entry.EnumerateObject())
            {
                if (property.Name is not ("name" or "signature" or "url" or "note" or "reason"))
                {
                    failures.Add($"{reference}: unknown property {property.Name}");
                }
            }
            string? name = TextOf(entry, "name");
            string? signature = TextOf(entry, "signature");
            string? url = TextOf(entry, "url");
            string? note = TextOf(entry, "note");
            string? reason = TextOf(entry, "reason");
            if (name is not null)
            {
                if (url is null)
                {
                    failures.Add($"{reference}: a name needs a url");
                }
                if (reason is not null)
                {
                    failures.Add($"{reference}: an entry with a name needs no reason; a note says what a reader would not expect");
                }
            }
            else
            {
                if (url is not null || signature is not null || note is not null)
                {
                    failures.Add($"{reference}: an entry without a name has no url, no signature and no note");
                }
                if (reason is null)
                {
                    failures.Add($"{reference}: needs a name and a url, or a reason");
                }
            }
            foreach (string key in new[] { "name", "signature", "url", "note", "reason" })
            {
                if (entry.TryGetProperty(key, out JsonElement value)
                    && !(value.ValueKind == JsonValueKind.Null || (value.ValueKind == JsonValueKind.String && value.GetString()!.Trim().Length > 0)))
                {
                    failures.Add($"{reference}: {key} is neither null nor a text");
                }
            }
            if (url is not null && !IsRelativeUrl(url))
            {
                failures.Add($"{reference}: {url} is not a page relative to the root of the API documentation");
            }
        }
        AssertNone(failures);
    }

    [Fact]
    public void EachOverloadOfANameHasASignatureOfItsOwn()
    {
        // A name with several urls has several overloads in C#, which the guide tells apart by their
        // signatures. Java's overloads that are one method in C# share its url and keep the plain name.
        var failures = new List<string>();
        var entries = ApiMapping.Refs()
            .Where(item => item.Entry.ValueKind == JsonValueKind.Object)
            .Select(item => (item.Reference, Name: TextOf(item.Entry, "name"), Url: TextOf(item.Entry, "url"),
                Signature: TextOf(item.Entry, "signature")))
            .Where(item => item.Name is not null && item.Url is not null);
        foreach (var overloads in entries.GroupBy(item => item.Name!, StringComparer.Ordinal))
        {
            string name = overloads.Key;
            int urlCount = overloads.Select(item => item.Url).Distinct(StringComparer.Ordinal).Count();
            foreach (var (reference, _, _, signature) in overloads)
            {
                if (urlCount > 1 && signature is null)
                {
                    failures.Add($"{reference}: {name} has {urlCount} overloads, so each needs a signature");
                }
                else if (urlCount == 1 && signature is not null)
                {
                    failures.Add($"{reference}: {name} is one method, so it needs no signature");
                }
                else if (signature is not null
                    && !((signature.StartsWith(name + "(", StringComparison.Ordinal) || signature.StartsWith(name + "<", StringComparison.Ordinal))
                        && signature.EndsWith(')')))
                {
                    failures.Add($"{reference}: the signature {signature} is not {name} with a parameter list");
                }
            }
            int pairCount = overloads.Select(item => (item.Url, item.Signature)).Distinct().Count();
            int signatureCount = overloads.Select(item => item.Signature).Distinct(StringComparer.Ordinal).Count();
            if (urlCount > 1 && (pairCount != urlCount || signatureCount != urlCount))
            {
                failures.Add($"{name}: the entries of one url need the same signature, those of another url another one");
            }
        }
        AssertNone(failures);
    }

    [Fact]
    public void TheDocRootIsWhereDocFxPutsTheApiDocumentation()
    {
        using JsonDocument docfx = JsonDocument.Parse(Sources.Read("docs/docfx.json"));
        string output = docfx.RootElement.GetProperty("build").GetProperty("output").GetString()!;
        Assert.Equal($"docs/{output}", ApiMapping.DocRoot);
        Assert.Contains("docs/docfx.json", ApiMapping.DocCommand, StringComparison.Ordinal);
    }

    [FactWhenApiDocumentationIsBuilt]
    public void EachUrlLeadsToAPageAndAnAnchorOfTheApiDocumentation()
    {
        AssertDocumentationIsBuilt();
        var failures = new List<string>();
        var anchorsOfPage = new Dictionary<string, HashSet<string>?>(StringComparer.Ordinal);
        int count = 0;
        foreach ((string reference, JsonElement entry) in ApiMapping.Refs())
        {
            string? url = entry.ValueKind == JsonValueKind.Object ? TextOf(entry, "url") : null;
            if (url is null || !IsRelativeUrl(url))
            {
                continue; // EachEntryHasANameAndAUrlOrAReason names these
            }
            count++;
            int hash = url.IndexOf('#', StringComparison.Ordinal);
            string page = hash < 0 ? url : url[..hash];
            if (!anchorsOfPage.TryGetValue(page, out HashSet<string>? anchors))
            {
                string file = Path.Combine(ApiMapping.DocDirectory, page);
                anchors = File.Exists(file) ? AnchorsOf(File.ReadAllText(file)) : null;
                anchorsOfPage[page] = anchors;
            }
            if (anchors is null)
            {
                failures.Add($"{reference}: {url} - no page {page} in {ApiMapping.DocRoot}");
            }
            else if (hash >= 0 && !anchors.Contains(url[(hash + 1)..]))
            {
                failures.Add($"{reference}: {url} - no anchor {url[(hash + 1)..]} on {page}");
            }
        }
        Assert.True(count > 0);
        AssertNone(failures);
    }

    [FactWhenApiDocumentationIsBuilt]
    public void EachSignatureIsTheOneTheApiDocumentationShowsAtItsUrl()
    {
        AssertDocumentationIsBuilt();
        Dictionary<string, string> shown = NamesWithTypeByUrl(File.ReadAllText(Path.Combine(ApiMapping.DocDirectory, "xrefmap.yml")));
        var failures = new List<string>();
        int count = 0;
        foreach ((string reference, JsonElement entry) in ApiMapping.Refs())
        {
            string? signature = entry.ValueKind == JsonValueKind.Object ? TextOf(entry, "signature") : null;
            string? url = entry.ValueKind == JsonValueKind.Object ? TextOf(entry, "url") : null;
            if (signature is null || url is null)
            {
                continue; // EachEntryHasANameAndAUrlOrAReason names a signature without a url
            }
            count++;
            if (!shown.TryGetValue(url, out string? nameWithType))
            {
                failures.Add($"{reference}: xrefmap.yml has no entry at {url}");
            }
            else if (nameWithType != signature)
            {
                failures.Add($"{reference}: the signature is {signature}, the API documentation shows {nameWithType}");
            }
        }
        Assert.True(count > 0);
        AssertNone(failures);
    }

    private static void AssertDocumentationIsBuilt() =>
        Assert.False(ApiMapping.DocumentationIsMissing(out string docRoot, out string docCommand),
            $"The API documentation is not built in {docRoot}. Build it with \"{docCommand}\".");

    /// <summary>
    /// The name with its type and parameter list of each entry of DocFX's <c>xrefmap.yml</c>, by its
    /// url: the name as the API documentation shows it, such as <c>DfsTraverser.Run(int)</c>.
    /// </summary>
    private static Dictionary<string, string> NamesWithTypeByUrl(string xrefmap)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        string? href = null;
        string? nameWithType = null;
        foreach (string line in xrefmap.Split('\n').Append("- "))
        {
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (href is not null && nameWithType is not null)
                {
                    result.TryAdd(href, nameWithType);
                }
                href = null;
                nameWithType = null;
            }
            string property = line.TrimStart('-', ' ').TrimEnd('\r');
            if (property.StartsWith("href: ", StringComparison.Ordinal))
            {
                href = Unquoted(property["href: ".Length..]);
            }
            else if (property.StartsWith("nameWithType: ", StringComparison.Ordinal))
            {
                nameWithType = Unquoted(property["nameWithType: ".Length..]);
            }
        }
        return result;
    }

    /// <summary>A YAML value without the quotes around it, if it has any.</summary>
    private static string Unquoted(string value) =>
        value.Length >= 2 && (value[0] is '"' or '\'') && value[^1] == value[0] ? value[1..^1] : value;

    /// <summary>The text of a property, or <see langword="null"/> if it is missing, null or not a text.</summary>
    private static string? TextOf(JsonElement entry, string key) =>
        entry.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String
            && value.GetString()!.Trim().Length > 0
            ? value.GetString()
            : null;

    /// <summary>A page of the site, with an optional anchor, relative to its root and inside it.</summary>
    private static bool IsRelativeUrl(string url) =>
        !url.StartsWith('/') && !url.StartsWith('#') && !url.Contains("://", StringComparison.Ordinal)
        && !url.Split('#')[0].Split('/').Contains("..") && url.Count(c => c == '#') <= 1;

    /// <summary>The values of the id attributes of a page, the anchors a url can name.</summary>
    private static HashSet<string> AnchorsOf(string html) =>
        new(IdAttribute().Matches(html).Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)), StringComparer.Ordinal);

    [GeneratedRegex("""\bid\s*=\s*"([^"]*)"|\bid\s*=\s*'([^']*)'""")]
    private static partial Regex IdAttribute();

    private static void AssertNone(List<string> failures) =>
        Assert.True(failures.Count == 0, $"{failures.Count} problems in {ApiMapping.MappingFile}:\n" + string.Join('\n', failures));
}

/// <summary>
/// A test that runs only where the API documentation is built, and is skipped otherwise, with the
/// command that builds it. Where the environment variable <c>GRAPHITY_REQUIRE_API_DOCS</c> is
/// <c>true</c>, as in the step of the CI that checks the API mapping, it always runs, so that a site
/// that is missing fails it instead of passing unchecked.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FactWhenApiDocumentationIsBuiltAttribute : FactAttribute
{
    public FactWhenApiDocumentationIsBuiltAttribute()
    {
        if (ApiMapping.DocumentationIsMissing(out string docRoot, out string docCommand)
            && Environment.GetEnvironmentVariable("GRAPHITY_REQUIRE_API_DOCS") != "true")
        {
            Skip = $"The API documentation is not built in {docRoot}, so the urls of {ApiMapping.MappingFile} are "
                + $"not checked against it. Build it with \"{docCommand}\" and run the tests again.";
        }
    }
}

/// <summary>The API mapping and the list of the refs of the Java API, as the tests read them.</summary>
internal static class ApiMapping
{
    internal const string MappingFile = "examples/api-csharp.json";
    internal const string JavaRefsFile = "examples/java-refs.txt";

    private static readonly Lazy<JsonDocument> Document = new(() => JsonDocument.Parse(Sources.Read(MappingFile)));

    /// <summary>The refs of the Java API, one per line of <see cref="JavaRefsFile"/>.</summary>
    internal static string[] JavaRefs =>
        [.. Sources.Read(JavaRefsFile).Split('\n').Where(line => line.Trim().Length > 0)];

    /// <summary>The entries of the mapping in their order, a key that occurs twice included.</summary>
    internal static IEnumerable<(string Reference, JsonElement Entry)> Refs() =>
        Document.Value.RootElement.GetProperty("refs").EnumerateObject().Select(property => (property.Name, property.Value));

    /// <summary>Where the build puts the API documentation, from the root of the repository.</summary>
    internal static string DocRoot => Document.Value.RootElement.GetProperty("docRoot").GetString()!;

    /// <summary>The command that builds the API documentation.</summary>
    internal static string DocCommand => Document.Value.RootElement.GetProperty("docCommand").GetString()!;

    /// <summary>The directory of the built API documentation.</summary>
    internal static string DocDirectory => Path.Combine(Sources.Root, DocRoot);

    /// <summary>
    /// Whether the mapping names where the API documentation goes and its home page is not there.
    /// Runs when the tests are discovered, so it never fails: if the mapping cannot be read, the
    /// test runs and its failure says why.
    /// </summary>
    internal static bool DocumentationIsMissing(out string docRoot, out string docCommand)
    {
        docRoot = "";
        docCommand = "";
        try
        {
            string? text = Sources.ReadOrNull(MappingFile);
            if (text is null)
            {
                return false;
            }
            using JsonDocument mapping = JsonDocument.Parse(text);
            docRoot = mapping.RootElement.GetProperty("docRoot").GetString() ?? "";
            docCommand = mapping.RootElement.GetProperty("docCommand").GetString() ?? "";
            return docRoot.Length > 0 && !File.Exists(Path.Combine(Sources.Root, docRoot, "index.html"));
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return false;
        }
    }
}
