using System.Globalization;

namespace Graphity.Dot.Tests;

/// <summary>
/// The counterpart of Java's <c>DotWriterTest</c>, with the same expected texts. A Java text block
/// ends with a line break; the raw strings here end with an empty line for it.
/// </summary>
public sealed class DotWriterTests
{
    private const StringComparison Ordinal = StringComparison.Ordinal;

    [Fact]
    public void WritesAPlainGraph()
    {
        // 0 -> 1, 0 -> 2, 2 -> 0, and 3 alone.
        string dot = DotWriter.Of(TestGraphs.Of(4, 0, 1, 0, 2, 2, 0)).WriteToString();
        Assert.Equal("""
            digraph {
              3;
              0 -> 1;
              0 -> 2;
              2 -> 0;
            }

            """, dot);
    }

    [Fact]
    public void WritesLabelsAndAttributesFromTheHooks()
    {
        string[] names = ["entry", "loop", "exit"];
        double[] weights = [1.5, 2.0, 0.25];
        string dot = DotWriter.Of(TestGraphs.Of(3, 0, 1, 1, 1, 1, 2))
            .GraphAttribute("rankdir", "LR")
            .NodeLabel(node => names[node])
            // Java's String.valueOf(double) for these values: "2.0", not .NET's "2".
            .EdgeLabel(edge => weights[edge].ToString("0.0##", CultureInfo.InvariantCulture))
            .NodeAttributes((node, attributes) =>
            {
                if (node == 0)
                {
                    attributes["shape"] = "doublecircle";
                }
            })
            .EdgeAttributes((edge, attributes) =>
            {
                if (edge == 1)
                {
                    attributes["color"] = "red";
                }
            })
            .WriteToString();
        Assert.Equal("""
            digraph {
              rankdir="LR";
              0 [label="entry", shape="doublecircle"];
              1 [label="loop"];
              2 [label="exit"];
              0 -> 1 [label="1.5"];
              1 -> 1 [label="2.0", color="red"];
              1 -> 2 [label="0.25"];
            }

            """, dot);
    }

    [Fact]
    public void EscapesQuotesBackslashesAndLineBreaks()
    {
        string dot = DotWriter.Of(TestGraphs.Isolated(1))
            .NodeLabel(node => "say \"hi\"\\\nbye")
            .WriteToString();
        Assert.Contains("0 [label=\"say \\\"hi\\\"\\\\\\nbye\"];", dot, Ordinal);
    }

    [Fact]
    public void WritesClustersInGroupOrderWithTheirLabels()
    {
        // 0 <-> 1 is one SCC, 2 and 3 are trivial; 1 -> 2 -> 3.
        var graph = TestGraphs.Of(4, 0, 1, 1, 0, 1, 2, 2, 3);
        var sccs = Components.SccsOf(graph);
        string dot = DotWriter.Of(graph)
            .Clusters(sccs, scc => sccs.SizeOf(scc) > 1 ? "SCC " + scc : null)
            .WriteToString();
        int bigScc = sccs.SccOf(0);
        Assert.Contains("  subgraph cluster_" + bigScc + " {\n    label=\"SCC " + bigScc
            + "\";\n    0;\n    1;\n  }\n", dot, Ordinal);
        Assert.Contains("  subgraph cluster_" + sccs.SccOf(3) + " {\n    3;\n  }\n", dot, Ordinal);
        Assert.EndsWith("  0 -> 1;\n  1 -> 0;\n  1 -> 2;\n  2 -> 3;\n}\n", dot, Ordinal);
    }

    [Fact]
    public void NodesInNoGroupStayOutsideTheClusters()
    {
        var graph = TestGraphs.Path(3);
        var partition = NodePartition.Of(graph, [0, -1, 0], 1);
        string dot = DotWriter.Of(graph).Clusters(partition, group => null).WriteToString();
        Assert.Equal("""
            digraph {
              subgraph cluster_0 {
                0;
                2;
              }
              0 -> 1;
              1 -> 2;
            }

            """, dot);
    }

    [Fact]
    public void TheNodeFilterDropsNodesAndTheirEdgesButKeepsTheIds()
    {
        string dot = DotWriter.Of(TestGraphs.Path(4))
            .NodeFilter(node => node != 1)
            .NodeLabel(node => "v" + node)
            .WriteToString();
        Assert.Equal("""
            digraph {
              0 [label="v0"];
              2 [label="v2"];
              3 [label="v3"];
              2 -> 3;
            }

            """, dot);
    }

