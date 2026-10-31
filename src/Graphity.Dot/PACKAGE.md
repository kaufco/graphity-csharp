# Graphity.Dot

Export of graphs to DOT (the language of Graphviz) to look at them with its layout tools.

This package of [Graphity for .NET](https://github.com/kaufco/graphity-csharp) has the namespace `Graphity.Dot`. It builds on `Graphity.Core` and is released with it at the same version. `DotWriter` writes a graph as DOT text. A graph holds no data, so the writer takes labels and attributes from hooks that receive a node ID or an edge index and read the caller's arrays. `AttributeHook` is the hook for the attributes of one node or edge. The clusters come from a partition of the nodes, with a hook for their labels. Here, the packages of a small program that depend on each other in a cycle:

<!-- example: WritingDot.cs#cycle -->
```csharp
using Graphity.Dot;

// The packages are the nodes of dependencies, names[node] is the name of a
// package, and sccs holds the strongly connected components of dependencies.

// Write the packages that lie on a cycle in DOT, each labeled with its name.
string dot = DotWriter.Of(dependencies)
    .NodeLabel(node => names[node])
    .NodeFilter(node => sccs.ContainsCycle(sccs.SccOf(node)))
    .WriteToString();
// digraph {
//   4 [label="model"];
//   5 [label="storage"];
//   4 -> 5;
//   5 -> 4;
// }
```

The node filter keeps the node IDs of the graph, so the hooks read the same arrays as for the whole graph. For Markdown and documentation, the package `Graphity.Mermaid` writes Mermaid flowcharts.

No parameter, return value or element in this package is `null` unless its type is nullable; a `null` argument where the type does not allow one throws `ArgumentNullException`. The XML documentation of every type and member states its contract: what it returns and throws, and its cost in time and memory.
