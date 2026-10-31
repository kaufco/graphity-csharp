using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Graphity;

/// <summary>
/// Operations on graphs, in five families: derived graphs, edge mappings, node sets, shape checks,
/// and a graph as text, hash or diff.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><description>
///   <b>Derived graphs:</b> <see cref="Remapped"/> renumbers the nodes of a graph by a
///   <see cref="NodeMapping"/>, which also selects subgraphs; <see cref="TransitiveClosure"/> and
///   <see cref="TransitiveReduction"/> add and drop the edges that other paths imply;
///   <see cref="DominatorTree"/> turns <see cref="Dominators"/> into a graph.
///   </description></item>
///   <item><description>
///   <b>Edge mappings:</b> <see cref="EdgeMapping"/>, <see cref="TransposedEdgeMapping"/> and
///   <see cref="EdgePartition"/> relate the edges of a remapped, transposed or condensed graph to the
///   edges of the graph it came from.
///   </description></item>
///   <item><description>
///   <b>Node sets:</b> <see cref="RootNodes"/> and <see cref="ReachableFrom(Graph, ReadOnlySpan{int})"/>.
///   </description></item>
///   <item><description>
///   <b>Shape checks:</b> <see cref="IsDag"/>, <see cref="IsForest"/>, <see cref="IsTree"/>,
///   <see cref="IsPath"/> and <see cref="IsStronglyConnected"/> read the graph as directed, and
///   <see cref="FindCycle"/> returns a cycle that makes a graph no DAG (directed acyclic graph);
///   <see cref="IsUndirectedForest"/>, <see cref="IsUndirectedTree"/>, <see cref="IsBipartite"/> and
///   <see cref="IsWeaklyConnected"/> read it as undirected.
///   </description></item>
///   <item><description>
///   <b>Text, hash and diff:</b> <see cref="AdjacencyListOf"/> writes a whole graph as text,
///   <see cref="TopologyHash"/> hashes it into 64 bits to keep, and <see cref="Diff"/> returns the
///   edges added and removed between two versions of a graph.
///   </description></item>
/// </list>
/// <para>
/// Data on nodes and edges follows a renumbered, transposed or condensed graph through the mappings.
/// Like the node mappings that the factories of <see cref="NodeMapping"/> make, the edge mappings
/// point from new to old: from the derived graph to the original one. Their <c>Gather</c> methods
/// carry an array of data over to the derived graph. An <see cref="Graphity.EdgePartition"/> groups
/// the edges of the original by the condensed edge they became, and its <c>Reduce</c> methods combine
/// their data.
/// </para>
/// <para>
/// A shape check without a prefix reads the graph as directed. One prefixed <c>Undirected</c> reads it
/// as undirected, as <see cref="Components.CcsOf"/> does: each edge connects its source and target
/// both ways, and the edges <c>a -> b</c> and <c>b -> a</c> count as one. <see cref="IsBipartite"/>
/// and <see cref="IsWeaklyConnected"/> read it so too, without the prefix, because neither property
/// has a directed reading. The checks of the undirected reading use
/// <see href="https://en.wikipedia.org/wiki/Disjoint-set_data_structure">union-find</see>, whose cost
/// grows with alpha(NodeCount). The inverse Ackermann function alpha is below 5 for any graph. No
/// shape check needs memory in proportion to the edge count.
/// </para>
/// <para>
/// Every result is computed anew on every call, not cached, and shares no array with the arguments:
/// keep a result to use it more than once. No method recurses, so deep graphs do not overflow the
/// call stack.
/// </para>
/// </remarks>
public static class Graphs
{
    // The constants of TopologyHash. Changing one of them, or a shift or rotation distance in the
    // code, changes the hash of every graph, which a release must announce in its changelog - and
    // every implementation must follow, as the hash is the same as in Java.

    // The seed: the fractional part of the square root of 2, scaled to 64 bits and rounded.
    private const ulong HashSeed = 0x6A09E667F3BCC909UL;

    // Spreads each input over all 64 bits: 2^64 / golden ratio, rounded down.
    private const ulong HashGoldenGamma = 0x9E3779B97F4A7C15UL;

    // The multiplier of each step: PRIME64_2 of xxHash64.
    private const ulong HashStepPrime = 0xC2B2AE3D27D4EB4FUL;

    // The two multipliers of the SplitMix64 finalizer, as in Java's SplittableRandom.
    private const ulong SplitMix64Multiplier1 = 0xBF58476D1CE4E5B9UL;

    private const ulong SplitMix64Multiplier2 = 0x94D049BB133111EBUL;

    // The characters the AdjacencyListOf builder reserves per node: a line without its targets,
    // "1234567 -> []\n", and some slack. An estimate for the initial capacity; the builder grows past
    // it if needed.
    private const int ListCharsPerNode = 16;

    // The characters the same builder reserves per target, ", 123456".
    private const int ListCharsPerEdge = 8;

    // The longest string .NET creates, 2^30 - 33 characters (a Java string holds about twice as many):
    // the cap on the capacity of the same builder.
    internal const int MaxStringLength = 0x3FFFFFDF;

    private const int Visited = 0b01;
    private const int OnStack = 0b10;

    /// <summary>
    /// Returns <paramref name="graph"/> with its nodes renumbered by <paramref name="newToOld"/>: node
    /// <c>i</c> of the result is node <c>newToOld.Get(i)</c> of <paramref name="graph"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The result has the edges of <paramref name="graph"/> between the nodes that
    /// <paramref name="newToOld"/> maps to; the other nodes are left out, with their edges. The factories
    /// of <see cref="NodeMapping"/> make the mapping: <c>ForSubgraph</c> selects nodes,
    /// <see cref="NodeMapping.ForPermutation"/> and <see cref="NodeMapping.Reindex"/> reorder all of
    /// them. Data on the nodes follows with the <c>Gather</c> methods of the mapping, data on the edges
    /// with <see cref="EdgeMapping"/>. To merge nodes into groups instead, use
    /// <see cref="NodePartition.Condense"/>.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount log maxOutDegree).<br/>
    /// <b>Memory:</b> 4 bytes per node and per edge for the result; while it runs, 4 x NodeCount bytes
    /// more.
    /// </para>
    /// </remarks>
    /// <example>
    /// Here, <c>calls</c> holds the calls between the methods of a program, and the methods that
    /// <c>load</c> reaches become a graph of their own, with their names from <c>names</c>:
    /// <!-- example: Mappings.cs#subgraph -->
    /// <code language="csharp">
    /// NodeMapping newToOld = NodeMapping.ForSubgraph(Graphs.ReachableFrom(calls, load));
    /// Graph loading = Graphs.Remapped(calls, newToOld);
    /// string[] loadingNames = new string[loading.NodeCount];
    /// newToOld.Gather(names, loadingNames);
    /// // loadingNames: [load, parse, read]
    /// // loading: load -> parse, load -> read, parse -> parse
    /// </code>
    /// </example>
    /// <param name="graph">The graph to renumber.</param>
    /// <param name="newToOld">The node of <paramref name="graph"/> for each node of the result.</param>
    /// <returns>The renumbered graph.</returns>
    /// <exception cref="ArgumentException">
    /// A node of the result maps to -1, or two map to the same node.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A node of the result maps to an ID that is not a node of <paramref name="graph"/>.
    /// </exception>
    public static Graph Remapped(Graph graph, NodeMapping newToOld)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(newToOld);
        int newNodeCount = newToOld.Mapping.Length;
        // The general path gives the same, but allocates the inverse over every old node first.
        if (newNodeCount == 0)
        {
            return new Graph([0], []);
        }