    [Fact]
    public void TheUndirectedReadingWritesEachReciprocalPairOnce()
    {
        // 0 <-> 1 stored both ways, 2 -> 1 stored from the higher node only, 2 -> 2 a self-loop.
        var graph = TestGraphs.Of(3, 0, 1, 1, 0, 2, 1, 2, 2);
        var seen = new int[4];
        string dot = DotWriter.Of(graph)
            .Undirected()
            .EdgeLabel(edge =>
            {
                seen[edge]++;
                return null;
            })
            .WriteToString();
        Assert.Equal("""
            graph {
              0 -- 1;
              2 -- 1;
              2 -- 2;
            }

            """, dot);
        Assert.Equal([1, 0, 1, 1], seen);
    }

    [Fact]
    public void WritingTwiceGivesTheSameText()
    {
        var graph = RandomGraphs.Gnp(40, 0.1, 5);
        var writer = DotWriter.Of(graph).NodeLabel(node => "n" + node);
        var first = new StringWriter(CultureInfo.InvariantCulture);
        writer.Write(first);
        Assert.Equal(first.ToString(), writer.WriteToString());
        Assert.Equal(graph.EdgeCount, first.ToString().Split('\n').Count(line => line.Contains(" -> ", Ordinal)));
    }

    [Fact]
    public void RejectsInvalidAttributeNamesAndForeignPartitions()
    {
        Assert.Throws<ArgumentException>(() => DotWriter.Of(TestGraphs.Path(2)).GraphAttribute("rank dir", "LR"));
        var writer = DotWriter.Of(TestGraphs.Path(2))
            .NodeAttributes((node, attributes) => attributes["bad-name"] = "x");
        Assert.Throws<ArgumentException>(writer.WriteToString);
        var other = Components.SccsOf(TestGraphs.Path(3));
        Assert.Throws<ArgumentException>(() => DotWriter.Of(TestGraphs.Path(2)).Clusters(other, scc => null));
    }

    [Fact]
    public void AnEmptyGraph() =>
        Assert.Equal("digraph {\n}\n", DotWriter.Of(TestGraphs.Empty()).WriteToString());

    [Fact]
    public void FilterAndClustersTogether()
    {
        // Groups {0, 1} and {2}; the filter drops 2, so its cluster disappears, and drops 1.
        var graph = TestGraphs.Path(4);
        var partition = NodePartition.Of(graph, [0, 0, 1, -1], 2);
        string dot = DotWriter.Of(graph)
            .Clusters(partition, group => "g" + group)
            .NodeFilter(node => node == 0 || node == 3)
            .WriteToString();
        Assert.Equal("""
            digraph {
              subgraph cluster_0 {
                label="g0";
                0;
              }
              3;
            }

            """, dot);
    }

    [Fact]
    public void AttributesReplaceTheLabel()
    {
        string dot = DotWriter.Of(TestGraphs.Path(2))
            .EdgeLabel(edge => "plain")
            .EdgeAttributes((edge, attributes) => attributes["label"] = "replaced")
            .WriteToString();
        Assert.Contains("  0 -> 1 [label=\"replaced\"];\n", dot, Ordinal);
    }

    [Fact]
    public void AttributesRemoveTheLabel()
    {
        string dot = DotWriter.Of(TestGraphs.Path(2))
            .EdgeLabel(edge => "plain")
            .EdgeAttributes((edge, attributes) => attributes.Remove("label"))
            .WriteToString();
        Assert.Contains("  0 -> 1;\n", dot, Ordinal);
    }

    [Fact]
    public void NodeHooksRunOncePerWrittenNode()
    {
        var graph = TestGraphs.Of(3, 0, 1);
        var partition = NodePartition.Of(graph, [0, -1, -1], 1);
        var labels = new int[3];
        var hooks = new int[3];
        DotWriter.Of(graph)
            .Clusters(partition, group => null)
            .NodeLabel(node =>
            {
                labels[node]++;
                return "v" + node;
            })
            .NodeAttributes((node, attributes) => hooks[node]++)
            .WriteToString();
        Assert.Equal([1, 1, 1], labels);
        Assert.Equal([1, 1, 1], hooks);
    }

