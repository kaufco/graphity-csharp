# Graphity for .NET

[![NuGet](https://img.shields.io/nuget/v/Graphity.All)](https://www.nuget.org/packages/Graphity.All)
[![API documentation](https://img.shields.io/badge/API%20documentation-online-blue)](https://kaufco.github.io/graphity-csharp/)
[![Guide](https://img.shields.io/badge/Guide-online-blue)](https://kaufco.github.io/graphity-doc/?lang=csharp)
[![CI](https://github.com/kaufco/graphity-csharp/actions/workflows/ci.yml/badge.svg)](https://github.com/kaufco/graphity-csharp/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-blue)](https://dotnet.microsoft.com/download/dotnet/10.0)

**This repository follows [Graphity for Java](https://github.com/kaufco/graphity).** Development happens there: see [its issues](https://github.com/kaufco/graphity/issues) and [its changelog](https://github.com/kaufco/graphity/blob/main/CHANGELOG.md) for what is new and what comes next.

This is the C# implementation of Graphity. It is managed code without dependencies, trimmable and compatible with AOT (ahead-of-time) compilation.

Graphity is a graph library which keeps graphs in a compact, immutable form and provides algorithms on top of it, such as strongly and weakly connected components, bridges and articulation points, topological order and levels, cycle detection and edge classification, breadth-first distances and reachability, lowest common ancestors, dominators, loop nesting and condensed graphs. The core stays lean: the analyses of control flow and the writers for DOT (the language of Graphviz) and for Mermaid flowcharts are packages of their own, which you add when you need them.

It is built for code analysis and compiler pipelines, with graphs such as abstract syntax trees, control-flow and data-flow graphs, and dependency graphs. More generally, it is built for anywhere you build a graph from your domain model, run analyses on it, and use the results. Every algorithm returns a typed, immutable result with follow-up queries, and runs without recursion, so deep graphs do not overflow the call stack.

Data on nodes and edges stays in your own arrays, indexed by node ID or edge index, and every transformation tells you how to carry them along. The graph itself stores only structure: 4 bytes per node and per edge, in a compressed sparse row (CSR) layout, which is where speed and footprint come from.

**The same library exists for Java, Rust and TypeScript**, with the same API and the same binary format: a graph written in C# can be read in any of them. See [One library, four languages](#one-library-four-languages).

## Contents

1. [Installation](#installation)
2. [Getting started](#getting-started)
3. [Where to find what](#where-to-find-what)
4. [Data on nodes and edges](#data-on-nodes-and-edges)
5. [One library, four languages](#one-library-four-languages)
6. [Documentation](#documentation)
7. [Building and testing from source](#building-and-testing-from-source)
8. [License](#license)

## Installation

`Graphity.All` brings in every package of Graphity:

```bash
dotnet add package Graphity.All
```

To take only what you need, depend on the packages themselves instead of `Graphity.All`, the same way and at the same version:

| Package | Namespace | What it adds |
|---|---|---|
| `Graphity.Core` | `Graphity` | The graph, the builder, and every algorithm that applies to any directed graph, dominators included |
| `Graphity.ControlFlow` | `Graphity.ControlFlow` | The analyses of control flow: loops, dominance frontiers, control dependence and the placement of phi-nodes |
| `Graphity.Dot` | `Graphity.Dot` | A writer for DOT (the language of Graphviz) |
| `Graphity.Mermaid` | `Graphity.Mermaid` | A writer for Mermaid flowcharts, which render in Markdown |
| `Graphity.All` | - | All of the above |

Graphity requires .NET 10 or newer. No parameter, return value or element in the API is `null` unless its type is annotated `?`, which the compiler reads from the signatures.

## Getting started

A `Graph` holds nothing but its structure: its nodes are the `int` IDs `0..NodeCount-1`, its edges are numbered by edge index, and it takes 4 bytes per node and per edge. Names, weights and any other data on nodes and edges stay in arrays you own, indexed by node ID or edge index.

The algorithms are classes of their own: each takes a graph and returns a typed result with follow-up queries. Graphs and results are immutable and safe to share between threads, and no algorithm recurses, so a deep graph does not overflow the call stack.

The following example is a complete class with a `Main` method, ready to run. It builds a graph of the dependencies between the packages of a small C# program. These dependencies contain a cycle:

<!-- example: GettingStarted.cs#program -->
```csharp
using System.Collections.Immutable;
using Graphity;

public static class GettingStarted
{
    public static void Main()
    {
        // The packages are the nodes 0..7, and uses[p] lists the packages that package p
        // uses.
        string[] names =
            ["app", "cli", "ui", "service", "model", "storage", "json", "util"];
        int[][] uses = [[1, 2, 3], [7], [3, 4, 7], [4, 5], [5], [4, 6, 7], [7], []];

        // Build the graph: an edge p -> q for every package q that p uses.
        var builder = new GraphBuilder();
        builder.AddNodes(uses.Length);
        for (int pkg = 0; pkg < uses.Length; pkg++)
        {
            builder.AddOutEdges(pkg, uses[pkg]);
        }
        Graph dependencies = builder.Build();

        // Find a cycle.
        ImmutableArray<int> cycle = Graphs.FindCycle(dependencies);
        // cycle: [4, 5] - model and storage use each other

        // Print the cycle with the names of its packages.
        Console.WriteLine($"[{string.Join(", ", cycle.Select(pkg => names[pkg]))}]");
        // [model, storage]
    }
}
```

The next two examples continue its `Main` method. Every further question is a call on the same graph. `Components.SccsOf` puts `model` and `storage` into one SCC (strongly connected component) and orders the SCCs so that each package comes after the packages it uses, which can serve as a build order:

<!-- example: Connectivity.cs#sccsOf -->
```csharp
// The SCCs in reverse topological order, each as the names of its packages.
Sccs sccs = Components.SccsOf(dependencies);
List<List<string>> buildOrder = [];
for (int scc = 0; scc < sccs.SccCount; scc++)
{
    buildOrder.Add([.. sccs.NodesOf(scc).Select(node => names[node])]);
}
// buildOrder: [[util], [cli], [json], [model, storage], [service], [ui], [app]]
```

Which packages does a change to `model` affect? `BiAdjacentGraph.Of` adds the in-nodes of every node, so that a search can follow the edges against their direction:

<!-- example: Transposing.cs#transposed -->
```csharp
int model = 4;
BiAdjacentGraph usedBy = BiAdjacentGraph.Of(dependencies).Transposed();
SortedIntView affected = Graphs.ReachableFrom(usedBy, model);
// affected: [0, 2, 3, 4, 5] - app, ui, service, model and storage
```

The [guide](https://kaufco.github.io/graphity-doc/?lang=csharp) goes on from here, one problem per page, on topics such as building graphs, cycles and orders, reachability, trees, dominance and control flow, transforming graphs with their data, and searches of your own.

## Where to find what

The guide [Solving Graph Problems with Graphity](https://kaufco.github.io/graphity-doc/?lang=csharp) shows how to solve graph problems with Graphity: one page per problem, with pictures of the graph, tested code and its results. It shows every example in Java, Rust, TypeScript and C#.

These are the questions that Graphity answers, with the algorithms and the entry points that answer them:

- **How do I build a graph?** `GraphBuilder` from edges in any order, or `Graph.Of` from the two arrays of a CSR layout.
- **Which parts belong together?** Strongly connected components (Tarjan's algorithm), weakly connected components, and the connected components of the graph read as undirected; bridges, articulation points and biconnected components (Hopcroft and Tarjan); strong bridges and strong articulation points (Italiano, Laura and Santaroni). All in `Components`.
- **In which order, and where are the cycles?** A topological order (topological sort) by depth-first search (`TopOrder`), and one with a level per node by Kahn's algorithm (`TopOrderAndLevels`); whether a graph has a cycle, and one of its cycles (`Graphs.IsDag`, `Graphs.FindCycle`); the tree, back, forward and cross edges of a depth-first search (`EdgeTypes`, `BackEdges`).
- **What is reachable, and how far?** The nodes reachable from one or several nodes (`Graphs.ReachableFrom`); distances and shortest paths by breadth-first search (`BfsDistances`); transitive closure and, for a DAG (directed acyclic graph), transitive reduction, both in `Graphs`.
- **Who is the ancestor of whom in a tree?** Parent, depth and LCA (lowest common ancestor) in a forest (`Ancestry`); the ancestor test in O(1) and the nodes of each subtree (`PreorderAncestry`); shape checks such as `Graphs.IsTree`, `Graphs.IsPath` and `Graphs.IsBipartite`.
- **Which node dominates which?** Dominators from one or several entries, and post-dominators on the transposed graph (`Dominators`, by the algorithm of Lengauer and Tarjan); the dominator tree (`Graphs.DominatorTree`).
- **Where are the loops, where do phi-nodes go, what controls what?** (`Graphity.ControlFlow`) The loop nesting forest, irreducible loops included (`Loops`, by Havlak's algorithm); the blocks where SSA (static single assignment) form places phi-nodes (`IteratedDominanceFrontier`, by the algorithm of Sreedhar and Gao); dominance frontiers and control dependence (`ControlFlow`).
- **How do I condense, filter or renumber a graph?** The condensed graph of a partition of your choosing (`NodePartition.Condense`); subgraphs and renumbered graphs (`Graphs.Remapped` with a `NodeMapping`); the transposed graph, without copying (`BiAdjacentGraph.Transposed`).
- **How does my data follow a transformation?** `NodeMapping` for data on nodes; `Graphs.EdgeMapping`, `Graphs.TransposedEdgeMapping` and `Graphs.EdgePartition` for data on edges.
- **How do I store, load and compare graphs?** `Graph.WriteTo` and `Graph.ReadFrom`, as the two arrays of the CSR layout in little-endian byte order; a 64-bit hash of the nodes and edges (`Graphs.TopologyHash`); the edges added and removed between two versions of a graph (`Graphs.Diff`).
- **How do I look at a graph?** (`Graphity.Dot`, `Graphity.Mermaid`) `DotWriter` and `MermaidWriter`, with labels, attributes, shapes and clusters from your own arrays; `Graphs.AdjacencyListOf` for a whole graph as text.
- **How do I write a search of my own?** The base classes of depth-first and breadth-first searches, without recursion (`DfsTraverser`, `DfsEdgeTraverser`, `FrontierTraverser`); `Csr` hands the arrays of a graph to loops of your own.

The [API documentation](https://kaufco.github.io/graphity-csharp/) opens on the same questions; the documentation of each namespace lists its entry points by topic, and every method states its contract: what it returns and throws, and its cost in time and memory. The guide has an [index of all entry points](https://kaufco.github.io/graphity-doc/api-index/?lang=csharp), each with the problems that use it.

## Data on nodes and edges

Graphity keeps no data on nodes or edges. You keep it in arrays of your own, one array per attribute. An array is indexed by node ID `0..NodeCount-1` or by edge index `0..EdgeCount-1` - like a column of a table whose rows are the nodes or the edges. An array can hold values or objects, and a graph can have as many arrays as you need.

Here, `calls` holds the calls between the methods of the small program, and `callSites` the places in the source where one method calls another. The nodes carry a named tuple per method, and the edges the number of call sites:

<!-- example: DataOnNodesAndEdges.cs#columns -->
```csharp
(string Name, string Package)[] methods =      // methods[node]
[
    ("main", "app"), ("parseArgs", "cli"), ("run", "app"), ("load", "service"),
    ("save", "service"), ("show", "ui"), ("parse", "model"), ("format", "model"),
    ("read", "storage"), ("write", "storage"),
];

int[] siteCount = new int[calls.EdgeCount];    // siteCount[edgeIndex]
foreach (CallSite site in callSites)
{
    siteCount[calls.EdgeIndexOf(site.Caller, site.Callee)]++;
}
// siteCount: 2 for run -> show and for parse -> parse, 1 for every other call
```

A transformation that renumbers nodes or merges them comes with the mapping or partition that carries the arrays along, each array on its own: you take the columns you still need and leave the others behind. Here, the subgraph of the methods that `load` reaches takes both arrays:

<!-- example: DataOnNodesAndEdges.cs#carryAlong -->
```csharp
NodeMapping newToOld = NodeMapping.ForSubgraph(Graphs.ReachableFrom(calls, load));
Graph loading = Graphs.Remapped(calls, newToOld);
// loading: load -> parse, load -> read, parse -> parse

var loadingMethods = new (string Name, string Package)[loading.NodeCount];
newToOld.Gather(methods, loadingMethods);
// loadingMethods: load, parse, read

int[] loadingSiteCount =
    Graphs.EdgeMapping(loading, calls, newToOld).Gather(siteCount);
// loadingSiteCount: 1 for load -> parse and for load -> read,
//                   2 for parse -> parse
```

A renumbering in depth-first order (`NodeMapping.Reindex`) and the transposed graph (`Graphs.TransposedEdgeMapping`) work the same way. A condensed graph merges edges, and `EdgePartition.Reduce` aggregates their data, such as the call sites behind each call between two packages. The [guide](https://kaufco.github.io/graphity-doc/transforming/reduce-data/?lang=csharp) shows how.

## One library, four languages

Graphity exists for [Java](https://github.com/kaufco/graphity), [Rust](https://github.com/kaufco/graphity-rust), [TypeScript](https://github.com/kaufco/graphity-ts) and [C#](https://github.com/kaufco/graphity-csharp). This matters where a system spans several languages, for example with a producer in Java and a consumer in TypeScript:

- **The same API.** Every public type and method of the Java library has its counterpart in the other languages, in the names of each language. So there is nothing new to learn, and neither side lacks a feature that the other has.
- **The same data.** All implementations read and write the same binary format, so a graph written in Java can be read in TypeScript, and the other way round. A graph also has the same hash in every language.
- **The same results.** Orders, IDs and the text of the writers are the same, bit for bit, wherever the API fixes them.

| Language | Repository | Dependency |
|---|---|---|
| Java | [kaufco/graphity](https://github.com/kaufco/graphity) | `com.kobayami:graphity` |
| Rust | [kaufco/graphity-rust](https://github.com/kaufco/graphity-rust) | `graphity-all` |
| TypeScript | [kaufco/graphity-ts](https://github.com/kaufco/graphity-ts) | `@kobayami/graphity` |
| C# | [kaufco/graphity-csharp](https://github.com/kaufco/graphity-csharp) | `Graphity.All` |

Each of these dependencies brings the whole library. The README of each repository lists the single modules.

## Documentation

This README is the overview; the guide goes into depth, and the API documentation is the contract.

- **Guide:** [kaufco.github.io/graphity-doc](https://kaufco.github.io/graphity-doc/?lang=csharp) - one page per problem, from building a graph to control-flow analysis: the problem, the graph before and after, and tested code, with an index by API and a glossary.
- **API reference:** [kaufco.github.io/graphity-csharp](https://kaufco.github.io/graphity-csharp/) - the contract of every class and method.
- **Changelog:** [CHANGELOG.md](CHANGELOG.md)

## Building and testing from source

To use Graphity, take the published packages (see [Installation](#installation)). The instructions below are for contributors who want to fork or extend the library.

From the repository root:

```bash
dotnet build -c Release
dotnet test -c Release
```

Builds all packages and runs all tests. Building needs the .NET 10 SDK.

## License

Graphity is licensed under the [Apache License, Version 2.0](LICENSE).

Copyright 2025–2026 Marco Kaufmann.

Forks and modifications are welcome under the terms of the license. The name "Graphity" refers to the original project at https://github.com/kaufco/graphity; please pick a different name for any redistributed version. See [`NOTICE`](NOTICE).
