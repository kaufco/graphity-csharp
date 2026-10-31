using System.Globalization;
using System.Text;

namespace Graphity.Mermaid;

/// <summary>
/// A writer of Mermaid flowcharts that labels and styles a graph with the caller's data by node ID and
/// edge index. Mermaid flowcharts render in Markdown.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Of"/> returns a writer for a graph, the methods that configure it return the writer
/// again, and <see cref="Write"/> or <see cref="WriteToString"/> write the text. The graph holds no
/// data, so labels, shapes, classes and styles come from hooks that receive a node ID or an edge index
/// and read the caller's arrays. Nodes are named by their node IDs after an <c>n</c>, such as
/// <c>n4</c>. The text lists the class definitions, the clusters in group order with their nodes,
/// then the nodes in no cluster, then the edges in edge index order, the classes of the nodes and
/// the edge styles, so the same graph and hooks always give the same text. Mermaid numbers the edges
/// in the order they are written; the writer translates edge indices into these numbers for
/// <c>linkStyle</c>, also when a filter leaves edges out.
/// </para>
/// <para>
/// Labels are written as quoted strings: the writer turns quotes, <c>#</c>, <c>%</c>, <c>&amp;</c>,
/// <c>:</c>, <c>&lt;</c>, <c>&gt;</c> and backticks into Mermaid entity codes and line breaks into
/// <c>&lt;br&gt;</c>, so that no label reads as HTML, as a Markdown string or as a directive. For the
/// same reason, whitespace right after the word <c>direction</c> becomes an entity code, and an empty
/// label, which Mermaid rejects, is written as one space.
/// </para>
/// <para>
/// Mermaid's renderer still changes some labels. Mermaid 11 applies Markdown emphasis and backslash
/// escapes to every label, so <c>2*3*4</c> loses its asterisks; Mermaid 12 shows a backslash before
/// <c>n</c> as a line break. Both read <c>¶ß</c> and <c>ﬂ°</c>, which Mermaid uses internally for
/// entity codes, as <c>;</c> and <c>&amp;</c>. Both also turn <c>fa:fa-</c><i>name</i> into an icon in
/// a node label with a line break. With <c>htmlLabels</c> set to <c>false</c>, Mermaid shows some
/// entity codes as written, such as <c>&amp;#58;</c> for <c>:</c>.
/// </para>
/// <para>
/// Class names must be identifiers - an ASCII letter or underscore, then ASCII letters, digits or
/// underscores - other than a Mermaid keyword: <c>style</c>, <c>linkStyle</c>, <c>interpolate</c>,
/// <c>classDef</c>, <c>class</c>, <c>href</c>, <c>click</c>, <c>call</c>, <c>graph</c>,
/// <c>flowchart</c>, <c>subgraph</c>, <c>end</c>, <c>_self</c>, <c>_blank</c>, <c>_parent</c> and
/// <c>_top</c>. Keywords are case-sensitive, so <c>End</c> is a valid class name. Styles are written
/// as given and must not be blank or contain a line break or a semicolon. Whitespace in these rules is
/// that of the Java library, so that both write the same text. <c>direction</c> is followed by
/// whitespace if Java's <c>Character.isWhitespace</c> or <c>isSpaceChar</c> holds for the next
/// character, or if that character is U+FEFF. A style is blank if it is empty or holds only characters
/// for which <c>Character.isWhitespace</c> holds.
/// </para>
/// <para>
/// Mermaid renders graphs of a few hundred nodes well; for larger ones, <c>DotWriter</c> in the
/// package <c>Graphity.Dot</c> writes DOT (the language of Graphviz). A writer keeps its configuration
/// and can write any number of times.
/// </para>
/// <para>Instances are mutable and not safe to share between threads.</para>
/// </remarks>
/// <example>
/// Here, the packages of a small program get their names from <c>names</c>. The strongly connected
/// components <c>sccs</c> tell which packages are in a cycle, and these are drawn in red:
/// <!-- example: WritingMermaid.cs#nodeClass -->
/// <code language="csharp">
/// string mermaid = MermaidWriter.Of(dependencies)
///     .NodeLabel(node => names[node])
///     .ClassDef("cycle", "stroke:red")
///     .NodeClass(node => sccs.ContainsCycle(sccs.SccOf(node)) ? "cycle" : null)
///     .WriteToString();
/// // model and storage, the cycle, in red:
/// //   classDef cycle stroke:red
/// //   class n4 cycle
/// //   class n5 cycle
/// </code>
/// </example>
public sealed class MermaidWriter
{
    /// <summary>
    /// The shape of a node, with the brackets Mermaid draws it with.
    /// </summary>
    public enum Shape
    {
        /// <summary>A rectangle, <c>[...]</c>.</summary>
        Rectangle,

