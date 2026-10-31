# Graphity.All

Graphity for .NET in one package: a graph library which keeps graphs in a compact, immutable form and provides algorithms on top of it, for code analysis and compiler pipelines and wherever else you build a graph from your domain model, run analyses on it, and use the results.

This package has no code of its own. It brings in the four packages of [Graphity for .NET](https://github.com/kaufco/graphity-csharp), each at least at the version of this package:

| Package | Namespace | What it adds |
|---|---|---|
| [`Graphity.Core`](https://www.nuget.org/packages/Graphity.Core) | `Graphity` | The graph, the builder, and every algorithm that applies to any directed graph, dominators included |
| [`Graphity.ControlFlow`](https://www.nuget.org/packages/Graphity.ControlFlow) | `Graphity.ControlFlow` | The analyses of control flow: loops, dominance frontiers, control dependence and the placement of phi-nodes |
| [`Graphity.Dot`](https://www.nuget.org/packages/Graphity.Dot) | `Graphity.Dot` | A writer for DOT (the language of Graphviz) |
| [`Graphity.Mermaid`](https://www.nuget.org/packages/Graphity.Mermaid) | `Graphity.Mermaid` | A writer for Mermaid flowcharts, which render in Markdown |

To take only what you need, add those packages themselves instead, at the same version. The readme of each package gives an overview of its namespace with an example, and the [README of the repository](https://github.com/kaufco/graphity-csharp#readme) a first program and the differences from Graphity for Java, whose API of the same version these packages implement.

No parameter, return value or element in these packages is `null` unless its type is nullable; a `null` argument where the type does not allow one throws `ArgumentNullException`. The XML documentation of every type and member states its contract: what it returns and throws, and its cost in time and memory.

Graphity is licensed under the [Apache License, Version 2.0](https://github.com/kaufco/graphity-csharp/blob/main/LICENSE); see also its [NOTICE](https://github.com/kaufco/graphity-csharp/blob/main/NOTICE).
