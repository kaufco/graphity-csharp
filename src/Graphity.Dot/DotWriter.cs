using System.Globalization;

namespace Graphity.Dot;

/// <summary>
/// A writer of DOT (the language of Graphviz) that labels and styles a graph with the caller's data by
/// node ID and edge index.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Of"/> returns a writer for a graph, the methods that configure it return the writer
/// again, and <see cref="Write"/> or <see cref="WriteToString"/> write the text. The graph holds no
/// data, so labels and attributes come from hooks that receive a node ID or an edge index and read the
/// caller's arrays. Nodes are named by their node IDs. The text lists the graph attributes, the
/// clusters in group order with their nodes, then the nodes in no cluster, then the edges in edge
/// index order, so the same graph and hooks always give the same text. A node outside the clusters is
/// listed only if it has a label or attributes, or no edge in the text; Graphviz creates the other
/// nodes from their edges.
/// </para>
/// <para>
/// Labels and attribute values are written as quoted strings: the writer escapes quotes and
/// backslashes and turns line breaks into DOT's <c>\n</c>. Graphviz decodes HTML entities such as
/// <c>&amp;lt;</c> in the attributes <c>label</c>, <c>xlabel</c>, <c>headlabel</c> and
/// <c>taillabel</c>. In these, the writer also turns <c>&amp;</c> into <c>&amp;amp;</c>, so that a
/// label shows exactly its text. Other values, such as a <c>URL</c>, keep their <c>&amp;</c>. NUL,
/// which DOT cannot hold, is written as U+FFFD.
/// </para>
/// <para>
/// Attribute names must be identifiers - an ASCII letter or underscore, then ASCII letters, digits or
/// underscores - other than the DOT keywords <c>node</c>, <c>edge</c>, <c>graph</c>, <c>digraph</c>,
/// <c>subgraph</c> and <c>strict</c>, in any letter case.
/// </para>
/// <para>
/// A writer keeps its configuration and can write any number of times. For Markdown and
/// documentation, <c>MermaidWriter</c> in the package <c>Graphity.Mermaid</c> writes a Mermaid
/// flowchart.
/// </para>
/// <para>Instances are mutable and not safe to share between threads.</para>
/// </remarks>
/// <example>
/// Here, the packages of a small program get their names from <c>names</c>. The strongly connected
/// components <c>sccs</c> tell which packages are in a cycle, and these are drawn in red:
/// <!-- example: WritingDot.cs#nodeAttributes -->
/// <code language="csharp">
/// string dot = DotWriter.Of(dependencies)
///     .NodeLabel(node => names[node])
///     .NodeAttributes((node, attributes) =>
///     {
///         if (sccs.ContainsCycle(sccs.SccOf(node)))
///         {
///             attributes["color"] = "red";
///         }
///     })
///     .WriteToString();
/// // model and storage, the cycle, in red:
/// //   4 [label="model", color="red"];
/// //   5 [label="storage", color="red"];
/// </code>
/// </example>
public sealed class DotWriter
{
    // The DOT keywords, which DOT matches in any letter case.
    private static readonly HashSet<string> Keywords =
        new(["node", "edge", "graph", "digraph", "subgraph", "strict"], StringComparer.OrdinalIgnoreCase);

    // The attributes whose values Graphviz reads as labels, decoding HTML entities.
    private static readonly HashSet<string> LabelAttributes =
        new(["label", "xlabel", "headlabel", "taillabel"], StringComparer.Ordinal);

    private readonly Selection _selection;
    private readonly OrderedDictionary<string, string> _graphAttributes = new(StringComparer.Ordinal);
    private Func<int, string?>? _nodeLabel;
    private Func<int, string?>? _edgeLabel;
    private AttributeHook? _nodeAttributes;
    private AttributeHook? _edgeAttributes;

    private DotWriter(Graph graph)
    {
        _selection = new Selection(graph);
    }

    /// <summary>
    /// Returns a writer for <paramref name="graph"/>, with no labels, attributes, clusters or filter yet.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="graph">The graph to write.</param>
    /// <returns>The writer.</returns>
    public static DotWriter Of(Graph graph) => new(graph);

    /// <summary>
    /// Sets an attribute of the graph, such as <c>rankdir</c> to <c>LR</c>.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="name">The attribute name, an identifier other than a DOT keyword.</param>
    /// <param name="value">The attribute value.</param>
    /// <returns>This writer.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not an identifier or is a DOT keyword.</exception>
    public DotWriter GraphAttribute(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        _graphAttributes[CheckName(name, nameof(name))] = value;
        return this;
    }

    /// <summary>
    /// Sets the label of each node, by node ID; <see langword="null"/> leaves a node unlabeled, and
    /// Graphviz then shows its node ID.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="label">Called with a node ID, returns the label of the node or <see langword="null"/>.</param>
    /// <returns>This writer.</returns>
    public DotWriter NodeLabel(Func<int, string?> label)
    {
        ArgumentNullException.ThrowIfNull(label);
        _nodeLabel = label;
        return this;
    }

