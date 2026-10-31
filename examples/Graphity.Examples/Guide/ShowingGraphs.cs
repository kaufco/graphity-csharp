using Graphity;
using Graphity.Dot;
using Graphity.Mermaid;

namespace Examples.Guide;

/// <summary>
/// Graphs written as text, in DOT for Graphviz and as Mermaid flowcharts.
/// </summary>
internal static class ShowingGraphs
{
    /// <summary>The two text forms of a graph.</summary>
    internal sealed record TextResult(string Full, string Brief);

    /// <summary>
    /// Returns the steps of the build as an adjacency list and in their short form.
    /// </summary>
    internal static TextResult Text()
    {
        Graph steps = SmallProgram.BuildSteps();
        // @start region="text"
        string full = Graphs.AdjacencyListOf(steps);   // every node, one line each:
        // 0 -> [1]
        // 1 -> [3, 4, 6]
        // 2 -> [6]
        // 3 -> [5]
        // 4 -> [8]
        // 5 -> [6]
        // 6 -> [7]
        // 7 -> [8]
        // 8 -> []

        // one line, for logs and debuggers:
        string brief = steps.ToString();
        // Graph[nodes=9, edges=10] {0 -> [1], 1 -> [3, 4, 6], 2 -> [6], 3 -> [5],
        //   4 -> [8], 5 -> [6], 6 -> [7], 7 -> [8], 8 -> []}
        // @end
        return new TextResult(full, brief);
    }

    /// <summary>Returns the packages in DOT, labeled, from left to right.</summary>
    internal static string Dot()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        // @start region="dot"
        DotWriter writer = DotWriter.Of(dependencies)
            .GraphAttribute("rankdir", "LR")
            .NodeLabel(node => names[node]);
        using var output = new StringWriter();   // or a file, through a StreamWriter
        writer.Write(output);
        string dot = output.ToString();
        // digraph {
        //   rankdir="LR";
        //   0 [label="app"];
        //   ...
        //   0 -> 1;
        //   ...
        // }
        // @end
        return dot;
    }

    /// <summary>
    /// Returns the packages in DOT, with the cycle framed and its back edge dashed.
    /// </summary>
    internal static string DotStyled()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        // @start region="dotStyled"
        // Frame the SCCs with a cycle: a partition in which every other package is in no
        // group (-1).
        Sccs sccs = Components.SccsOf(dependencies);
        int[] frameOf = new int[dependencies.NodeCount];
        for (int node = 0; node < frameOf.Length; node++)
        {
            frameOf[node] = sccs.ContainsCycle(sccs.SccOf(node)) ? sccs.SccOf(node) : -1;
        }
        NodePartition frames = NodePartition.Of(dependencies, frameOf, sccs.SccCount);
        EdgeTypes edgeTypes = EdgeTypes.Of(dependencies);
        string dot = DotWriter.Of(dependencies)
            .NodeLabel(node => names[node])
            .Clusters(frames, scc => $"SCC {scc}")
            // Draw the edges that close a cycle dashed and red.
            .EdgeAttributes((edge, attributes) =>
            {
                if (edgeTypes.TypeOf(edge) == EdgeType.Back)
                {
                    attributes["style"] = "dashed";
                    attributes["color"] = "red";
                }
            })
            // Leave out util, the package of helpers: four packages use it, and its edges
            // only crowd the picture.
            .NodeFilter(node => names[node] != "util")
            .WriteToString();
        // @end
        return dot;
    }

    /// <summary>Returns the steps of the build as a Mermaid flowchart.</summary>
    internal static string Mermaid()
    {
        Graph steps = SmallProgram.BuildSteps();
        string[] names = SmallProgram.StepNames();
        // @start region="mermaid"
        string mermaid = MermaidWriter.Of(steps)
            .Direction(MermaidDirection.LeftRight)
            .NodeLabel(step => names[step])
            .NodeShape(step => names[step] == "publish"
                ? MermaidWriter.Shape.Stadium : null)
            .WriteToString();
        // flowchart LR
        //   n0["generate"]
        //   ...
        //   n8(["publish"])
        //   n0 --> n1
        //   ...
        // @end
        return mermaid;
    }

    /// <summary>
    /// Returns the rules of the grammar as a Mermaid flowchart with their SCCs framed.
    /// </summary>
    internal static string MermaidStyled()
    {
        Graph rules = SmallGrammar.Rules();
        string[] names = SmallGrammar.RuleNames();
        // @start region="mermaidStyled"
        // Frame the SCCs with a cycle: a partition in which every other rule is in no
        // group (-1).
        Sccs sccs = Components.SccsOf(rules);
        int[] frameOf = new int[rules.NodeCount];
        for (int rule = 0; rule < frameOf.Length; rule++)
        {
            frameOf[rule] = sccs.ContainsCycle(sccs.SccOf(rule)) ? sccs.SccOf(rule) : -1;
        }
        NodePartition frames = NodePartition.Of(rules, frameOf, sccs.SccCount);
        string mermaid = MermaidWriter.Of(rules)
            .NodeLabel(rule => $"{rule}: {names[rule]}")
            .Clusters(frames, scc => $"SCC {scc}")
            // Outline program, the rule where a parser starts.
            .ClassDef("focus", "stroke:#8b5cf6,stroke-width:3px")
            .NodeClass(rule => names[rule] == "program" ? "focus" : null)
            // Draw the edges within an SCC, the edges that lie on a cycle, in orange.
            .EdgeStyle(edge =>
                sccs.SccOf(rules.EdgeSource(edge)) == sccs.SccOf(rules.EdgeTarget(edge))
                    ? "stroke:#f59e0b,stroke-width:3px" : null)
            .WriteToString();
        // @end
        return mermaid;
    }
}
