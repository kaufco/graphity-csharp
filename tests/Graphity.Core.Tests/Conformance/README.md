# Conformance fixtures

These files hold the results of the Java library (Graphity 3.0.0) on 28 graphs: 14 shapes, seven
plain and seven shuffled, each with 200 and 1,000 nodes. `ConformanceTests` reads them and requires
the C# port to produce the same results bit for bit. The port never computes its own expected
values.

In the file names, `<case>` stands for `<shape>-<n>`:

| File | Content |
|---|---|
| `<case>.gphy` | the graph, as `Graph.WriteTo` writes it |
| `<case>.scc.i32`, `<case>.wcc.i32` | the SCC (strongly connected component) and the WCC (weakly connected component) of every node |
| `<case>.condensed.gphy` | the condensation by SCCs |
| `<case>.edgetypes.u8` | the edge type of every edge: 0 tree, 1 back, 2 forward, 3 cross |
| `<case>.reindex.i32` | `NodeMapping.Reindex`, new to old |
| `<case>.dfspre.i32`, `<case>.dfspost.i32` | the order of the hook calls of a plain `DfsTraverser` |
| `<case>.toporder.i32`, `.kahnorder.i32`, `.levels.i32` | DAGs (directed acyclic graphs) only: the order of `TopOrder.Of`, the order and the level of every node of `TopOrderAndLevels.Of` |
| `cases.tsv` | name, shape, node and edge count, whether the graph is a DAG, the number of SCCs, WCCs and levels |

`.i32` files are little-endian 32-bit integers without a header, `.u8` files one byte per edge.
