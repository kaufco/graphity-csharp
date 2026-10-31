using System.Globalization;

namespace Graphity.Mermaid.Tests;

/// <summary>
/// The counterpart of Java's <c>MermaidWriterTest</c>, with the same expected texts. A Java text
/// block ends with a line break; the raw strings here end with an empty line for it.
/// </summary>
public sealed class MermaidWriterTests
{
    private const StringComparison Ordinal = StringComparison.Ordinal;

    [Fact]
    public void WritesAPlainFlowchart()
    {
        string text = MermaidWriter.Of(TestGraphs.Of(3, 0, 1, 0, 2, 2, 0)).WriteToString();
        Assert.Equal("""
            flowchart TD
              n0
              n1
              n2
              n0 --> n1
              n0 --> n2
              n2 --> n0

            """, text);
    }

    [Fact]
    public void WritesLabelsShapesClassesAndEdgeStyles()
    {
        string[] names = ["entry", "loop", "exit"];
        string text = MermaidWriter.Of(TestGraphs.Of(3, 0, 1, 1, 1, 1, 2))
            .Direction(MermaidDirection.LeftRight)
            .NodeLabel(node => names[node])
            .EdgeLabel(edge => edge == 1 ? "again" : null)
            .NodeShape(node => node == 0 ? MermaidWriter.Shape.Circle : null)
            .ClassDef("hot", "fill:#f96,stroke:#333")
            .NodeClass(node => node == 1 ? "hot" : null)
            .EdgeStyle(edge => edge == 1 ? "stroke:red" : null)
            .WriteToString();
        Assert.Equal("""
            flowchart LR
              classDef hot fill:#f96,stroke:#333
              n0(("entry"))
              n1["loop"]
              n2["exit"]
              n0 --> n1
              n1 -->|"again"| n1
              n1 --> n2
              class n1 hot
              linkStyle 1 stroke:red

            """, text);
    }

    [Fact]
    public void EdgeStylesFollowTheWrittenLinksWhenAFilterDropsEdges()
    {
        // 0 -> 1, 0 -> 2, 1 -> 2; without node 1 only 0 -> 2 (edge 1) remains, Mermaid's link 0.
        string text = MermaidWriter.Of(TestGraphs.Of(3, 0, 1, 0, 2, 1, 2))
            .NodeFilter(node => node != 1)
            .EdgeStyle(edge => "stroke-width:" + (edge + 1) + "px")
            .WriteToString();
        Assert.Equal("""
            flowchart TD
              n0
              n2
              n0 --> n2
              linkStyle 0 stroke-width:2px

            """, text);
    }

    [Fact]
    public void EscapesQuotesHashesAnglesAndLineBreaks()
    {
        string text = MermaidWriter.Of(TestGraphs.Isolated(1))
            .NodeLabel(node => "a \"b\" #1 <c>\nd")
            .WriteToString();
        Assert.Contains("n0[\"a #quot;b#quot; #35;1 #lt;c#gt;<br>d\"]", text, Ordinal);
    }

    [Fact]
    public void WritesSubgraphsAndTheUndirectedReading()
    {
        var graph = TestGraphs.Of(3, 0, 1, 1, 0, 1, 2);
        var sccs = Components.SccsOf(graph);
        string text = MermaidWriter.Of(graph)
            .Undirected()
            .Clusters(sccs, scc => sccs.SizeOf(scc) > 1 ? "cycle" : null)
            .WriteToString();
        int big = sccs.SccOf(0);
        Assert.Contains("  subgraph c" + big + "[\"cycle\"]\n    n0\n    n1\n  end\n", text, Ordinal);
        Assert.Contains("  subgraph c" + sccs.SccOf(2) + "\n    n2\n  end\n", text, Ordinal);
        Assert.EndsWith("  n0 --- n1\n  n1 --- n2\n", text, Ordinal);
    }

    [Fact]
    public void RejectsInvalidClassNamesAndStyles()
    {
        var writer = MermaidWriter.Of(TestGraphs.Path(2));
        Assert.Throws<ArgumentException>(() => writer.ClassDef("1st", "fill:red"));
        Assert.Throws<ArgumentException>(() => writer.ClassDef("ok", "fill:red; x"));
        var badClass = MermaidWriter.Of(TestGraphs.Path(2)).NodeClass(node => "a b");
        Assert.Throws<ArgumentException>(badClass.WriteToString);
        var badStyle = MermaidWriter.Of(TestGraphs.Path(2)).EdgeStyle(edge => "a\nb");
        Assert.Throws<ArgumentException>(badStyle.WriteToString);
    }

    [Fact]
    public void AnEmptyGraph() =>
        Assert.Equal("flowchart TD\n", MermaidWriter.Of(TestGraphs.Empty()).WriteToString());

