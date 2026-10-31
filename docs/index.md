---
title: Overview
---

# Graphity for .NET

Graphity is a graph library which keeps graphs in a compact, immutable form and provides algorithms on top of it, such as strongly and weakly connected components, bridges and articulation points, topological order and levels, cycle detection and edge classification, breadth-first distances and reachability, lowest common ancestors, dominators, loop nesting and condensed graphs.

This is its C# implementation, which follows the [Java library](https://github.com/kaufco/graphity) of the same version type by type and method by method.

Every algorithm returns a typed, immutable result with follow-up queries, and runs without recursion, so deep graphs do not overflow the call stack. Data on nodes and edges stays in your own arrays, indexed by node ID or edge index, and every transformation tells you how to carry them along. The graph itself stores only structure: 4 bytes per node and per edge, in a compressed sparse row (CSR) layout, which is where speed and footprint come from.

## Where to start

The nodes of a <xref:Graphity.Graph> are the `int` IDs `0..NodeCount-1`, and its edges are numbered by edge index `0..EdgeCount-1`, ordered by source and then by target. <xref:Graphity.GraphBuilder> builds a graph from edges in any order. The algorithms are not methods of the graph but static methods: `Of` on a class named after the algorithm, such as [TopOrder.Of](xref:Graphity.TopOrder.Of*) and [Dominators.Of](xref:Graphity.Dominators.Of*); the methods of <xref:Graphity.Components> for the components; and those of <xref:Graphity.Graphs> for the rest, such as [Graphs.FindCycle](xref:Graphity.Graphs.FindCycle*).

A `Graph` holds the out-nodes of every node. [BiAdjacentGraph.Of](xref:Graphity.BiAdjacentGraph.Of*) adds the in-nodes. The algorithms that need them, such as <xref:Graphity.Dominators> and [Components.BiconnectivityOf](xref:Graphity.Components.BiconnectivityOf*), take a `BiAdjacentGraph`: build it once and pass it to all of them.

The following example builds a graph of the dependencies between the packages of a small C# program. These dependencies contain a cycle:

<!-- example: GettingStarted.cs#firstResult -->
```csharp
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
```

## Where to find what