        /// <summary>A rectangle with rounded corners, <c>(...)</c>.</summary>
        Rounded,

        /// <summary>A stadium, <c>([...])</c>.</summary>
        Stadium,

        /// <summary>A subroutine box, <c>[[...]]</c>.</summary>
        Subroutine,

        /// <summary>A cylinder, <c>[(...)]</c>.</summary>
        Cylinder,

        /// <summary>A circle, <c>((...))</c>.</summary>
        Circle,

        /// <summary>A double circle, <c>(((...)))</c>.</summary>
        DoubleCircle,

        /// <summary>A rhombus, <c>{...}</c>.</summary>
        Rhombus,

        /// <summary>A hexagon, <c>{{...}}</c>.</summary>
        Hexagon,
    }

    // The words that the flowchart lexer of Mermaid reads as keywords, never as a class name.
    private static readonly HashSet<string> Keywords = new(
        ["style", "linkStyle", "interpolate", "classDef", "class", "href", "click", "call", "graph",
            "flowchart", "subgraph", "end", "_self", "_blank", "_parent", "_top"],
        StringComparer.Ordinal);

    // The word that starts a direction statement; whitespace after it is encoded in labels.
    private const string DirectionWord = "direction";

    private readonly Selection _selection;
    private MermaidDirection _direction = MermaidDirection.TopDown;
    private readonly OrderedDictionary<string, string> _classDefs = new(StringComparer.Ordinal);
    private Func<int, string?>? _nodeLabel;
    private Func<int, string?>? _edgeLabel;
    private Func<int, Shape?>? _nodeShape;
    private Func<int, string?>? _nodeClass;
    private Func<int, string?>? _edgeStyle;

    private MermaidWriter(Graph graph)
    {
        _selection = new Selection(graph);
    }

    /// <summary>
    /// Returns a writer for <paramref name="graph"/>, top down, with no labels, shapes, classes, clusters
    /// or filter yet.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="graph">The graph to write.</param>
    /// <returns>The writer.</returns>
    public static MermaidWriter Of(Graph graph) => new(graph);

    /// <summary>
    /// Sets the direction of the flowchart; without a call, it is <see cref="MermaidDirection.TopDown"/>.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="direction">The direction.</param>
    /// <returns>This writer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="direction"/> is not a member of <see cref="MermaidDirection"/>.
    /// </exception>
    public MermaidWriter Direction(MermaidDirection direction)
    {
        _ = CodeOf(direction);
        _direction = direction;
        return this;
    }

    /// <summary>
    /// Sets the label of each node, by node ID; <see langword="null"/> shows the name of the node, such as
    /// <c>n4</c>.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="label">Called with a node ID, returns the label of the node or <see langword="null"/>.</param>
    /// <returns>This writer.</returns>
    public MermaidWriter NodeLabel(Func<int, string?> label)
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
    public MermaidWriter EdgeLabel(Func<int, string?> label)
    {
        ArgumentNullException.ThrowIfNull(label);
        _edgeLabel = label;
        return this;
    }

    /// <summary>
    /// Sets the shape of each node, by node ID; <see langword="null"/> draws a rectangle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A value that is not a member of <see cref="Shape"/> makes <see cref="Write"/> throw
    /// <see cref="ArgumentOutOfRangeException"/>.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="shape">Called with a node ID, returns the shape of the node or <see langword="null"/>.</param>
    /// <returns>This writer.</returns>
    public MermaidWriter NodeShape(Func<int, Shape?> shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        _nodeShape = shape;
        return this;
    }