    /// <summary>
    /// Sets the label of each edge, by edge index; <see langword="null"/> leaves an edge unlabeled.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="label">Called with an edge index, returns the label of the edge or <see langword="null"/>.</param>
    /// <returns>This writer.</returns>
    public DotWriter EdgeLabel(Func<int, string?> label)
    {
        ArgumentNullException.ThrowIfNull(label);
        _edgeLabel = label;
        return this;
    }

    /// <summary>
    /// Sets the hook that adds DOT attributes, such as <c>shape</c>, <c>color</c> or <c>style</c>, to
    /// each node, by node ID.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hook receives the label from <see cref="NodeLabel"/> as the attribute <c>label</c>, which it
    /// can replace or remove.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="hook">Called with each node that is written, puts its attributes.</param>
    /// <returns>This writer.</returns>
    public DotWriter NodeAttributes(AttributeHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        _nodeAttributes = hook;
        return this;
    }

    /// <summary>
    /// Sets the hook that adds DOT attributes, such as <c>color</c> or <c>style</c>, to each edge, by
    /// edge index.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hook receives the label from <see cref="EdgeLabel"/> as the attribute <c>label</c>, which it
    /// can replace or remove.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="hook">Called with each edge that is written, puts its attributes.</param>
    /// <returns>This writer.</returns>
    public DotWriter EdgeAttributes(AttributeHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        _edgeAttributes = hook;
        return this;
    }

    /// <summary>
    /// Draws each group of <paramref name="partition"/> as a cluster, with a label by group ID.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nodes in no group stay outside every cluster, and a group without a node to write has no cluster.
    /// A label of <see langword="null"/> leaves a cluster unlabeled, also when the graph has a
    /// <c>label</c>, which Graphviz would otherwise repeat on every cluster.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="partition">The groups, over the nodes of the graph, such as an <see cref="Sccs"/>.</param>
    /// <param name="label">Called with a group ID, returns the label of its cluster or <see langword="null"/>.</param>
    /// <returns>This writer.</returns>
    /// <exception cref="ArgumentException"><paramref name="partition"/> has a different node count than the graph.</exception>
    public DotWriter Clusters(NodePartition partition, Func<int, string?> label)
    {
        _selection.SetClusters(partition, label);
        return this;
    }

    /// <summary>
    /// Restricts the text to the nodes that <paramref name="include"/> accepts and the edges between them.
    /// </summary>
    /// <remarks>
    /// <para>Node IDs stay those of the graph, so the hooks keep reading the same arrays.</para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="include">Called with a node ID, returns whether to write the node.</param>
    /// <returns>This writer.</returns>
    public DotWriter NodeFilter(Func<int, bool> include)
    {
        ArgumentNullException.ThrowIfNull(include);
        _selection.NodeFilter = include;
        return this;
    }

    /// <summary>
    /// Switches to the undirected reading of the graph: a <c>graph</c> with <c>--</c> edges instead of a
    /// <c>digraph</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edges <c>a -> b</c> and <c>b -> a</c> become one, written as the edge from the lower node ID;
    /// the edge hooks receive its edge index.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <returns>This writer.</returns>
    public DotWriter Undirected()
    {
        _selection.Undirected = true;
        return this;
    }

    /// <summary>Writes the graph to <paramref name="output"/>, as DOT text.</summary>
    /// <remarks>
    /// <para>
    /// Numbers are written with the invariant culture, whatever the <see cref="TextWriter.FormatProvider"/>
    /// of <paramref name="output"/>.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount + groupCount), plus the hooks, with groupCount the number of
    /// groups of the partition drawn as clusters; in the undirected reading, O(log OutDegree(target))
    /// more per edge.<br/>
    /// <b>Memory:</b> NodeCount bytes while it runs.
    /// </para>
    /// </remarks>
    /// <param name="output">Where the text goes; not closed.</param>
    /// <exception cref="IOException">Writing to <paramref name="output"/> fails.</exception>
    /// <exception cref="ArgumentException">
    /// An attribute hook puts a name that is not an identifier or is a DOT keyword, or a
    /// <see langword="null"/> value; what was written before stays in <paramref name="output"/>.
    /// </exception>
    public void Write(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        Selection selection = _selection;
        int n = selection.Graph.NodeCount;
        string arrow = selection.Undirected ? " -- " : " -> ";
        output.Write(selection.Undirected ? "graph {\n" : "digraph {\n");
        foreach (var attribute in _graphAttributes)
        {
            output.Write("  ");
            output.Write(attribute.Key);
            output.Write('=');
            Quote(attribute.Value, LabelAttributes.Contains(attribute.Key), output);
            output.Write(";\n");
        }
        bool graphLabeled = _graphAttributes.ContainsKey("label");

        var hasEdge = new bool[n];
        selection.ForEachWrittenEdge((source, target, _) =>
        {
            hasEdge[source] = true;
            hasEdge[target] = true;
        });

        var attributes = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        Clusters? clusters = selection.Clusters;
        if (clusters is not null)
        {
            NodePartition partition = clusters.Partition;
            for (int group = 0; group < partition.GroupCount; group++)
            {
                SortedIntView nodes = partition.NodesOf(group);
                bool opened = false;
                for (int i = 0; i < nodes.Count; i++)
                {
                    int node = nodes[i];
                    if (!selection.IsIncluded(node))
                    {
                        continue;
                    }
                    if (!opened)
                    {
                        output.Write("  subgraph cluster_");
                        Selection.WriteInt(output, group);
                        output.Write(" {\n");
                        string? label = clusters.Label(group);
                        if (label is not null)
                        {
                            output.Write("    label=");
                            Quote(label, true, output);
                            output.Write(";\n");
                        }
                        else if (graphLabeled)
                        {
                            output.Write("    label=\"\";\n");
                        }
                        opened = true;
                    }
                    PutNodeAttributes(node, attributes);
                    WriteNode(node, attributes, "    ", output);
                }
                if (opened)
                {
                    output.Write("  }\n");
                }
            }
        }
        for (int node = 0; node < n; node++)
        {
            if (!selection.IsIncluded(node) || selection.ClusterOf(node) >= 0)
            {
                continue;
            }
            PutNodeAttributes(node, attributes);
            if (attributes.Count != 0 || !hasEdge[node])
            {
                WriteNode(node, attributes, "  ", output);
            }
        }
        selection.ForEachWrittenEdge((source, target, edgeIndex) =>
        {
            output.Write("  ");
            Selection.WriteInt(output, source);
            output.Write(arrow);
            Selection.WriteInt(output, target);
            PutEdgeAttributes(edgeIndex, attributes);
            WriteAttributes(attributes, output);
            output.Write(";\n");
        });
        output.Write("}\n");
    }