    [Theory]
    [InlineData("node")]
    [InlineData("Edge")]
    [InlineData("graph")]
    [InlineData("DIGRAPH")]
    [InlineData("subgraph")]
    [InlineData("strict")]
    public void RejectsDotKeywordsAsAttributeNames(string keyword) =>
        Assert.Throws<ArgumentException>(() => DotWriter.Of(TestGraphs.Path(2)).GraphAttribute(keyword, "x"));

    [Fact]
    public void ALoneCarriageReturnIsALineBreak()
    {
        string dot = DotWriter.Of(TestGraphs.Isolated(1)).NodeLabel(node => "a\rb\r\nc").WriteToString();
        Assert.Contains("label=\"a\\nb\\nc\"", dot, Ordinal);
    }

    [Fact]
    public void AClusterWithoutLabelDoesNotInheritTheGraphLabel()
    {
        var graph = TestGraphs.Path(3);
        var partition = NodePartition.Of(graph, [0, 1, -1], 2);
        string dot = DotWriter.Of(graph)
            .GraphAttribute("label", "TITLE")
            .Clusters(partition, group => group == 1 ? "own" : null)
            .WriteToString();
        Assert.Equal("""
            digraph {
              label="TITLE";
              subgraph cluster_0 {
                label="";
                0;
              }
              subgraph cluster_1 {
                label="own";
                1;
              }
              0 -> 1;
              1 -> 2;
            }

            """, dot);
    }

    [Fact]
    public void LabelsEscapeAmpersandsSinceGraphvizDecodesEntitiesThere()
    {
        var graph = TestGraphs.Path(2);
        var partition = NodePartition.Of(graph, [0, -1], 1);
        string dot = DotWriter.Of(graph)
            .GraphAttribute("label", "A & B")
            .GraphAttribute("URL", "x?a=1&b=2")
            .Clusters(partition, group => "&lt;")
            .NodeLabel(node => "p = &sum;")
            .EdgeAttributes((edge, attributes) =>
            {
                attributes["xlabel"] = "&";
                attributes["headlabel"] = "&amp;";
                attributes["taillabel"] = "&#38;";
                attributes["URL"] = "y?c=3&d=4";
            })
            .WriteToString();
        Assert.Equal("""
            digraph {
              label="A &amp; B";
              URL="x?a=1&b=2";
              subgraph cluster_0 {
                label="&amp;lt;";
                0 [label="p = &amp;sum;"];
              }
              1 [label="p = &amp;sum;"];
              0 -> 1 [xlabel="&amp;", headlabel="&amp;amp;", taillabel="&amp;#38;", URL="y?c=3&d=4"];
            }

            """, dot);
    }

    [Fact]
    public void ANulCharacterIsWrittenAsTheReplacementCharacter()
    {
        string dot = DotWriter.Of(TestGraphs.Isolated(1))
            .NodeLabel(node => "a\u0000b")
            .NodeAttributes((node, attributes) => attributes["URL"] = "\u0000")
            .WriteToString();
        Assert.Contains("0 [label=\"a\uFFFDb\", URL=\"\uFFFD\"];", dot, Ordinal);
    }

    // The C# surface: TextWriter, the ordered dictionary of the hook, null and the keyword case.

    [Fact]
    public void WriteAppendsToATextWriterWithoutClosingIt()
    {
        var writer = DotWriter.Of(TestGraphs.Path(2));
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        output.Write("before\n");
        writer.Write(output);
        output.Write("after");
        Assert.Equal("before\n" + writer.WriteToString() + "after", output.ToString());
    }

    [Fact]
    public void TheHookDictionaryKeepsInsertionOrderAndThePlaceOfAReplacedName()
    {
        string dot = DotWriter.Of(TestGraphs.Isolated(1))
            .NodeLabel(node => "first")
            .NodeAttributes((node, attributes) =>
            {
                attributes["z"] = "1";
                attributes["a"] = "2";
                attributes["m"] = "3";
                attributes["label"] = "second";
                attributes.Remove("a");
                attributes["a"] = "4";
            })
            .WriteToString();
        Assert.Equal("digraph {\n  0 [label=\"second\", z=\"1\", m=\"3\", a=\"4\"];\n}\n", dot);
    }