    [Fact]
    public void EveryShapeHasItsBrackets()
    {
        MermaidWriter.Shape[] shapes = Enum.GetValues<MermaidWriter.Shape>();
        string text = MermaidWriter.Of(TestGraphs.Isolated(shapes.Length))
            .NodeShape(node => shapes[node])
            .WriteToString();
        Assert.Equal("""
            flowchart TD
              n0
              n1("n1")
              n2(["n2"])
              n3[["n3"]]
              n4[("n4")]
              n5(("n5"))
              n6((("n6")))
              n7{"n7"}
              n8{{"n8"}}

            """, text);
    }

    [Fact]
    public void UndirectedLinksWithLabelsAndStylesCountOnlyWrittenLinks()
    {
        // 0 <-> 1 stored both ways (edges 0 and 1), 1 -> 2 (edge 2): two links are written.
        string text = MermaidWriter.Of(TestGraphs.Of(3, 0, 1, 1, 0, 1, 2))
            .Undirected()
            .EdgeLabel(edge => "e" + edge)
            .EdgeStyle(edge => edge == 2 ? "stroke:blue" : null)
            .WriteToString();
        Assert.EndsWith("""
              n0 ---|"e0"| n1
              n1 ---|"e2"| n2
              linkStyle 1 stroke:blue

            """, text, Ordinal);
    }

    [Fact]
    public void FilterAndClustersTogether()
    {
        var graph = TestGraphs.Path(4);
        var partition = NodePartition.Of(graph, [0, 0, 1, -1], 2);
        string text = MermaidWriter.Of(graph)
            .Clusters(partition, group => null)
            .NodeFilter(node => node == 0 || node == 3)
            .NodeClass(node => "c")
            .ClassDef("c", "fill:red")
            .WriteToString();
        Assert.Equal("""
            flowchart TD
              classDef c fill:red
              subgraph c0
                n0
              end
              n3
              class n0 c
              class n3 c

            """, text);
    }

    [Fact]
    public void EscapesAmpersandsAndBackticks()
    {
        string text = MermaidWriter.Of(TestGraphs.Isolated(1))
            .NodeLabel(node => "`**x**` &lt;")
            .WriteToString();
        Assert.Contains("n0[\"#96;**x**#96; #38;lt;\"]", text, Ordinal);
    }

    [Fact]
    public void RejectsReservedAndHyphenatedClassNamesEmptyStylesAndForeignPartitions()
    {
        var writer = MermaidWriter.Of(TestGraphs.Path(2));
        Assert.Throws<ArgumentException>(() => writer.ClassDef("end", "fill:red"));
        Assert.Throws<ArgumentException>(() => writer.ClassDef("a--b", "fill:red"));
        var e = Assert.Throws<ArgumentException>(() => writer.ClassDef("ok", " "));
        Assert.Contains("must not be blank", e.Message, Ordinal);
        Assert.Throws<ArgumentException>(() => writer.Clusters(Components.SccsOf(TestGraphs.Path(3)), scc => null));
    }

    [Fact]
    public void AnEmptyLabelIsWrittenAsOneSpace()
    {
        var graph = TestGraphs.Path(2);
        var partition = NodePartition.Of(graph, [0, -1], 1);
        string text = MermaidWriter.Of(graph)
            .NodeLabel(node => "")
            .NodeShape(node => node == 1 ? MermaidWriter.Shape.Circle : null)
            .EdgeLabel(edge => "")
            .Clusters(partition, group => "")
            .WriteToString();
        Assert.Equal("""
            flowchart TD
              subgraph c0[" "]
                n0[" "]
              end
              n1((" "))
              n0 -->|" "| n1

            """, text);
    }

    [Theory]
    [InlineData("style")]
    [InlineData("linkStyle")]
    [InlineData("interpolate")]
    [InlineData("classDef")]
    [InlineData("class")]
    [InlineData("href")]
    [InlineData("click")]
    [InlineData("call")]
    [InlineData("graph")]
    [InlineData("flowchart")]
    [InlineData("subgraph")]
    [InlineData("end")]
    [InlineData("_self")]
    [InlineData("_blank")]
    [InlineData("_parent")]
    [InlineData("_top")]
    public void RejectsEveryMermaidKeywordAsAClassName(string keyword)
    {
        Assert.Throws<ArgumentException>(() => MermaidWriter.Of(TestGraphs.Path(2)).ClassDef(keyword, "fill:red"));
        var writer = MermaidWriter.Of(TestGraphs.Path(2)).NodeClass(node => keyword);
        Assert.Throws<ArgumentException>(writer.WriteToString);
    }

