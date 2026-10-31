# Graphity.ControlFlow

Analyses of control flow: loops, dominance frontiers, control dependence and the placement of phi-nodes.

This package of [Graphity for .NET](https://github.com/kaufco/graphity-csharp) has the namespace `Graphity.ControlFlow`. It builds on `Graphity.Core` and is released with it at the same version. Its analyses work on a control-flow graph, with the basic blocks of a method as nodes and an edge wherever control can pass from one block to another:

- `Loops` - the loop nesting forest: every loop with its header, its body and its entries, and how the loops nest, irreducible loops included.
- `IteratedDominanceFrontier` - the blocks where SSA (static single assignment) form places the phi-nodes of a variable.
- `ControlFlow` - the dominance frontiers as a graph, and from them control dependence: which block decides whether another one runs.

The analyses follow edges against their direction, so they take a `BiAdjacentGraph`, or the `Dominators` computed on one; build it once and pass it to all of them. Here, the loop of a method `load` around its block `then`, from its control flow `cfg` and its entry block `entry`:

<!-- example: AnalyzingControlFlow.cs#loops -->
```csharp
using Graphity;
using Graphity.ControlFlow;

// The basic blocks of load are the nodes 0..5 of cfg: entry, header, body, then,
// next, exit.

// Find the loops of load, from its entry block.
Loops loops = Loops.Of(BiAdjacentGraph.Of(cfg), entry);

// Find the loop around the block then, with its header and its body.
int loop = loops.InnermostLoopOf(then);         // 0 - the for loop, the only loop
int header = loops.HeaderOf(loop);              // 1 - the block header
// [1, 2, 3, 4] - header, body, then and next
ReadOnlySpan<int> blocks = loops.BodyOf(loop);
```

The class `ControlFlow` shares its name with the namespace; code in another namespace below `Graphity` writes `Graphity.ControlFlow.ControlFlow` or uses a `using` alias.

No parameter, return value or element in this package is `null` unless its type is nullable; a `null` argument where the type does not allow one throws `ArgumentNullException`. The XML documentation of every type and member states its contract: what it returns and throws, and its cost in time and memory.
