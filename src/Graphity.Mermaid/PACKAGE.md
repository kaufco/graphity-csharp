# Graphity.Mermaid

Export of graphs to Mermaid flowcharts, which render directly in Markdown, in issues and in documentation.

This package of [Graphity for .NET](https://github.com/kaufco/graphity-csharp) has the namespace `Graphity.Mermaid`. It builds on `Graphity.Core` and is released with it at the same version. `MermaidWriter` writes a graph as the text of a Mermaid flowchart, in the direction of choice (`MermaidDirection`) and with node shapes (`MermaidWriter.Shape`). A graph holds no data, so the writer takes labels, shapes, classes and styles from hooks that receive a node ID or an edge index and read the caller's arrays. The clusters come from a partition of the nodes, with a hook for their labels. Here, the packages of a small program that depend on each other in a cycle:

<!-- example: WritingMermaid.cs#cycle -->
```csharp
using Graphity.Mermaid;

// The packages are the nodes of dependencies, names[node] is the name of a
// package, and sccs holds the strongly connected components of dependencies.

// Write the packages that lie on a cycle as a Mermaid flowchart, each labeled
// with its name.
string mermaid = MermaidWriter.Of(dependencies)
    .NodeLabel(node => names[node])
    .NodeFilter(node => sccs.ContainsCycle(sccs.SccOf(node)))
    .WriteToString();
// flowchart TD
//   n4["model"]
//   n5["storage"]
//   n4 --> n5
//   n5 --> n4
```

The node filter keeps the node IDs of the graph, so the hooks read the same arrays as for the whole graph. Mermaid renders graphs of a few hundred nodes well; for larger ones, the package `Graphity.Dot` writes DOT (the language of Graphviz).

No parameter, return value or element in this package is `null` unless its type is nullable; a `null` argument where the type does not allow one throws `ArgumentNullException`. The XML documentation of every type and member states its contract: what it returns and throws, and its cost in time and memory.
