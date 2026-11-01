# Changelog

All notable changes to Graphity for .NET are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Graphity for .NET adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Its
version is that of the Java library whose API it implements: the same version number means the
same features as in [Graphity](https://github.com/kaufco/graphity) for Java.

---

## [3.0.0] — 2026-11-01

This is the first release of Graphity in C#. It implements the API of Graphity 3.0.0 for Java in
the conventions of .NET: the same packages, types and methods, the same algorithms and data
layouts, the same limits, errors and complexity, and the same results where the Java library
specifies them. Requires .NET 10 or
newer.

### Added

- `Graphity.Core` (namespace `Graphity`) - the graph, the builder, and every algorithm that applies
  to any directed graph, dominators included
- `Graphity.ControlFlow` - the analyses of control flow: loops, dominance frontiers, control
  dependence and the placement of phi-nodes
- `Graphity.Dot` - a writer for DOT (the language of Graphviz)
- `Graphity.Mermaid` - a writer for Mermaid flowcharts, which render in Markdown
- `Graphity.All` - the counterpart of the Java artifact `graphity`: all four packages with one
  dependency
- The API documentation at [kaufco.github.io/graphity-csharp](https://kaufco.github.io/graphity-csharp/),
  with the overview and the examples of the Java documentation in C#