    [Fact]
    public void KeywordsAreCaseSensitiveAndOnlyWhole()
    {
        string text = MermaidWriter.Of(TestGraphs.Isolated(1))
            .ClassDef("End", "fill:red")
            .ClassDef("STYLE", "fill:blue")
            .ClassDef("styled", "fill:green")
            .ClassDef("_topLevel", "fill:gray")
            .NodeClass(node => "End")
            .WriteToString();
        Assert.Equal("""
            flowchart TD
              classDef End fill:red
              classDef STYLE fill:blue
              classDef styled fill:green
              classDef _topLevel fill:gray
              n0
              class n0 End

            """, text);
    }

    [Fact]
    public void EscapesPercentSignsAndColonsThatMermaidWouldPreprocess()
    {
        string text = MermaidWriter.Of(TestGraphs.Path(2))
            .NodeLabel(node => node == 0 ? "50%%{init: {'theme':'dark'}}%% done" : "style:\"bold\";")
            .EdgeLabel(edge => "100%")
            .WriteToString();
        Assert.Equal("""
            flowchart TD
              n0["50#37;#37;{init#58; {'theme'#58;'dark'}}#37;#37; done"]
              n1["style#58;#quot;bold#quot;;"]
              n0 -->|"100#37;"| n1

            """, text);
    }

    [Fact]
    public void EscapesWhitespaceAfterTheWordDirection()
    {
        string[] labels = ["direction LR", "go direction\tTB", "Direction LR", "directions LR"];
        string text = MermaidWriter.Of(TestGraphs.Isolated(labels.Length))
            .NodeLabel(node => labels[node])
            .WriteToString();
        Assert.Equal("""
            flowchart TD
              n0["direction#32;LR"]
              n1["go direction#9;TB"]
              n2["Direction LR"]
              n3["directions LR"]

            """, text);
    }

    // The C# surface and Java's character classes; the expected texts are those of the Java writer.

    [Theory]
    [InlineData(MermaidDirection.TopDown, "TD")]
    [InlineData(MermaidDirection.BottomUp, "BT")]
    [InlineData(MermaidDirection.LeftRight, "LR")]
    [InlineData(MermaidDirection.RightLeft, "RL")]
    public void EveryDirectionHasItsCode(MermaidDirection direction, string code) =>
        Assert.Equal("flowchart " + code + "\n", MermaidWriter.Of(TestGraphs.Empty()).Direction(direction).WriteToString());

    [Theory]
    [InlineData("direction\u00A0x", "direction#160;x")]
    [InlineData("direction\u2007x", "direction#8199;x")]
    [InlineData("direction\u202Fx", "direction#8239;x")]
    [InlineData("direction\u3000x", "direction#12288;x")]
    [InlineData("direction\u1680x", "direction#5760;x")]
    [InlineData("direction\u2028x", "direction#8232;x")]
    [InlineData("direction\u2029x", "direction#8233;x")]
    [InlineData("direction\uFEFFx", "direction#65279;x")]
    [InlineData("direction\u000Bx", "direction#11;x")]
    [InlineData("direction\fx", "direction#12;x")]
    [InlineData("direction\u001Cx", "direction#28;x")]
    [InlineData("direction\u001Fx", "direction#31;x")]
    [InlineData("direction\u0085x", "direction\u0085x")]
    [InlineData("direction\u200Bx", "direction\u200Bx")]
    [InlineData("direction\u180Ex", "direction\u180Ex")]
    [InlineData("direction\r\nx", "direction<br>x")]
    [InlineData("direction\rx", "direction<br>x")]
    [InlineData("xdirection  y", "xdirection#32; y")]
    [InlineData("direction\u00A0", "direction#160;")]
    [InlineData("direction", "direction")]
    public void WhitespaceAfterDirectionFollowsJavasCharacterClasses(string label, string expected) =>
        Assert.Equal("flowchart TD\n  n0[\"" + expected + "\"]\n",
            MermaidWriter.Of(TestGraphs.Isolated(1)).NodeLabel(node => label).WriteToString());

    [Theory]
    [InlineData("\u00A0")]
    [InlineData("\u2007")]
    [InlineData("\u202F")]
    [InlineData("\u0085")]
    public void AStyleOfNoBreakSpacesIsNotBlankAsInJava(string style) =>
        Assert.Equal("flowchart TD\n  classDef ok " + style + "\n",
            MermaidWriter.Of(TestGraphs.Empty()).ClassDef("ok", style).WriteToString());

    [Theory]
    [InlineData("")]
    [InlineData("\t")]
    [InlineData("\u000B")]
    [InlineData("\u001C")]
    [InlineData("\u001F")]
    [InlineData("\u2003")]
    [InlineData("\t\u3000")]
    [InlineData("\u2028")]
    [InlineData("\r")]
    [InlineData("a;b")]
    public void RejectsBlankStylesAsJavaDefinesThem(string style)
    {
        Assert.Throws<ArgumentException>(() => MermaidWriter.Of(TestGraphs.Empty()).ClassDef("ok", style));
        var writer = MermaidWriter.Of(TestGraphs.Path(2)).EdgeStyle(edge => style);
        Assert.Throws<ArgumentException>(writer.WriteToString);
    }