    /// <summary>Returns the DOT text of the graph, as <see cref="Write"/> writes it.</summary>
    /// <remarks>
    /// <b>Time:</b> as for <see cref="Write"/>.<br/>
    /// <b>Memory:</b> the text for the result; while it runs, up to twice as much again and NodeCount
    /// bytes more.
    /// </remarks>
    /// <returns>The DOT text.</returns>
    /// <exception cref="ArgumentException">As for <see cref="Write"/>.</exception>
    public string WriteToString()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        Write(output);
        return output.ToString();
    }

    private static void WriteNode(int node, OrderedDictionary<string, string> attributes, string indent, TextWriter output)
    {
        output.Write(indent);
        Selection.WriteInt(output, node);
        WriteAttributes(attributes, output);
        output.Write(";\n");
    }

    private void PutNodeAttributes(int node, OrderedDictionary<string, string> attributes)
    {
        attributes.Clear();
        string? label = _nodeLabel?.Invoke(node);
        if (label is not null)
        {
            attributes["label"] = label;
        }
        _nodeAttributes?.Invoke(node, attributes);
    }

    private void PutEdgeAttributes(int edgeIndex, OrderedDictionary<string, string> attributes)
    {
        attributes.Clear();
        string? label = _edgeLabel?.Invoke(edgeIndex);
        if (label is not null)
        {
            attributes["label"] = label;
        }
        _edgeAttributes?.Invoke(edgeIndex, attributes);
    }

    private static void WriteAttributes(OrderedDictionary<string, string> attributes, TextWriter output)
    {
        bool first = true;
        foreach (var attribute in attributes)
        {
            string name = CheckName(attribute.Key, null);
            // A hook outside the nullable contract; Java fails here with a NullPointerException.
            string value = attribute.Value
                ?? throw new ArgumentException($"the DOT attribute {name} has a null value");
            output.Write(first ? " [" : ", ");
            output.Write(name);
            output.Write('=');
            Quote(value, LabelAttributes.Contains(name), output);
            first = false;
        }
        if (!first)
        {
            output.Write(']');
        }
    }

    private static string CheckName(string name, string? paramName)
    {
        if (!Selection.IsIdentifier(name) || Keywords.Contains(name))
        {
            throw new ArgumentException("not a DOT attribute name: " + name, paramName);
        }
        return name;
    }

    /// <summary>
    /// Writes <paramref name="text"/> as a DOT quoted string that shows exactly that text, NUL
    /// aside, which DOT cannot hold. For an attribute in which Graphviz decodes HTML entities, the
    /// labels, <paramref name="decodesEntities"/> turns <c>&amp;</c> into <c>&amp;amp;</c>.
    /// </summary>
    private static void Quote(string text, bool decodesEntities, TextWriter output)
    {
        output.Write('"');
        int plain = 0;
        for (int i = 0; i < text.Length; i++)
        {
            string? escaped = text[i] switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => i + 1 < text.Length && text[i + 1] == '\n' ? "" : "\\n",
                '&' when decodesEntities => "&amp;",
                '\0' => "\uFFFD",
                _ => null,
            };
            if (escaped is null)
            {
                continue;
            }
            output.Write(text.AsSpan(plain, i - plain));
            output.Write(escaped);
            plain = i + 1;
        }
        output.Write(text.AsSpan(plain));
        output.Write('"');
    }
}