        // Inverse (old → new) for edge-target translation, built in the same pass that checks every
        // new node. Not IndexMapping.InvertArray: Remapped rejects an unmapped new node, which an
        // inverse skips, and names both new nodes of a repeat.
        int[] oldOf = newToOld.Mapping;
        int oldNodeCount = graph.NodeCount;
        var newOf = new int[oldNodeCount];
        Array.Fill(newOf, -1);
        for (int newNode = 0; newNode < newNodeCount; newNode++)
        {
            int oldNode = oldOf[newNode];
            if (oldNode == -1)
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                    $"new node {newNode} is unmapped"), nameof(newToOld));
            }
            // Every NodeMapping holds -1 or a non-negative ID, so the upper bound is all that is left.
            if (oldNode >= oldNodeCount)
            {
                throw NotANode(newNode, oldNode, oldNodeCount);
            }
            if (newOf[oldNode] != -1)
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                    $"new nodes {newOf[oldNode]} and {newNode} both map to node {oldNode}"), nameof(newToOld));
            }
            newOf[oldNode] = newNode;
        }

        int[] oldTargets = graph.OutAdj;
        int[] oldStart = graph.OutStart;
        // The checks above make the mapping injective into the old nodes, so equal counts make it a
        // bijection: no edge is dropped, and the degree count becomes offset arithmetic.
        bool total = newNodeCount == oldNodeCount;

        // 1) Out-degree of every new node
        var newStart = new int[newNodeCount + 1];
        if (total)
        {
            for (int newNode = 0; newNode < newNodeCount; newNode++)
            {
                int oldNode = oldOf[newNode];
                newStart[newNode + 1] = oldStart[oldNode + 1] - oldStart[oldNode];
            }
        }
        else
        {
            for (int newNode = 0; newNode < newNodeCount; newNode++)
            {
                int oldNode = oldOf[newNode];
                int count = 0;
                for (int i = oldStart[oldNode], end = oldStart[oldNode + 1]; i < end; i++)
                {
                    if (newOf[oldTargets[i]] >= 0)
                    {
                        count++;
                    }
                }
                newStart[newNode + 1] = count;
            }
        }

        // 2) Prefix sum: degrees to start offsets
        for (int newNode = 0; newNode < newNodeCount; newNode++)
        {
            newStart[newNode + 1] += newStart[newNode];
        }

        // 3) Targets, each list sorted
        var newTargets = new int[newStart[newNodeCount]];
        if (total)
        {
            for (int newNode = 0; newNode < newNodeCount; newNode++)
            {
                int writePos = newStart[newNode];
                int oldNode = oldOf[newNode];
                for (int i = oldStart[oldNode], end = oldStart[oldNode + 1]; i < end; i++)
                {
                    newTargets[writePos++] = newOf[oldTargets[i]];
                }
                newTargets.AsSpan(newStart[newNode], writePos - newStart[newNode]).Sort();
            }
        }
        else
        {
            for (int newNode = 0; newNode < newNodeCount; newNode++)
            {
                int writePos = newStart[newNode];
                int oldNode = oldOf[newNode];
                for (int i = oldStart[oldNode], end = oldStart[oldNode + 1]; i < end; i++)
                {
                    int mapped = newOf[oldTargets[i]];
                    if (mapped >= 0)
                    {
                        newTargets[writePos++] = mapped;
                    }
                }
                newTargets.AsSpan(newStart[newNode], writePos - newStart[newNode]).Sort();
            }
        }
        return new Graph(newStart, newTargets);
    }

    private static ArgumentOutOfRangeException NotANode(int newNode, int oldNode, int oldNodeCount) =>
        new("newToOld", oldNode,
            string.Create(CultureInfo.InvariantCulture,
                $"new node {newNode} maps to {oldNode}, which is not a node of the old graph (nodeCount={oldNodeCount})"));

    /// <summary>
    /// Returns the dominator tree of <paramref name="dominators"/> as a graph, with an edge
    /// <c>d -> n</c> for every node <c>n</c> whose immediate dominator is <c>d</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The result has the node IDs of the analyzed graph, and its edges point away from the roots of the
    /// tree, for a walk down the tree such as with a <see cref="DfsTraverser"/>. Its root nodes are the
    /// entries, the nodes that only the virtual root above several entries dominates, and the unreachable
    /// nodes. To ask whether one node dominates another, use <see cref="Ancestry.Of(Dominators)"/>, which
    /// shares the immediate dominators instead of building this graph.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount).<br/>
    /// <b>Memory:</b> 4 bytes per node and per edge for the result.
    /// </para>
    /// </remarks>
    /// <param name="dominators">The dominators of a graph.</param>
    /// <returns>The dominator tree.</returns>
    public static Graph DominatorTree(Dominators dominators)
    {
        ArgumentNullException.ThrowIfNull(dominators);
        var children = ParentForest.ChildrenOf(dominators.ImmediateDominator);
        return new Graph(children.Start, children.Members);
    }

    /// <summary>
    /// Returns the transitive closure of <paramref name="graph"/>: the same nodes, with an edge
    /// <c>u -> v</c> wherever a path of one or more edges leads from <c>u</c> to <c>v</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A node has a self-loop in the result if and only if it lies on a cycle. The result can have up to
    /// NodeCount^2 edges; for the nodes reachable from a few nodes,
    /// <see cref="ReachableFrom(Graph, ReadOnlySpan{int})"/> costs far less.
    /// </para>
    /// <para>
    /// The closure is computed on the SCCs (strongly connected components) of <paramref name="graph"/>,
    /// with a row of bits per SCC for the SCCs it reaches. The rows take sccCount^2 / 8 bytes, about 1.25
    /// GB for 100 000 SCCs.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount + sccCount x (sccCount + EdgeCount) / 64 +
    /// resultEdgeCount log NodeCount), with sccCount the number of SCCs.<br/>
    /// <b>Memory:</b> 4 bytes per node and per edge for the result; while it runs, sccCount^2 / 8 bytes
    /// for the rows, which are held together with the result at the peak, and O(NodeCount + EdgeCount)
    /// more.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to close.</param>
    /// <returns>The transitive closure.</returns>
    /// <exception cref="ArgumentException">
    /// The closure has more edges than a graph can have, or <paramref name="graph"/> has more than
    /// 2^30 - 29 nodes.
    /// </exception>
    public static Graph TransitiveClosure(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        Sccs sccs = Sccs.Of(graph);
        int k = sccs.SccCount;
        Graph dag = sccs.Condense();
        int[] dagStart = dag.OutStart;
        int[] dagTargets = dag.OutAdj;
        int words = (k + 63) >>> 6;
        int[] members = sccs.Members;
        int[] memberStart = sccs.MemberStart;

        // SCC IDs are in reverse topological order: every successor of c has a smaller ID.
        var reach = new ulong[k][];
        var reachSize = new int[k];
        long total = 0;
        for (int c = 0; c < k; c++)
        {
            var row = new ulong[words];
            if (sccs.ContainsCycle(c))
            {
                row[c >> 6] |= 1UL << c;
            }
            for (int e = dagStart[c], end = dagStart[c + 1]; e < end; e++)
            {
                int d = dagTargets[e];
                row[d >> 6] |= 1UL << d;
                ulong[] below = reach[d];
                for (int w = 0; w < words; w++)
                {
                    row[w] |= below[w];
                }
            }
            reach[c] = row;
            long size = 0;
            for (int w = 0; w < words; w++)
            {
                for (ulong bits = row[w]; bits != 0; bits &= bits - 1)
                {
                    int scc = (w << 6) + BitOperations.TrailingZeroCount(bits);
                    size += memberStart[scc + 1] - memberStart[scc];
                }
            }
            reachSize[c] = (int)size;
            total += size * (memberStart[c + 1] - memberStart[c]);
            if (total > ArrayLimits.MaxArrayLength)
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                    $"the transitive closure has more than {ArrayLimits.MaxArrayLength}"
                    + $" edges, the largest array length"), nameof(graph));
            }
        }

        // Each component's sorted target list is written once, at its first node, and copied from
        // there to its other nodes; its bit row is dropped as soon as the list exists.
        var start = new int[n + 1];
        int[] sccOf = sccs.Mapping;
        for (int v = 0; v < n; v++)
        {
            start[v + 1] = start[v] + reachSize[sccOf[v]];
        }
        var targets = new int[(int)total];
        for (int c = 0; c < k; c++)
        {
            ulong[] row = reach[c];
            int first = start[members[memberStart[c]]];
            int at = first;
            for (int w = 0; w < words; w++)
            {
                for (ulong bits = row[w]; bits != 0; bits &= bits - 1)
                {
                    int scc = (w << 6) + BitOperations.TrailingZeroCount(bits);
                    for (int i = memberStart[scc], end = memberStart[scc + 1]; i < end; i++)
                    {
                        targets[at++] = members[i];
                    }
                }
            }
            targets.AsSpan(first, at - first).Sort();
            for (int i = memberStart[c] + 1; i < memberStart[c + 1]; i++)
            {
                Array.Copy(targets, first, targets, start[members[i]], at - first);
            }
            reach[c] = [];
        }
        return new Graph(start, targets);
    }

    /// <summary>
    /// Returns the transitive reduction of <paramref name="dag"/>: the same nodes, keeping each edge
    /// <c>u -> v</c> unless another path leads from <c>u</c> to <c>v</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// From each node, the result reaches the same nodes as <paramref name="dag"/>, with as few edges as
    /// possible; on a DAG, this result is unique. For a graph with cycles, reduce the DAG of its SCCs,
    /// which is <c>Components.SccsOf(graph).Condense()</c>.
    /// </para>
    /// <para>
    /// The reduction is computed with a row of bits per node for the nodes it reaches. The rows take
    /// NodeCount^2 / 8 bytes, about 1.25 GB for 100 000 nodes.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount log maxOutDegree + (NodeCount + EdgeCount) x NodeCount / 64).<br/>
    /// <b>Memory:</b> 4 bytes per node and per edge for the result; while it runs, NodeCount^2 / 8 bytes
    /// for the rows and O(NodeCount + EdgeCount) more.
    /// </para>
    /// </remarks>
    /// <param name="dag">The DAG to reduce.</param>
    /// <returns>The transitive reduction.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="dag"/> has a cycle, a self-loop included, or a depth-first path is longer than
    /// 2^30 - 29 nodes.
    /// </exception>
    public static Graph TransitiveReduction(Graph dag)
    {
        ArgumentNullException.ThrowIfNull(dag);
        int n = dag.NodeCount;
        ImmutableArray<int> order = TopOrder.Of(dag, nameof(dag));
        var position = new int[n];
        for (int i = 0; i < n; i++)
        {
            position[order[i]] = i;
        }
        int[] start = dag.OutStart;
        int[] targets = dag.OutAdj;
        int words = (n + 63) >>> 6;
        var reach = new ulong[n][];
        var keep = new bool[targets.Length];
        int kept = 0;
        var byPosition = new long[16];

        for (int i = n - 1; i >= 0; i--)
        {
            int u = order[i];
            int from = start[u];
            int degree = start[u + 1] - from;
            if (byPosition.Length < degree)
            {
                byPosition = new long[Math.Max(degree, 2 * byPosition.Length)];
            }
            // Sort key: topological position of the target high, edge index low.
            for (int j = 0; j < degree; j++)
            {
                byPosition[j] = ((long)position[targets[from + j]] << 32) | (uint)(from + j);
            }
            byPosition.AsSpan(0, degree).Sort();
            var row = new ulong[words];
            for (int j = 0; j < degree; j++)
            {
                int e = (int)byPosition[j];
                int v = targets[e];
                if ((row[v >> 6] & (1UL << v)) != 0)
                {
                    continue;
                }
                keep[e] = true;
                kept++;
                row[v >> 6] |= 1UL << v;
                ulong[] below = reach[v];
                for (int w = 0; w < words; w++)
                {
                    row[w] |= below[w];
                }
            }
            reach[u] = row;
        }

        var resultStart = new int[n + 1];
        var resultTargets = new int[kept];
        int next = 0;
        for (int v = 0; v < n; v++)
        {
            resultStart[v] = next;
            for (int e = start[v], end = start[v + 1]; e < end; e++)
            {
                if (keep[e])
                {
                    resultTargets[next++] = targets[e];
                }
            }
        }
        resultStart[n] = next;
        return new Graph(resultStart, resultTargets);
    }

    /// <summary>
    /// Returns the edge mapping from <paramref name="newGraph"/> to <paramref name="oldGraph"/> that goes
    /// with the node mapping <paramref name="newToOld"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Edge <c>s -> t</c> of <paramref name="newGraph"/> maps to the edge
    /// <c>newToOld.Get(s) -> newToOld.Get(t)</c> of <paramref name="oldGraph"/>, or to -1 if
    /// <paramref name="oldGraph"/> has no such edge or <c>s</c> or <c>t</c> maps to -1. For a graph from
    /// <c>Remapped(oldGraph, newToOld)</c>, every edge has its counterpart, and the <c>Gather</c> methods
    /// of the result carry data on the edges over to the new graph. For the opposite direction, from
    /// the edges of <paramref name="oldGraph"/> to those of <paramref name="newGraph"/>, invert the
    /// result with <see cref="Graphity.EdgeMapping.Inverted"/>. The transposed graph needs
    /// <see cref="TransposedEdgeMapping"/> instead, as its edges run the other way.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount log maxOutDegree), with the node and edge count of
    /// <paramref name="newGraph"/> and the out-degrees of <paramref name="oldGraph"/>.<br/>
    /// <b>Memory:</b> 4 bytes per edge of <paramref name="newGraph"/> for the result.
    /// </para>
    /// </remarks>
    /// <example>
    /// Here, <c>siteCount</c> holds the number of call sites of each call by edge index of
    /// <c>calls</c>, and the subgraph <c>loading</c> from the example at <see cref="Remapped"/> takes
    /// these counts along:
    /// <!-- example: Mappings.cs#edgeMapping -->
    /// <code language="csharp">
    /// int[] loadingSiteCount =
    ///     Graphs.EdgeMapping(loading, calls, newToOld).Gather(siteCount);
    /// // 1 for load -> parse and for load -> read, 2 for parse -> parse
    /// </code>
    /// </example>
    /// <param name="newGraph">The graph whose edges are mapped.</param>
    /// <param name="oldGraph">The graph they are mapped to.</param>
    /// <param name="newToOld">The node of <paramref name="oldGraph"/> for each node of <paramref name="newGraph"/>, or -1.</param>
    /// <returns>The edge mapping.</returns>
    /// <exception cref="ArgumentException"><c>newToOld.IndexCount != newGraph.NodeCount</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A node of <paramref name="newGraph"/> maps to an ID that is not a node of <paramref name="oldGraph"/>.
    /// </exception>
    public static EdgeMapping EdgeMapping(Graph newGraph, Graph oldGraph, NodeMapping newToOld)
    {
        ArgumentNullException.ThrowIfNull(newGraph);
        ArgumentNullException.ThrowIfNull(oldGraph);
        ArgumentNullException.ThrowIfNull(newToOld);
        int newNodeCount = newGraph.NodeCount;
        int[] oldOf = newToOld.Mapping;
        if (oldOf.Length != newNodeCount)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"newToOld has an index count of {oldOf.Length}, but newGraph has {newNodeCount} nodes"),
                nameof(newToOld));
        }
        int oldNodeCount = oldGraph.NodeCount;
        int[] newTargets = newGraph.OutAdj;
        int[] newStart = newGraph.OutStart;
        int[] oldTargets = oldGraph.OutAdj;
        int[] oldStart = oldGraph.OutStart;
        var mapping = new int[newGraph.EdgeCount];

        // Every entry is checked in its own node's iteration before the result is returned, so a
        // target needs no check: one that maps to -1 or beyond the old graph is simply not found.
        for (int newNode = 0; newNode < newNodeCount; newNode++)
        {
            int oldNode = oldOf[newNode];
            int newEnd = newStart[newNode + 1];
            if (oldNode >= oldNodeCount)
            {
                throw NotANode(newNode, oldNode, oldNodeCount);
            }
            if (oldNode < 0)
            {
                mapping.AsSpan(newStart[newNode], newEnd - newStart[newNode]).Fill(-1);
                continue;
            }
            int oldFrom = oldStart[oldNode];
            int oldEnd = oldStart[oldNode + 1];
            for (int i = newStart[newNode]; i < newEnd; i++)
            {
                int oldTarget = oldOf[newTargets[i]];
                mapping[i] = SortedIntView.IndexOfInRange(oldTargets, oldFrom, oldEnd, oldTarget);
            }
        }
        return new EdgeMapping(mapping, IndexMapping.CountValid(mapping));
    }

    /// <summary>
    /// Returns the edge mapping from the transposed graph of <paramref name="graph"/> to
    /// <paramref name="graph"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Edge <c>e</c> of <c>BiAdjacentGraph.Of(graph).Transposed()</c> maps to the edge of
    /// <paramref name="graph"/> it reverses: the transposed graph keeps the node IDs, but not the edge
    /// indices. The mapping is total, and its <c>Gather</c> methods carry data on the edges over to the
    /// transposed graph. <see cref="BiAdjacentGraph.TransposedEdgeIndex"/> translates a single edge, in
    /// the opposite direction.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 4 x EdgeCount bytes for the result; while it runs, 4 x NodeCount bytes more.
    /// </para>
    /// </remarks>
    /// <example>
    /// Here, <c>siteCount</c> holds the number of call sites of each call by edge index of
    /// <c>calls</c>, and the counts follow the calls to the transposed graph, which lists the callers
    /// of <c>parse</c>:
    /// <!-- example: Mappings.cs#transposedEdges -->
    /// <code language="csharp">
    /// BiAdjacentGraph calledBy = BiAdjacentGraph.Of(calls).Transposed();
    /// int[] calledBySiteCount = Graphs.TransposedEdgeMapping(calls).Gather(siteCount);
    /// for (int edge = calledBy.OutEdgeStart(parse);
    ///      edge &lt; calledBy.OutEdgeEnd(parse);
    ///      edge++)
    /// {
    ///     output.WriteLine(
    ///         $"{names[calledBy.EdgeTarget(edge)]}: {calledBySiteCount[edge]}");
    /// }
    /// // load: 1
    /// // parse: 2
    /// </code>
    /// </example>
    /// <param name="graph">The graph whose transposed graph is mapped to it.</param>
    /// <returns>The edge mapping from the transposed graph to <paramref name="graph"/>.</returns>
    public static EdgeMapping TransposedEdgeMapping(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return Graphity.EdgeMapping.ForTransposed(graph);
    }

    /// <summary>
    /// Returns the edge partition that goes with the node partition <paramref name="partition"/>, from
    /// the edges of the partitioned graph to the edges of <paramref name="condensedGraph"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edges from group <c>g</c> to group <c>h</c> all map to the condensed edge <c>g -> h</c>, which
    /// is edge <c>condensedGraph.EdgeIndexOf(g, h)</c>. <see cref="Graphity.EdgePartition.EdgesOf"/>
    /// lists them, and the <c>Reduce</c> methods of the result combine their data. An edge inside a
    /// group, or from or to a node in no group, maps to -1. <paramref name="condensedGraph"/> must be
    /// <c>partition.Condense()</c>; only its node count is checked.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount log maxOutDegree), with the node and edge count of the
    /// partitioned graph and the out-degrees of <paramref name="condensedGraph"/>.<br/>
    /// <b>Memory:</b> up to 12 bytes per edge of the partitioned graph for the result.
    /// </para>
    /// </remarks>
    /// <example>
    /// Here, <c>byPackage</c> groups the methods of a program by package. Its condensed graph
    /// <c>packageCalls</c> holds the calls between packages (see <see cref="NodePartition.Of"/>).
    /// <c>byPackageCall</c> tells which calls between methods are behind the call from <c>service</c> to
    /// <c>model</c>:
    /// <!-- example: Partitions.cs#edgesOf -->
    /// <code language="csharp">
    /// EdgePartition byPackageCall = Graphs.EdgePartition(packageCalls, byPackage);
    /// int serviceToModel = packageCalls.EdgeIndexOf(service, model);
    /// SortedIntView behind = byPackageCall.EdgesOf(serviceToModel);
    /// // behind: [5, 7] - the calls load -> parse and save -> format
    /// </code>
    /// </example>
    /// <param name="condensedGraph">The graph that <c>partition.Condense()</c> returned.</param>
    /// <param name="partition">The partition of the nodes.</param>
    /// <returns>The edge partition, with a group per edge of <paramref name="condensedGraph"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <c>condensedGraph.NodeCount</c> is not <c>partition.GroupCount</c>.
    /// </exception>
    public static EdgePartition EdgePartition(Graph condensedGraph, NodePartition partition)
    {
        ArgumentNullException.ThrowIfNull(condensedGraph);
        ArgumentNullException.ThrowIfNull(partition);
        int groupCount = partition.GroupCount;
        if (condensedGraph.NodeCount != groupCount)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"condensedGraph has {condensedGraph.NodeCount} nodes, but the partition has {groupCount} groups"),
                nameof(condensedGraph));
        }
        Graph graph = partition.SourceGraph;
        var mapping = new int[graph.EdgeCount];
        Array.Fill(mapping, -1);
        int[] groupOf = partition.Mapping;
        int[] targets = graph.OutAdj;
        int[] start = graph.OutStart;
        int[] condensedTargets = condensedGraph.OutAdj;
        int[] condensedStart = condensedGraph.OutStart;

        // 1) Condensed edge of every cross-group edge.
        for (int node = 0; node < graph.NodeCount; node++)
        {
            int sourceGroup = groupOf[node];
            if (sourceGroup < 0)
            {
                continue;
            }
            for (int ei = start[node], end = start[node + 1]; ei < end; ei++)
            {
                int targetGroup = groupOf[targets[ei]];
                if (sourceGroup != targetGroup && targetGroup >= 0)
                {
                    mapping[ei] = SortedIntView.IndexOfInRange(condensedTargets,
                        condensedStart[sourceGroup], condensedStart[sourceGroup + 1], targetGroup);
                }
            }
        }

        // 2) Original edges per condensed edge, as CSR.
        var groups = ParentForest.GroupIndices(mapping, condensedGraph.EdgeCount);
        return new EdgePartition(mapping, groups.Members, groups.Start);
    }

    /// <summary>
    /// Returns the root nodes of <paramref name="graph"/> in ascending order. A root node has in-degree
    /// 0.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A traversal of a DAG or a forest starts from them, as in
    /// <c>traverser.Run(Graphs.RootNodes(graph))</c> with a <see cref="DfsTraverser"/>.
    /// <see cref="BiAdjacentGraph.IsRootNode"/> checks a single node in O(1).
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount).<br/>
    /// <b>Memory:</b> 4 bytes per root node for the result.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to search.</param>
    /// <returns>The root nodes.</returns>
    public static SortedIntView RootNodes(BiAdjacentGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        int[] inStart = graph.InStart;
        int count = 0;
        for (int node = 0; node < n; node++)
        {
            if (inStart[node] == inStart[node + 1])
            {
                count++;
            }
        }
        var roots = new int[count];
        int next = 0;
        for (int node = 0; node < n; node++)
        {
            if (inStart[node] == inStart[node + 1])
            {
                roots[next++] = node;
            }
        }
        return new SortedIntView(roots, 0, count);
    }

    /// <summary>
    /// Returns the nodes reachable from <paramref name="source"/>, in ascending order and with
    /// <paramref name="source"/> included. Equivalent to <c>ReachableFrom(graph, [source])</c>.
    /// </summary>
    /// <remarks>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> as for <see cref="ReachableFrom(Graph, ReadOnlySpan{int})"/>.
    /// </remarks>
    /// <param name="graph">The graph to search.</param>
    /// <param name="source">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The reachable nodes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="source"/> is out of range.</exception>
    public static SortedIntView ReachableFrom(Graph graph, int source)
    {
        ArgumentNullException.ThrowIfNull(graph);
        Checks.Index(source, graph.NodeCount);
        return ReachableFrom(graph, [source]);
    }

    /// <summary>
    /// Returns the nodes reachable from at least one of <paramref name="sources"/>, in ascending order
    /// and with the sources included.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The search follows the edges in their direction; for the nodes from which a source is reachable,
    /// search the transposed graph, as shown at <see cref="BiAdjacentGraph.Transposed"/>. A source given
    /// twice counts once, and with no sources, no node is reachable. Being sorted, the result goes
    /// straight into <see cref="NodeMapping.ForSubgraph(SortedIntView)"/> for the subgraph of the
    /// reachable nodes, as shown at <see cref="Remapped"/>. For distances and shortest paths, use
    /// <see cref="BfsDistances"/>.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(sources.Length + NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 4 bytes per reachable node for the result; while it runs, 1 bit per node and
    /// briefly up to 12 bytes per reachable node.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to search.</param>
    /// <param name="sources">Node IDs, each in <c>0..NodeCount-1</c>.</param>
    /// <returns>The reachable nodes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A source is out of range.</exception>
    public static SortedIntView ReachableFrom(Graph graph, ReadOnlySpan<int> sources)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        Checks.Nodes(sources, n);
        int[] outStart = graph.OutStart;
        int[] adj = graph.OutAdj;
        var visited = new ulong[(n >> 6) + 1];
        // Every reachable node enters the queue once, so the queue grows into the result.
        var queue = new int[Math.Min(n, Math.Max(16, sources.Length))];
        int tail = 0;
        foreach (int s in sources)
        {
            if ((visited[s >> 6] & (1UL << s)) == 0)
            {
                visited[s >> 6] |= 1UL << s;
                queue[tail++] = s;
            }
        }
        for (int head = 0; head < tail; head++)
        {
            int u = queue[head];
            for (int e = outStart[u], end = outStart[u + 1]; e < end; e++)
            {
                int v = adj[e];
                if ((visited[v >> 6] & (1UL << v)) == 0)
                {
                    visited[v >> 6] |= 1UL << v;
                    if (tail == queue.Length)
                    {
                        Array.Resize(ref queue, (int)Math.Min(n, 2L * queue.Length));
                    }
                    queue[tail++] = v;
                }
            }
        }
        // The queue is done; refill it in ascending order from the bit set, at its exact length.
        if (tail < queue.Length)
        {
            queue = queue[..tail];
        }
        int k = 0;
        for (int w = 0; w < visited.Length; w++)
        {
            for (ulong bits = visited[w]; bits != 0; bits &= bits - 1)
            {
                queue[k++] = (w << 6) + BitOperations.TrailingZeroCount(bits);
            }
        }
        return new SortedIntView(queue, 0, tail);
    }

    /// <summary>
    /// Returns whether <paramref name="graph"/> is a DAG: a graph without cycles. A self-loop is a cycle.
    /// </summary>
    /// <remarks>
    /// <para><see cref="FindCycle"/> returns a cycle where this method finds one.</para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount), stopping at the first cycle.<br/>
    /// <b>Memory:</b> 2 bits per node while it runs, and up to 8 x NodeCount bytes for the stack, which
    /// grows with the deepest path.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to check.</param>
    /// <returns><see langword="true"/> if <paramref name="graph"/> has no cycle.</returns>
    /// <exception cref="ArgumentException">A depth-first path is longer than 2^30 - 29 nodes.</exception>
    public static bool IsDag(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return FindCycleCore(graph, false, out _);
    }

    /// <summary>
    /// Returns the nodes of a cycle of <paramref name="graph"/> in the order of its edges, or an empty
    /// list if <paramref name="graph"/> is a DAG.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The last node has an edge to the first, and a self-loop is a cycle of one node. The search goes
    /// depth-first, from each unvisited node in ascending order and through the out-nodes in ascending
    /// order, and returns the first cycle it closes: always the same one, but not necessarily the
    /// shortest. For a cycle inside a given SCC, search the subgraph of its nodes from
    /// <see cref="Remapped"/>; the node mapping of the subgraph translates the result back.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount), stopping at the first cycle.<br/>
    /// <b>Memory:</b> 4 bytes per node of the cycle for the result; while it runs, 2 bits per node and up
    /// to 8 x NodeCount bytes for the stack, which grows with the deepest path.
    /// </para>
    /// </remarks>
    /// <example>
    /// Here, the cycle in the dependencies between the packages of a program:
    /// <!-- example: GraphOperations.cs#findCycle -->
    /// <code language="csharp">
    /// ImmutableArray&lt;int> cycle = Graphs.FindCycle(dependencies);
    /// // cycle: [4, 5] - model and storage, closed by the edge storage -> model
    /// </code>
    /// </example>
    /// <param name="graph">The graph to search.</param>
    /// <returns>The nodes of the cycle, each once, or an empty list.</returns>
    /// <exception cref="ArgumentException">A depth-first path is longer than 2^30 - 29 nodes.</exception>
    public static ImmutableArray<int> FindCycle(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        FindCycleCore(graph, true, out int[]? cycle);
        return cycle is null ? [] : ImmutableCollectionsMarshal.AsImmutableArray(cycle);
    }

    /// <summary>
    /// Returns whether <paramref name="graph"/> is a forest: a DAG in which every node has in-degree at
    /// most 1, so that the edges point away from the root nodes.
    /// </summary>
    /// <remarks>
    /// <para>The empty graph is a forest. For the undirected reading, see <see cref="IsUndirectedForest"/>.</para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 8 x NodeCount bytes while it runs.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to check.</param>
    /// <returns><see langword="true"/> if <paramref name="graph"/> is a forest.</returns>
    public static bool IsForest(Graph graph) => ComputeForestInfo(graph) is not null;

    /// <summary>
    /// Returns whether <paramref name="graph"/> is a tree: a forest with at most one root node.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edges of a tree point away from its root. For a tree whose edges point toward the root, check
    /// the transposed graph, which is <c>BiAdjacentGraph.Of(graph).Transposed()</c>. For the undirected
    /// reading, see <see cref="IsUndirectedTree"/>. The empty graph is a tree.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 8 x NodeCount bytes while it runs.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to check.</param>
    /// <returns><see langword="true"/> if <paramref name="graph"/> is a tree.</returns>
    public static bool IsTree(Graph graph) => ComputeForestInfo(graph) is { RootCount: <= 1 };

    /// <summary>
    /// Returns whether <paramref name="graph"/> is a path: a tree in which every node has out-degree at
    /// most 1.
    /// </summary>
    /// <remarks>
    /// <para>The empty graph is a path.</para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 8 x NodeCount bytes while it runs.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to check.</param>
    /// <returns><see langword="true"/> if <paramref name="graph"/> is a path.</returns>
    public static bool IsPath(Graph graph) => ComputeForestInfo(graph) is { RootCount: <= 1, MaxOutDegree: <= 1 };

    /// <summary>
    /// Returns whether <paramref name="graph"/> is strongly connected: every node reaches every other
    /// along the edges.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Answers without the partition of <see cref="Components.SccsOf"/>. Uses
    /// <see href="https://en.wikipedia.org/wiki/Tarjan%27s_strongly_connected_components_algorithm">Tarjan's algorithm</see>,
    /// without recursion. It starts from node 0 and stops as soon as an SCC without node 0 is complete.
    /// Where the SCCs are already computed, <see cref="Sccs.IsStronglyConnected"/> answers in O(1). The
    /// empty graph is strongly connected, and so is a single node. For the undirected reading, see
    /// <see cref="IsWeaklyConnected"/>.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount), stopping at the first SCC without node 0.<br/>
    /// <b>Memory:</b> 8 x NodeCount bytes while it runs, and up to 8 x NodeCount bytes for the stack,
    /// which grows with the deepest path.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to check.</param>
    /// <returns><see langword="true"/> if every node reaches every other.</returns>
    /// <exception cref="ArgumentException">A depth-first path is longer than 2^30 - 29 nodes.</exception>
    public static bool IsStronglyConnected(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        if (n <= 1)
        {
            return true;
        }
        int[] outStart = graph.OutStart;
        int[] adj = graph.OutAdj;
        var index = new int[n]; // discovery time from 1; 0 = unvisited
        var low = new int[n];
        int[] frames = DfsFrames.Initial(n);
        int time = 1;
        index[0] = 1;
        low[0] = 1;
        frames[0] = 0;
        frames[1] = outStart[0];
        int top = 0;

        while (true)
        {
            int u = frames[2 * top];
            int end = outStart[u + 1];
            int i = frames[2 * top + 1];
            bool descended = false;
            while (i < end)
            {
                int v = adj[i++];
                if (index[v] == 0)
                {
                    frames[2 * top + 1] = i;
                    index[v] = ++time;
                    low[v] = time;
                    if (++top == frames.Length >> 1)
                    {
                        frames = DfsFrames.Grow(frames, n);
                    }
                    frames[2 * top] = v;
                    frames[2 * top + 1] = outStart[v];
                    descended = true;
                    break;
                }
                if (index[v] < low[u])
                {
                    low[u] = index[v];
                }
            }
            if (descended)
            {
                continue;
            }
            if (top == 0)
            {
                return time == n;
            }
            if (low[u] == index[u])
            {
                return false;
            }
            int parent = frames[2 * (top - 1)];
            if (low[u] < low[parent])
            {
                low[parent] = low[u];
            }
            top--;
        }
    }

    /// <summary>
    /// Iterative DFS for cycle detection, packed visited/on-stack state, stopping at the first back
    /// edge (self-loops included).
    /// </summary>
    /// <returns>Whether the graph is acyclic.</returns>
    private static bool FindCycleCore(Graph graph, bool recordCycle, out int[]? cycle)
    {
        cycle = null;
        int n = graph.NodeCount;
        int[] outStart = graph.OutStart;
        int[] adj = graph.OutAdj;
        var state = new int[(n >> 4) + 1];
        int[] frames = DfsFrames.Initial(n);

        for (int root = 0; root < n; root++)
        {
            int rShift = (root & 0xF) << 1;
            if (((state[root >> 4] >>> rShift) & Visited) != 0)
            {
                continue;
            }
            state[root >> 4] |= (Visited | OnStack) << rShift;
            frames[0] = root;
            frames[1] = outStart[root];
            int top = 0;

            while (top >= 0)
            {
                int u = frames[2 * top];
                int end = outStart[u + 1];
                int i = frames[2 * top + 1];
                bool descended = false;
                while (i < end)
                {
                    int v = adj[i++];
                    int shift = (v & 0xF) << 1;
                    int flags = state[v >> 4] >>> shift;
                    if ((flags & OnStack) != 0)
                    {
                        if (recordCycle)
                        {
                            // The path from v up to u, the top, is on the frame stack: u -> v closes it.
                            int at = top;
                            while (frames[2 * at] != v)
                            {
                                at--;
                            }
                            cycle = new int[top - at + 1];
                            for (int k = 0; k < cycle.Length; k++)
                            {
                                cycle[k] = frames[2 * (at + k)];
                            }
                        }
                        return false;
                    }
                    if ((flags & Visited) == 0)
                    {
                        frames[2 * top + 1] = i;
                        state[v >> 4] |= (Visited | OnStack) << shift;
                        if (++top == frames.Length >> 1)
                        {
                            frames = DfsFrames.Grow(frames, n);
                        }
                        frames[2 * top] = v;
                        frames[2 * top + 1] = outStart[v];
                        descended = true;
                        break;
                    }
                }
                if (descended)
                {
                    continue;
                }
                state[u >> 4] &= ~(OnStack << ((u & 0xF) << 1));
                top--;
            }
        }
        return true;
    }

    /// <summary>
    /// The root count and largest out-degree of <paramref name="graph"/> if it is a forest, or null if
    /// a node has in-degree &gt; 1 or lies on or below a cycle. <see cref="Ancestry.Of(Graph)"/> makes
    /// the same check, with a witness for its message.
    /// </summary>
    private static ForestInfo? ComputeForestInfo(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        int[] targets = graph.OutAdj;
        int[] start = graph.OutStart;
        var inDegree = new int[n];
        foreach (int target in targets)
        {
            if (++inDegree[target] > 1)
            {
                return null;
            }
        }
        // With every in-degree <= 1, a BFS from the roots enqueues each node at most once; a node it
        // misses lies on a cycle or below one.
        var queue = new int[n];
        int head = 0;
        int tail = 0;
        int maxOutDegree = 0;
        for (int i = 0; i < n; i++)
        {
            int outDegree = start[i + 1] - start[i];
            if (outDegree > maxOutDegree)
            {
                maxOutDegree = outDegree;
            }
            if (inDegree[i] == 0)
            {
                queue[tail++] = i;
            }
        }
        int rootCount = tail;
        while (head < tail)
        {
            int node = queue[head++];
            for (int i = start[node], end = start[node + 1]; i < end; i++)
            {
                queue[tail++] = targets[i];
            }
        }
        return tail == n ? new ForestInfo(rootCount, maxOutDegree) : null;
    }

    /// <summary>The root count and the largest out-degree of a forest.</summary>
    private readonly record struct ForestInfo(int RootCount, int MaxOutDegree);

    /// <summary>
    /// Returns whether <paramref name="graph"/> is bipartite: its nodes split into two sets such that
    /// every edge runs from one set to the other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads the graph as undirected, so the direction of the edges does not matter. A self-loop makes
    /// the graph not bipartite, unlike the other checks of the undirected reading, which ignore
    /// self-loops. The empty graph is bipartite.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount x alpha(NodeCount)), stopping at the first cycle of odd
    /// length.<br/>
    /// <b>Memory:</b> 10 x NodeCount bytes while it runs.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to check.</param>
    /// <returns><see langword="true"/> if <paramref name="graph"/> is bipartite.</returns>
    /// <exception cref="ArgumentException"><paramref name="graph"/> has more than 2^30 - 29 nodes.</exception>
    public static bool IsBipartite(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        ArrayLimits.CheckNodeCount(n, ArrayLimits.MaxNodeCountTwoInts, nameof(IsBipartite));
        int[] targets = graph.OutAdj;
        int[] start = graph.OutStart;
        // Double cover: element u means "u in class A", u + n "u in class B". An edge ties each
        // endpoint to the other's opposite; once a node shares a set with its own opposite, there is
        // an odd cycle. Both unions are symmetric in u and v.
        int[] parent = UnionFind.Singletons(2 * n);
        byte[] rank = new byte[2 * n];
        for (int u = 0; u < n; u++)
        {
            for (int ei = start[u], end = start[u + 1]; ei < end; ei++)
            {
                int v = targets[ei];
                if (u == v)
                {
                    return false;
                }
                UnionFind.Union(parent, rank, u, v + n);
                UnionFind.Union(parent, rank, u + n, v);
                if (UnionFind.Find(parent, u) == UnionFind.Find(parent, u + n))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Returns whether <paramref name="graph"/> is a tree when read as undirected: connected and without
    /// cycles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike for <see cref="IsTree"/>, the direction of the edges does not matter: edges toward the
    /// root, or in mixed directions, make a tree as well. The edges <c>a -> b</c> and <c>b -> a</c> count
    /// as one, so a tree may store its edges in both directions, and self-loops are ignored. The empty
    /// graph is a tree.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount x alpha(NodeCount)), plus O(log maxOutDegree) for each pair
    /// <c>a -> b</c>, <c>b -> a</c>; O(1) for more than 3 x NodeCount - 2 edges, too many for any
    /// tree.<br/>
    /// <b>Memory:</b> 5 x NodeCount bytes while it runs.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to check.</param>
    /// <returns><see langword="true"/> if <paramref name="graph"/> is a tree when read as undirected.</returns>
    public static bool IsUndirectedTree(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        if (n == 0)
        {
            return true;
        }
        if (ExceedsAcyclicEdgeBound(graph))
        {
            return false;
        }
        // A cycle yields -1, which never equals n - 1 here: one comparison covers acyclicity and
        // connectedness.
        return UndirectedMergeCount(graph) == n - 1;
    }

    /// <summary>
    /// Returns whether <paramref name="graph"/> is a forest when read as undirected: without cycles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike for <see cref="IsForest"/>, the direction of the edges does not matter. The edges
    /// <c>a -> b</c> and <c>b -> a</c> count as one, so they make no cycle, and self-loops are ignored.
    /// The empty graph is a forest.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount x alpha(NodeCount)), plus O(log maxOutDegree) for each pair
    /// <c>a -> b</c>, <c>b -> a</c>; O(1) for more than 3 x NodeCount - 2 edges, too many for any
    /// forest.<br/>
    /// <b>Memory:</b> 5 x NodeCount bytes while it runs.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to check.</param>
    /// <returns><see langword="true"/> if <paramref name="graph"/> is a forest when read as undirected.</returns>
    public static bool IsUndirectedForest(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return !ExceedsAcyclicEdgeBound(graph) && UndirectedMergeCount(graph) >= 0;
    }

    /// <summary>
    /// Returns whether <paramref name="graph"/> is weakly connected: connected when the direction of the
    /// edges is ignored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Answers without the partition of <see cref="Components.WccsOf"/>, and stops as soon as all nodes
    /// are connected. The empty graph is weakly connected, and so is a single node. For the directed
    /// reading, see <see cref="IsStronglyConnected"/>.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount x alpha(NodeCount)), stopping once all nodes are
    /// connected.<br/>
    /// <b>Memory:</b> 5 x NodeCount bytes while it runs.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph to check.</param>
    /// <returns><see langword="true"/> if <paramref name="graph"/> is weakly connected.</returns>
    public static bool IsWeaklyConnected(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        if (n <= 1)
        {
            return true;
        }
        int[] targets = graph.OutAdj;
        int[] start = graph.OutStart;
        // Reciprocal pairs need no attention: a second union of the same two nodes merges nothing.
        int[] parent = UnionFind.Singletons(n);
        byte[] rank = new byte[n];
        int merges = 0;
        for (int u = 0; u < n; u++)
        {
            for (int ei = start[u], end = start[u + 1]; ei < end; ei++)
            {
                if (UnionFind.Union(parent, rank, u, targets[ei]) && ++merges == n - 1)
                {
                    return true; // n - 1 merges join every node
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Whether the graph holds more edges than any acyclic undirected reading could: NodeCount - 1
    /// undirected edges in up to two directions, plus one self-loop per node.
    /// </summary>
    private static bool ExceedsAcyclicEdgeBound(Graph graph)
    {
        int n = graph.NodeCount;
        return graph.EdgeCount > (n == 0 ? 0 : (3L * n) - 2);
    }

    /// <summary>
    /// Merges the endpoints of every undirected edge and returns the number of merges, so that the
    /// nodes form NodeCount - result components; or -1 as soon as an edge closes an undirected
    /// cycle. Self-loops are skipped. Nodes are walked in ascending order: a failed union for
    /// <c>v &gt; u</c> is a cycle, for <c>v &lt; u</c> one binary search in v's out-adjacency decides
    /// whether it is the second stored direction of an edge already counted.
    /// </summary>
    private static int UndirectedMergeCount(Graph graph)
    {
        int n = graph.NodeCount;
        int[] targets = graph.OutAdj;
        int[] start = graph.OutStart;
        int[] parent = UnionFind.Singletons(n);
        byte[] rank = new byte[n];
        int merges = 0;
        for (int u = 0; u < n; u++)
        {
            for (int ei = start[u], end = start[u + 1]; ei < end; ei++)
            {
                int v = targets[ei];
                if (u == v)
                {
                    continue;
                }
                if (UnionFind.Union(parent, rank, u, v))
                {
                    merges++;
                }
                else if (v > u || !SortedIntView.ContainsInRange(targets, start[v], start[v + 1], u))
                {
                    return -1;
                }
            }
        }
        return merges;
    }

    /// <summary>
    /// Returns the whole graph as text: one line per node with its out-nodes, in ascending order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each line ends with a line feed. Unlike <see cref="Graph.ToString"/>, which is cut short for
    /// large graphs, this lists every node and edge; a <see cref="BiAdjacentGraph"/> lists its
    /// out-nodes. The text of a graph with more than about
    /// 100 million edges is longer than a <see cref="string"/> can be (2^30 - 33 characters) and ends in
    /// an <see cref="OutOfMemoryException"/>.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 2 bytes per character for the result; while it runs, about as much again.
    /// </para>
    /// </remarks>
    /// <example>
    /// For the control flow of a method with a loop:
    /// <!-- example: GraphOperations.cs#adjacencyListOf -->
    /// <code language="csharp">
    /// string text = Graphs.AdjacencyListOf(cfg);
    /// // 0 -> [1]
    /// // 1 -> [2, 5]
    /// // 2 -> [3, 4]
    /// // 3 -> [4]
    /// // 4 -> [1]
    /// // 5 -> []
    /// </code>
    /// </example>
    /// <param name="graph">The graph to list.</param>
    /// <returns>The adjacency list, one line per node.</returns>
    public static string AdjacencyListOf(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        int[] start = graph.OutStart;
        int[] targets = graph.OutAdj;
        // The builder allocates its capacity at once; more than the longest string would be wasted.
        var sb = new StringBuilder((int)Math.Min(MaxStringLength,
            ((long)ListCharsPerNode * n) + ((long)ListCharsPerEdge * graph.EdgeCount)));
        for (int v = 0; v < n; v++)
        {
            sb.Append(v).Append(" -> [");
            for (int e = start[v], end = start[v + 1]; e < end; e++)
            {
                if (e > start[v])
                {
                    sb.Append(", ");
                }
                sb.Append(targets[e]);
            }
            sb.Append("]\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Returns a 64-bit hash of the nodes and edges of <paramref name="graph"/>, consistent with
    /// <see cref="Graph.Equals(Graph?)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Equal graphs have equal hashes, and different graphs rarely do. A
    /// <see cref="BiAdjacentGraph"/> and a <see cref="Graph"/> with the same nodes and edges count
    /// as equal. Like <see cref="Graph.GetHashCode"/>, the hash is computed on every call: compute
    /// it once and keep it, for instance as the key of a cache of results per graph, or to see when
    /// a transformation stops changing a graph. It depends on nothing but the nodes and edges, so
    /// it is the same in every run and on every platform; a release of Graphity that changes it
    /// says so in its changelog. Every implementation of Graphity computes the same hash. Equal
    /// hashes do not prove equal graphs, and graphs can be made to collide on purpose: compare with
    /// <see cref="Graph.Equals(Graph?)"/> where it matters.
    /// </para>
    /// <para><b>Time:</b> O(NodeCount + EdgeCount).</para>
    /// </remarks>
    /// <param name="graph">The graph to hash.</param>
    /// <returns>The hash.</returns>
    public static long TopologyHash(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        int[] start = graph.OutStart;
        int[] targets = graph.OutAdj;
        ulong h = MixInto(HashSeed, n);
        h = MixInto(h, graph.EdgeCount);
        for (int v = 0; v < n; v++)
        {
            int end = start[v + 1];
            h = MixInto(h, end - start[v]);
            for (int e = start[v]; e < end; e++)
            {
                h = MixInto(h, targets[e]);
            }
        }
        // SplitMix64 finalizer: every input bit reaches every output bit.
        h = (h ^ (h >> 30)) * SplitMix64Multiplier1;
        h = (h ^ (h >> 27)) * SplitMix64Multiplier2;
        return (long)(h ^ (h >> 31));
    }

    // One step of the hash: mixes value into h.
    private static ulong MixInto(ulong h, int value) =>
        BitOperations.RotateLeft(h ^ ((ulong)(long)value * HashGoldenGamma), 29) * HashStepPrime;

    /// <summary>
    /// Returns the edges added and removed between <paramref name="before"/> and
    /// <paramref name="after"/>. Both are graphs over the same nodes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Added edges come with their edge indices in <paramref name="after"/>, removed edges with theirs in
    /// <paramref name="before"/>. With these edge indices, the caller can look up data on the edges of
    /// either graph in arrays indexed by edge index. Both lists are in edge index order. An empty diff
    /// means that the graphs are equal.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount), with the edges of both graphs.<br/>
    /// <b>Memory:</b> 12 bytes per added or removed edge for the result; while it runs, up to 18 bytes
    /// more per such edge.
    /// </para>
    /// </remarks>
    /// <param name="before">The older graph.</param>
    /// <param name="after">The newer graph, with the same node count.</param>
    /// <returns>The added and removed edges.</returns>
    /// <exception cref="ArgumentException">The node counts differ.</exception>
    public static GraphDiff Diff(Graph before, Graph after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        int n = before.NodeCount;
        if (after.NodeCount != n)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"diff needs graphs over the same nodes, but before has {n} nodes and after {after.NodeCount}"),
                nameof(after));
        }
        int[] oldStart = before.OutStart;
        int[] oldTargets = before.OutAdj;
        int[] newStart = after.OutStart;
        int[] newTargets = after.OutAdj;
        // Parallel lists, so that none needs more entries than its graph has edges.
        var addedSources = new IntArrayList();
        var addedTargets = new IntArrayList();
        var addedEdgeIndices = new IntArrayList();
        var removedSources = new IntArrayList();
        var removedTargets = new IntArrayList();
        var removedEdgeIndices = new IntArrayList();
        for (int v = 0; v < n; v++)
        {
            int oldPos = oldStart[v];
            int oldEnd = oldStart[v + 1];
            int newPos = newStart[v];
            int newEnd = newStart[v + 1];
            // No target reaches int.MaxValue - node IDs stay below it -, so it marks an exhausted list.
            while (oldPos < oldEnd || newPos < newEnd)
            {
                int oldTarget = oldPos < oldEnd ? oldTargets[oldPos] : int.MaxValue;
                int newTarget = newPos < newEnd ? newTargets[newPos] : int.MaxValue;
                if (oldTarget == newTarget)
                {
                    oldPos++;
                    newPos++;
                }
                else if (oldTarget < newTarget)
                {
                    removedSources.Add(v);
                    removedTargets.Add(oldTarget);
                    removedEdgeIndices.Add(oldPos++);
                }
                else
                {
                    addedSources.Add(v);
                    addedTargets.Add(newTarget);
                    addedEdgeIndices.Add(newPos++);
                }
            }
        }
        // Both lists are in edge index order already, which SortedEdges detects and only copies.
        return new GraphDiff(
            Edges.SortedEdges(after, addedSources.Elements, addedTargets.Elements, addedEdgeIndices.Elements,
                addedSources.Count, false),
            Edges.SortedEdges(before, removedSources.Elements, removedTargets.Elements, removedEdgeIndices.Elements,
                removedSources.Count, false));
    }
}
