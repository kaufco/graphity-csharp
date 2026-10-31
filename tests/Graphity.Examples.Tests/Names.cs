namespace Examples.Tests;

/// <summary>The comments of the examples give node IDs with their names; the tests compare both.</summary>
internal static class Names
{
    internal static string[] Of(IEnumerable<int> nodes, string[] names) => [.. nodes.Select(node => names[node])];

    /// <summary>The edges of a graph in edge index order, each as <c>source -> target</c> with the names.</summary>
    internal static List<string> EdgesByName(Graph graph, string[] names)
    {
        var edges = new List<string>();
        graph.ForEachEdge((source, target, edge) => edges.Add($"{names[source]} -> {names[target]}"));
        return edges;
    }

    /// <summary>The edges of a list, as the comments of the examples write them, in their order.</summary>
    internal static List<string> EdgesOf(Edges edges, string[] names)
    {
        var result = new List<string>();
        for (int p = 0; p < edges.Count; p++)
        {
            result.Add($"{names[edges.SourceNodes[p]]} -> {names[edges.TargetNodes[p]]}");
        }
        return result;
    }

    /// <summary>The lines that <paramref name="write"/> writes, as Java's <c>String.lines()</c> splits them.</summary>
    internal static List<string> LinesWrittenBy(Action<TextWriter> write)
    {
        using var output = new StringWriter();
        write(output);
        return LinesOf(output.ToString());
    }

    /// <summary>The lines of <paramref name="text"/>, as Java's <c>String.lines()</c> splits them.</summary>
    internal static List<string> LinesOf(string text)
    {
        var lines = new List<string>();
        using var reader = new StringReader(text);
        for (string? line = reader.ReadLine(); line is not null; line = reader.ReadLine())
        {
            lines.Add(line);
        }
        return lines;
    }
}