    /// <summary>
    /// Defines a class for <see cref="NodeClass"/> to assign, written as <c>classDef name style</c>.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="name">The class name, an identifier other than a Mermaid keyword.</param>
    /// <param name="style">The style of the class, such as <c>fill:#f96,stroke:#333</c>.</param>
    /// <returns>This writer.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is not an identifier or is a Mermaid keyword, or <paramref name="style"/>
    /// is blank or contains a line break or a semicolon.
    /// </exception>
    public MermaidWriter ClassDef(string name, string style)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(style);
        _classDefs[CheckClass(name, nameof(name))] = CheckStyle(style, nameof(style));
        return this;
    }

    /// <summary>
    /// Sets the class of each node, by node ID; <see langword="null"/> assigns none.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="className">
    /// Called with a node ID, returns the name of the class of the node, an identifier other than a
    /// Mermaid keyword, or <see langword="null"/>.
    /// </param>
    /// <returns>This writer.</returns>
    public MermaidWriter NodeClass(Func<int, string?> className)
    {
        ArgumentNullException.ThrowIfNull(className);
        _nodeClass = className;
        return this;
    }

    /// <summary>
    /// Sets the style of each edge, by edge index, written as a <c>linkStyle</c> statement;
    /// <see langword="null"/> leaves an edge in the default style.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="style">
    /// Called with an edge index, returns the style of the edge, such as
    /// <c>stroke:red,stroke-width:2px</c>, or <see langword="null"/>.
    /// </param>
    /// <returns>This writer.</returns>
    public MermaidWriter EdgeStyle(Func<int, string?> style)
    {
        ArgumentNullException.ThrowIfNull(style);
        _edgeStyle = style;
        return this;
    }

    /// <summary>
    /// Draws each group of <paramref name="partition"/> as a cluster (a Mermaid <c>subgraph</c>), with a
    /// label by group ID.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nodes in no group stay outside every cluster, and a group without a node to write has no cluster.
    /// A label of <see langword="null"/> shows the name of the cluster: <c>c</c> and the group ID, such
    /// as <c>c2</c>.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="partition">The groups, over the nodes of the graph, such as an <see cref="Sccs"/>.</param>
    /// <param name="label">Called with a group ID, returns the label of its cluster or <see langword="null"/>.</param>
    /// <returns>This writer.</returns>
    /// <exception cref="ArgumentException"><paramref name="partition"/> has a different node count than the graph.</exception>
    public MermaidWriter Clusters(NodePartition partition, Func<int, string?> label)
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
    public MermaidWriter NodeFilter(Func<int, bool> include)
    {
        ArgumentNullException.ThrowIfNull(include);
        _selection.NodeFilter = include;
        return this;
    }

    /// <summary>
    /// Switches to the undirected reading of the graph: edges without arrowheads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edges <c>a -> b</c> and <c>b -> a</c> become one, written as the edge from the lower node ID;
    /// the edge hooks receive its edge index.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <returns>This writer.</returns>
    public MermaidWriter Undirected()
    {
        _selection.Undirected = true;
        return this;
    }

    /// <summary>
    /// Writes the graph to <paramref name="output"/>, as the text of a Mermaid flowchart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Numbers are written with the invariant culture, whatever the <see cref="TextWriter.FormatProvider"/>
    /// of <paramref name="output"/>.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount + groupCount), plus the hooks, with groupCount the number of
    /// groups of the partition drawn as clusters; in the undirected reading, O(log OutDegree(target))
    /// more per edge.<br/>
    /// <b>Memory:</b> up to twice the size of the <c>linkStyle</c> lines while it runs, as they are
    /// written last.
    /// </para>
    /// </remarks>
    /// <param name="output">Where the text goes; not closed.</param>
    /// <exception cref="IOException">Writing to <paramref name="output"/> fails.</exception>
    /// <exception cref="ArgumentException">
    /// A hook returns a class name that is not an identifier or is a Mermaid keyword, or an edge style
    /// that is blank or contains a line break or a semicolon; what was written before stays in
    /// <paramref name="output"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The shape hook returns a value that is not a member of <see cref="Shape"/>.
    /// </exception>
    public void Write(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        Selection selection = _selection;
        int n = selection.Graph.NodeCount;
        output.Write("flowchart ");
        output.Write(CodeOf(_direction));
        output.Write('\n');
        foreach (var classDef in _classDefs)
        {
            output.Write("  classDef ");
            output.Write(classDef.Key);
            output.Write(' ');
            output.Write(classDef.Value);
            output.Write('\n');
        }

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
                        output.Write("  subgraph c");
                        Selection.WriteInt(output, group);
                        string? label = clusters.Label(group);
                        if (label is not null)
                        {
                            output.Write('[');
                            Quote(label, output);
                            output.Write(']');
                        }
                        output.Write('\n');
                        opened = true;
                    }
                    WriteNode(node, "    ", output);
                }
                if (opened)
                {
                    output.Write("  end\n");
                }
            }
        }
        for (int node = 0; node < n; node++)
        {
            if (selection.IsIncluded(node) && selection.ClusterOf(node) < 0)
            {
                WriteNode(node, "  ", output);
            }
        }

        string link = selection.Undirected ? " ---" : " -->";
        var styles = new StringBuilder();
        int ordinal = 0;
        selection.ForEachWrittenEdge((source, target, edgeIndex) =>
        {
            output.Write("  n");
            Selection.WriteInt(output, source);
            output.Write(link);
            string? label = _edgeLabel?.Invoke(edgeIndex);
            if (label is not null)
            {
                output.Write('|');
                Quote(label, output);
                output.Write('|');
            }
            output.Write(" n");
            Selection.WriteInt(output, target);
            output.Write('\n');
            string? style = _edgeStyle?.Invoke(edgeIndex);
            if (style is not null)
            {
                styles.Append("  linkStyle ").Append(ordinal.ToString(CultureInfo.InvariantCulture))
                    .Append(' ').Append(CheckStyle(style, null)).Append('\n');
            }
            ordinal++;
        });

        if (_nodeClass is not null)
        {
            for (int node = 0; node < n; node++)
            {
                if (!selection.IsIncluded(node))
                {
                    continue;
                }
                string? className = _nodeClass(node);
                if (className is not null)
                {
                    output.Write("  class n");
                    Selection.WriteInt(output, node);
                    output.Write(' ');
                    output.Write(CheckClass(className, null));
                    output.Write('\n');
                }
            }
        }
        output.Write(styles);
    }

    /// <summary>
    /// Returns the text of the Mermaid flowchart, as <see cref="Write"/> writes it.
    /// </summary>
    /// <remarks>
    /// <b>Time:</b> as for <see cref="Write"/>.<br/>
    /// <b>Memory:</b> the text for the result; while it runs, up to twice as much again, and the
    /// <c>linkStyle</c> lines as for <see cref="Write"/>.
    /// </remarks>
    /// <returns>The Mermaid text.</returns>
    /// <exception cref="ArgumentException">As for <see cref="Write"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">As for <see cref="Write"/>.</exception>
    public string WriteToString()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        Write(output);
        return output.ToString();
    }

    private void WriteNode(int node, string indent, TextWriter output)
    {
        output.Write(indent);
        output.Write('n');
        Selection.WriteInt(output, node);
        string? label = _nodeLabel?.Invoke(node);
        Shape? shape = _nodeShape?.Invoke(node);
        if (label is not null || (shape is not null && shape != Shape.Rectangle))
        {
            (string open, string close) = BracketsOf(shape ?? Shape.Rectangle);
            output.Write(open);
            Quote(label ?? "n" + node.ToString(CultureInfo.InvariantCulture), output);
            output.Write(close);
        }
        output.Write('\n');
    }

    private static string CodeOf(MermaidDirection direction) => direction switch
    {
        MermaidDirection.TopDown => "TD",
        MermaidDirection.BottomUp => "BT",
        MermaidDirection.LeftRight => "LR",
        MermaidDirection.RightLeft => "RL",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "not a MermaidDirection"),
    };

    private static (string Open, string Close) BracketsOf(Shape shape) => shape switch
    {
        Shape.Rectangle => ("[", "]"),
        Shape.Rounded => ("(", ")"),
        Shape.Stadium => ("([", "])"),
        Shape.Subroutine => ("[[", "]]"),
        Shape.Cylinder => ("[(", ")]"),
        Shape.Circle => ("((", "))"),
        Shape.DoubleCircle => ("(((", ")))"),
        Shape.Rhombus => ("{", "}"),
        Shape.Hexagon => ("{{", "}}"),
        // A value the shape hook returned, not an argument of Write: no parameter to name.
        _ => throw new ArgumentOutOfRangeException(null, shape, "not a MermaidWriter.Shape"),
    };

    private static string CheckClass(string name, string? paramName)
    {
        if (!Selection.IsIdentifier(name) || Keywords.Contains(name))
        {
            throw new ArgumentException("not a Mermaid class name: " + name, paramName);
        }
        return name;
    }

    private static string CheckStyle(string style, string? paramName)
    {
        if (IsBlank(style) || style.AsSpan().IndexOfAny('\n', '\r', ';') >= 0)
        {
            throw new ArgumentException("a Mermaid style must not be blank, nor contain a line "
                + "break or semicolon: " + style, paramName);
        }
        return style;
    }

    /// <summary>
    /// Writes <paramref name="text"/> as a Mermaid quoted string.
    /// </summary>
    /// <remarks>
    /// Mermaid applies regular expressions to the whole text before parsing, strings included:
    /// <c>%%</c> starts a directive or a comment, and on a line with <c>style</c> and <c>:</c> the
    /// last <c>;</c> is dropped, which ends an entity code. Its lexer takes any line with
    /// <c>direction</c>, whitespace and a direction code for a direction statement. So <c>%</c>,
    /// <c>:</c> and whitespace right after <c>direction</c> are written as entity codes too, and an
    /// empty text, which Mermaid rejects, as one space.
    /// </remarks>
    private static void Quote(string text, TextWriter output)
    {
        if (text.Length == 0)
        {
            output.Write("\" \"");
            return;
        }
        output.Write('"');
        int plain = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            string? escaped = c switch
            {
                '"' => "#quot;",
                '#' => "#35;",
                '%' => "#37;",
                '&' => "#38;",
                ':' => "#58;",
                '`' => "#96;",
                '<' => "#lt;",
                '>' => "#gt;",
                '\n' => "<br>",
                '\r' => i + 1 < text.Length && text[i + 1] == '\n' ? "" : "<br>",
                _ when IsSpace(c) && i >= DirectionWord.Length
                    && text.AsSpan(i - DirectionWord.Length, DirectionWord.Length).SequenceEqual(DirectionWord)
                    => "#" + ((int)c).ToString(CultureInfo.InvariantCulture) + ";",
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

    /// <summary>
    /// Whether <paramref name="c"/> is whitespace, at least everything JavaScript's <c>\s</c>
    /// matches: Java's <c>Character.isWhitespace</c> or <c>isSpaceChar</c>, or U+FEFF.
    /// </summary>
    private static bool IsSpace(char c) => IsJavaWhitespace(c) || IsSpaceSeparator(c) || c == '\uFEFF';

    /// <summary>
    /// Java's <c>String.isBlank</c>: empty or only Java whitespace. Not .NET's
    /// <see cref="string.IsNullOrWhiteSpace"/>, which also counts U+0085 and the no-break spaces
    /// but not U+001C..U+001F.
    /// </summary>
    private static bool IsBlank(string text)
    {
        foreach (char c in text)
        {
            if (!IsJavaWhitespace(c))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Java's <c>Character.isWhitespace</c>: a space, line or paragraph separator other than the
    /// no-break spaces U+00A0, U+2007 and U+202F, or one of <c>\t \n \u000B \f \r</c> and
    /// U+001C..U+001F. No surrogate is whitespace, so testing chars equals testing code points.
    /// </summary>
    private static bool IsJavaWhitespace(char c) => c switch
    {
        '\t' or '\n' or '\u000B' or '\f' or '\r' or (>= '\u001C' and <= '\u001F') => true,
        '\u00A0' or '\u2007' or '\u202F' => false,
        _ => IsSpaceSeparator(c),
    };

    /// <summary>Java's <c>Character.isSpaceChar</c>: a space, line or paragraph separator.</summary>
    private static bool IsSpaceSeparator(char c) => char.GetUnicodeCategory(c) is UnicodeCategory.SpaceSeparator
        or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}