| Question | Entry points |
|---|---|
| How do I build a graph? | <xref:Graphity.GraphBuilder> from edges in any order, or [Graph.Of](xref:Graphity.Graph.Of*) from the two arrays of a CSR layout. |
| Which parts belong together? | <xref:Graphity.Components>: strongly and weakly connected components, and the connected components of the graph read as undirected; bridges, articulation points and biconnected components; strong bridges and strong articulation points. |
| In which order, and where are the cycles? | A topological order (<xref:Graphity.TopOrder>), and one with a level per node (<xref:Graphity.TopOrderAndLevels>); whether a graph has a cycle, and one of its cycles ([Graphs.IsDag](xref:Graphity.Graphs.IsDag*), [Graphs.FindCycle](xref:Graphity.Graphs.FindCycle*)); the tree, back, forward and cross edges of a depth-first search (<xref:Graphity.EdgeTypes>, <xref:Graphity.BackEdges>). |
| What is reachable, and how far? | The nodes reachable from one or several nodes ([Graphs.ReachableFrom](xref:Graphity.Graphs.ReachableFrom*)); distances and shortest paths by breadth-first search (<xref:Graphity.BfsDistances>); transitive closure and, for a DAG (directed acyclic graph), transitive reduction ([Graphs.TransitiveClosure](xref:Graphity.Graphs.TransitiveClosure*), [Graphs.TransitiveReduction](xref:Graphity.Graphs.TransitiveReduction*)). |
| Who is the ancestor of whom in a tree? | Parent, depth and LCA (lowest common ancestor) in a forest (<xref:Graphity.Ancestry>); the ancestor test in O(1) and the nodes of each subtree (<xref:Graphity.PreorderAncestry>); shape checks such as [Graphs.IsTree](xref:Graphity.Graphs.IsTree*), [Graphs.IsPath](xref:Graphity.Graphs.IsPath*) and [Graphs.IsBipartite](xref:Graphity.Graphs.IsBipartite*). |
| Which node dominates which? | Dominators from one or several entries, and post-dominators on the transposed graph (<xref:Graphity.Dominators>); the dominator tree ([Graphs.DominatorTree](xref:Graphity.Graphs.DominatorTree*)). |
| Where are the loops, where do phi-nodes go, what controls what? (`Graphity.ControlFlow`) | The loop nesting forest, irreducible loops included (<xref:Graphity.ControlFlow.Loops>); the blocks where SSA (static single assignment) form places phi-nodes (<xref:Graphity.ControlFlow.IteratedDominanceFrontier>); dominance frontiers and control dependence (<xref:Graphity.ControlFlow.ControlFlow>). |
| How do I condense, filter or renumber a graph? | The condensed graph of a partition of your choosing ([NodePartition.Condense](xref:Graphity.NodePartition.Condense*)); subgraphs and renumbered graphs ([Graphs.Remapped](xref:Graphity.Graphs.Remapped*) with a <xref:Graphity.NodeMapping>); the transposed graph, without copying ([BiAdjacentGraph.Transposed](xref:Graphity.BiAdjacentGraph.Transposed*)). |
| How does my data follow a transformation? | <xref:Graphity.NodeMapping> for data on nodes; [Graphs.EdgeMapping](xref:Graphity.Graphs.EdgeMapping*), [Graphs.TransposedEdgeMapping](xref:Graphity.Graphs.TransposedEdgeMapping*) and [Graphs.EdgePartition](xref:Graphity.Graphs.EdgePartition*) for data on edges. |
| How do I store, load and compare graphs? | [Graph.WriteTo](xref:Graphity.Graph.WriteTo*) and [Graph.ReadFrom](xref:Graphity.Graph.ReadFrom*), as the two arrays of the CSR layout; a 64-bit hash of the nodes and edges ([Graphs.TopologyHash](xref:Graphity.Graphs.TopologyHash*)); the edges added and removed between two versions of a graph ([Graphs.Diff](xref:Graphity.Graphs.Diff*)). |
| How do I look at a graph? (`Graphity.Dot`, `Graphity.Mermaid`) | <xref:Graphity.Dot.DotWriter> and <xref:Graphity.Mermaid.MermaidWriter>, with labels, attributes, shapes and clusters from your own arrays; [Graphs.AdjacencyListOf](xref:Graphity.Graphs.AdjacencyListOf*) for a whole graph as text. |
| How do I write a search of my own? | The base classes of depth-first and breadth-first searches, without recursion (<xref:Graphity.DfsTraverser>, <xref:Graphity.DfsEdgeTraverser>, <xref:Graphity.FrontierTraverser>); <xref:Graphity.Csr> hands the arrays of a graph to loops of your own. |

The documentation of each namespace, listed [below](#packages), groups its entry points by topic, and the documentation of each class names the algorithm it uses.

## Contracts

Every method states its contract: what it returns and throws, whether a result is a view or a copy, in which order the results come where that matters, and its cost in time and memory. Three rules hold across the whole API:

- No parameter, return value or element in the API is `null` unless its type is nullable; a `null` argument where the type does not allow one throws `ArgumentNullException`.
- Graphs, mappings, partitions and the results of the algorithms are immutable and safe to share between threads. The builder, the traversers and the writers are mutable and not safe to share between threads.
- A node ID, an edge index or any other ID or position outside its range throws `ArgumentOutOfRangeException`; any other invalid argument throws `ArgumentException`.

## Further references

- **Guide:** [kaufco.github.io/graphity-doc](https://kaufco.github.io/graphity-doc/?lang=csharp) - one page per problem, from building a graph to control-flow analysis: the problem, the graph before and after, and tested code, with an index by API and a glossary.
- **README:** [kaufco/graphity-csharp](https://github.com/kaufco/graphity-csharp#readme) - how to add Graphity to a project, and a first complete program.
- **Repository:** [kaufco/graphity-csharp](https://github.com/kaufco/graphity-csharp) - the source code and the changelog. It follows [Graphity for Java](https://github.com/kaufco/graphity), where development happens.

## Packages

| Package | Namespace | What it adds |
|---|---|---|
| `Graphity.Core` | <xref:Graphity> | The graph, the builder, and every algorithm that applies to any directed graph, dominators included |
| `Graphity.ControlFlow` | <xref:Graphity.ControlFlow> | The analyses of control flow: loops, dominance frontiers, control dependence and the placement of phi-nodes |
| `Graphity.Dot` | <xref:Graphity.Dot> | A writer for DOT (the language of Graphviz) |
| `Graphity.Mermaid` | <xref:Graphity.Mermaid> | A writer for Mermaid flowcharts, which render in Markdown |
| `Graphity.All` | - | All of the above |