    [Fact]
    public void DefiningAClassAgainReplacesItsStyleInPlace()
    {
        string text = MermaidWriter.Of(TestGraphs.Empty())
            .ClassDef("a", "fill:red")
            .ClassDef("b", "fill:blue")
            .ClassDef("a", "fill:green")
            .WriteToString();
        Assert.Equal("flowchart TD\n  classDef a fill:green\n  classDef b fill:blue\n", text);
    }

    [Fact]
    public void ARectangleWithoutLabelIsTheBareName()
    {
        string text = MermaidWriter.Of(TestGraphs.Isolated(2))
            .NodeShape(node => MermaidWriter.Shape.Rectangle)
            .NodeLabel(node => node == 0 ? null : "x")
            .WriteToString();
        Assert.Equal("flowchart TD\n  n0\n  n1[\"x\"]\n", text);
    }

    [Fact]
    public void WriteAppendsToATextWriterWithoutClosingIt()
    {
        var writer = MermaidWriter.Of(TestGraphs.Path(2));
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        output.Write("before\n");
        writer.Write(output);
        output.Write("after");
        Assert.Equal("before\n" + writer.WriteToString() + "after", output.ToString());
    }

    [Fact]
    public void WhatWasWrittenBeforeAnInvalidClassStaysInTheOutputButNoLinkStyle()
    {
        var writer = MermaidWriter.Of(TestGraphs.Path(2))
            .EdgeStyle(edge => "stroke:red")
            .NodeClass(node => node == 1 ? "end" : null);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var e = Assert.Throws<ArgumentException>(() => writer.Write(output));
        Assert.Equal("not a Mermaid class name: end", e.Message);
        Assert.Equal("flowchart TD\n  n0\n  n1\n  n0 --> n1\n  class n1 ", output.ToString());
    }

    [Fact]
    public void HooksAreCalledOncePerWrittenElement()
    {
        var graph = TestGraphs.Of(3, 0, 1, 1, 0, 1, 2);
        var partition = NodePartition.Of(graph, [0, 0, -1], 1);
        var calls = new List<string>();
        MermaidWriter.Of(graph)
            .Undirected()
            .Clusters(partition, group =>
            {
                calls.Add("cluster" + group);
                return null;
            })
            .NodeLabel(node =>
            {
                calls.Add("label" + node);
                return null;
            })
            .NodeShape(node =>
            {
                calls.Add("shape" + node);
                return null;
            })
            .EdgeLabel(edge =>
            {
                calls.Add("edgeLabel" + edge);
                return null;
            })
            .EdgeStyle(edge =>
            {
                calls.Add("style" + edge);
                return null;
            })
            .NodeClass(node =>
            {
                calls.Add("class" + node);
                return null;
            })
            .WriteToString();
        Assert.Equal(
            ["cluster0", "label0", "shape0", "label1", "shape1", "label2", "shape2",
                "edgeLabel0", "style0", "edgeLabel2", "style2", "class0", "class1", "class2"],
            calls);
    }

    [Fact]
    public void RejectsUndefinedEnumValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MermaidWriter.Of(TestGraphs.Empty()).Direction((MermaidDirection)4));
        var writer = MermaidWriter.Of(TestGraphs.Isolated(1)).NodeShape(node => (MermaidWriter.Shape)9);
        // The hook returned the value; no parameter of WriteToString is at fault.
        Assert.Null(Assert.Throws<ArgumentOutOfRangeException>(writer.WriteToString).ParamName);
    }

    [Fact]
    public void RejectsNullArguments()
    {
        var graph = TestGraphs.Path(2);
        Assert.Throws<ArgumentNullException>(() => MermaidWriter.Of(null!));
        var writer = MermaidWriter.Of(graph);
        Assert.Throws<ArgumentNullException>(() => writer.NodeLabel(null!));
        Assert.Throws<ArgumentNullException>(() => writer.EdgeLabel(null!));
        Assert.Throws<ArgumentNullException>(() => writer.NodeShape(null!));
        Assert.Throws<ArgumentNullException>(() => writer.ClassDef(null!, "fill:red"));
        Assert.Throws<ArgumentNullException>(() => writer.ClassDef("ok", null!));
        Assert.Throws<ArgumentNullException>(() => writer.NodeClass(null!));
        Assert.Throws<ArgumentNullException>(() => writer.EdgeStyle(null!));
        Assert.Throws<ArgumentNullException>(() => writer.Clusters(null!, group => null));
        Assert.Throws<ArgumentNullException>(() => writer.Clusters(Components.SccsOf(graph), null!));
        Assert.Throws<ArgumentNullException>(() => writer.NodeFilter(null!));
        Assert.Throws<ArgumentNullException>(() => writer.Write(null!));
    }
}