    [Fact]
    public void TheHookDictionaryHoldsOnlyTheLabelAndIsClearedForEveryElement()
    {
        var seen = new List<string>();
        DotWriter.Of(TestGraphs.Path(3))
            .NodeLabel(node => node == 1 ? null : "v" + node)
            .NodeAttributes((node, attributes) =>
            {
                seen.Add(node + ":" + string.Join(",", attributes.Select(a => a.Key + "=" + a.Value)));
                attributes["mark"] = "x";
            })
            .EdgeAttributes((edge, attributes) => seen.Add("e" + edge + ":" + attributes.Count))
            .WriteToString();
        Assert.Equal(["0:label=v0", "1:", "2:label=v2", "e0:0", "e1:0"], seen);
    }

    [Fact]
    public void SettingAGraphAttributeAgainReplacesItsValueInPlace()
    {
        string dot = DotWriter.Of(TestGraphs.Empty())
            .GraphAttribute("rankdir", "LR")
            .GraphAttribute("label", "x")
            .GraphAttribute("rankdir", "TB")
            .WriteToString();
        Assert.Equal("digraph {\n  rankdir=\"TB\";\n  label=\"x\";\n}\n", dot);
    }

    [Theory]
    [InlineData("_")]
    [InlineData("a1")]
    [InlineData("_9")]
    [InlineData("Node1")]
    [InlineData("nodes")]
    [InlineData("STRICTLY")]
    public void AcceptsIdentifiersThatAreNoKeyword(string name) =>
        Assert.Equal("digraph {\n  " + name + "=\"v\";\n}\n",
            DotWriter.Of(TestGraphs.Empty()).GraphAttribute(name, "v").WriteToString());

    [Theory]
    [InlineData("")]
    [InlineData("1a")]
    [InlineData("a-b")]
    [InlineData("\u00E4")]
    [InlineData("STRICT")]
    [InlineData("Subgraph")]
    public void RejectsNamesThatAreNoIdentifierOrAKeywordInAnyCase(string name)
    {
        var e = Assert.Throws<ArgumentException>(() => DotWriter.Of(TestGraphs.Empty()).GraphAttribute(name, "v"));
        Assert.StartsWith("not a DOT attribute name: " + name, e.Message, Ordinal);
        var hooked = DotWriter.Of(TestGraphs.Isolated(1))
            .NodeAttributes((node, attributes) => attributes[name] = "v");
        Assert.Throws<ArgumentException>(hooked.WriteToString);
    }

    [Fact]
    public void TheLabelAttributesAreMatchedCaseSensitively()
    {
        string dot = DotWriter.Of(TestGraphs.Isolated(1))
            .NodeAttributes((node, attributes) =>
            {
                attributes["Label"] = "&";
                attributes["taillabel"] = "&";
            })
            .WriteToString();
        Assert.Contains("0 [Label=\"&\", taillabel=\"&amp;\"];", dot, Ordinal);
    }

    [Fact]
    public void WhatWasWrittenBeforeAnInvalidNameStaysInTheOutput()
    {
        var writer = DotWriter.Of(TestGraphs.Path(2))
            .EdgeAttributes((edge, attributes) => attributes["edge"] = "x");
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Throws<ArgumentException>(() => writer.Write(output));
        Assert.Equal("digraph {\n  0 -> 1", output.ToString());
    }

    [Fact]
    public void RejectsNullArgumentsAndNullValues()
    {
        var graph = TestGraphs.Path(2);
        Assert.Throws<ArgumentNullException>(() => DotWriter.Of(null!));
        var writer = DotWriter.Of(graph);
        Assert.Throws<ArgumentNullException>(() => writer.GraphAttribute(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => writer.GraphAttribute("x", null!));
        Assert.Throws<ArgumentNullException>(() => writer.NodeLabel(null!));
        Assert.Throws<ArgumentNullException>(() => writer.EdgeLabel(null!));
        Assert.Throws<ArgumentNullException>(() => writer.NodeAttributes(null!));
        Assert.Throws<ArgumentNullException>(() => writer.EdgeAttributes(null!));
        Assert.Throws<ArgumentNullException>(() => writer.Clusters(null!, group => null));
        Assert.Throws<ArgumentNullException>(() => writer.Clusters(Components.SccsOf(graph), null!));
        Assert.Throws<ArgumentNullException>(() => writer.NodeFilter(null!));
        Assert.Throws<ArgumentNullException>(() => writer.Write(null!));
        writer.NodeAttributes((node, attributes) => attributes["URL"] = null!);
        Assert.Throws<ArgumentException>(writer.WriteToString);
    }
}
