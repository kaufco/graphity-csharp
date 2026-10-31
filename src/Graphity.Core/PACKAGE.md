# Graphity.Core

Directed graphs and their analysis, for tools that see a program as graphs: components, orders and cycles, reachability, trees and dominators, and graphs derived from other graphs.

This package is the core of [Graphity for .NET](https://github.com/kaufco/graphity-csharp), with the namespace `Graphity`. The packages `Graphity.ControlFlow`, `Graphity.Dot` and `Graphity.Mermaid` build on it and are released with it at the same version, and `Graphity.All` brings in all four. A `Graph` has the nodes `0..NodeCount-1` and holds nothing but its edges, in 4 bytes per node and per edge; names and other data on nodes and edges stay in arrays of the caller, indexed by node ID or edge index. `GraphBuilder` builds a graph from a model of the caller, and the algorithms take the graph and return immutable results. None of them recurses, so a deep graph does not overflow the call stack. Here, the packages of a small program and the packages each of them uses become a graph, which has a cycle:

<!-- example: GettingStarted.cs#firstResult -->
```csharp
using System.Collections.Immutable;
using Graphity;

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

## Graphs

- `Graph` - a directed graph with the out-nodes of every node; `BiAdjacentGraph` - one that also holds the in-nodes, for the algorithms that follow edges against their direction. It gives the transposed graph as well.
- `GraphBuilder` - builds a graph from edges in any order; `Graph.Of` takes the arrays of its CSR (compressed sparse row) layout, which `Csr` hands to algorithm code of your own.
- `Graph.WriteTo(Stream)` and `Graph.ReadFrom(Stream)` - store a graph in a stream and load it again, in the binary format of the Java library.
- `SortedIntView` - the sorted list of node IDs or edge indices that lookups such as `Graph.OutNodes` return; `Edges` - a list of edges, each with its source, target and edge index.
- `Graphs.AdjacencyListOf`, `Graphs.TopologyHash` and `Graphs.Diff` - a whole graph as text, a hash to keep, and the edges added and removed between two versions of a graph.

## Components and connectivity

- `Components.SccsOf` - the SCCs (strongly connected components), as `Sccs`.
- `Components.WccsOf` - the weakly connected components, which are the components of the graph read as undirected.
- `Components.BiconnectivityOf` - the bridges, articulation points and biconnected components, as `Biconnectivity`.
- `Components.StrongConnectivityOf` - the strong bridges and strong articulation points, as `StrongConnectivity`.
- `Graphs.IsStronglyConnected` and `Graphs.IsWeaklyConnected` - the same questions for the whole graph, answered without the components.

## Orders, cycles and traversals

- `TopOrder` - a topological order of a DAG (directed acyclic graph); `TopOrderAndLevels` - one with a level per node.
- `Graphs.IsDag` and `Graphs.FindCycle` - whether a graph has a cycle, and one of its cycles.
- `EdgeTypes` - the tree, back, forward and cross edges of a depth-first search; `BackEdges` - the back edges alone.
- `DfsTraverser`, `DfsEdgeTraverser` and `FrontierTraverser` - the base classes of depth-first and breadth-first searches of your own.

## Reachability and distances

- `Graphs.ReachableFrom` - the nodes reachable from one or several nodes; `Graphs.RootNodes` - the nodes with in-degree 0, called root nodes.
- `BfsDistances` - distances and shortest paths from one or several nodes.
- `Graphs.TransitiveClosure` and `Graphs.TransitiveReduction` - an edge for every path, and the fewest edges for the same paths.

## Trees and dominators

- `Ancestry` - parent, depth and lowest common ancestor in a forest; `PreorderAncestry` - the ancestor test in O(1) and the nodes of each subtree.
- `Graphs.IsTree`, `Graphs.IsForest` and `Graphs.IsPath` - shape checks, and their counterparts for the graph read as undirected, such as `Graphs.IsUndirectedTree`.
- `Dominators` - the dominators of a control-flow graph, and on its transposed graph the post-dominators; `Graphs.DominatorTree` - the dominator tree as a graph.

Loops, dominance frontiers, control dependence and the placement of phi-nodes are in the package `Graphity.ControlFlow`. The package `Graphity.Mermaid` writes a graph as a Mermaid flowchart, and `Graphity.Dot` writes it as text in DOT (the language of Graphviz).

## Derived graphs and the data on them

- `Graphs.Remapped` with a `NodeMapping` - a subgraph or a graph with renumbered nodes; `EdgeMapping` carries data on the edges over to it.
- `NodePartition.Condense` - the condensed graph of a partition of the nodes into groups; `EdgePartition` - the edges behind each of its edges.
- `BiAdjacentGraph.Transposed` - the transposed graph, without copying; `Graphs.TransposedEdgeMapping` carries data on the edges over to it.

No parameter, return value or element in this package is `null` unless its type is nullable; a `null` argument where the type does not allow one throws `ArgumentNullException`. The XML documentation of every type and member states its contract: what it returns and throws, and its cost in time and memory.
