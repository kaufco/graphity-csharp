using System.Collections;
using System.Globalization;
using System.Numerics;

namespace Graphity;

/// <summary>
/// A mapping from the node IDs of one graph to those of another, to extract a subgraph or to
/// renumber the nodes of a graph.
/// </summary>
/// <remarks>
/// <para>
/// <c>Get(node)</c> is the node ID that <c>node</c> maps to, or -1 if <c>node</c> is not mapped.
/// The factories return mappings from a new graph to an old one. These map each new node to the
/// node it comes from:
/// </para>
/// <list type="bullet">
///   <item><description>
///   <see cref="ForSubgraph(SortedIntView)"/> and its overloads - the selected nodes, numbered in
///   ascending order;
///   </description></item>
///   <item><description><see cref="ForPermutation"/> - every node, in an order of the caller's choice;</description></item>
///   <item><description><see cref="Reindex"/> - every node, in depth-first preorder;</description></item>
///   <item><description><see cref="Of"/> - any mapping, from an array.</description></item>
/// </list>
/// <para>
/// <see cref="Graphs.Remapped"/> builds the new graph from the old one, and <c>Gather</c> carries
/// data on the old nodes, such as their names, over to the new ones.
/// <see cref="Graphs.EdgeMapping"/> derives the <see cref="EdgeMapping"/> that carries data on
/// edges. <see cref="Inverted"/> returns the mapping from old to new, and <see cref="Compose"/>
/// chains two mappings.
/// </para>
/// <para>Instances are immutable and safe to share between threads.</para>
/// <para><b>Memory:</b> 4 x IndexCount bytes.</para>
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
public sealed class NodeMapping : IndexMapping
{
    // Takes the array as it is, not copied: the caller hands over an array that nobody else holds,
    // and validCount matches it.
    internal NodeMapping(int[] mapping, int validCount)
        : base(mapping, validCount)
    {
    }

    /// <summary>
    /// Returns the mapping that maps each node <c>i</c> to <c>mapping[i]</c>, and leaves it unmapped
    /// where that is -1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use it for mappings that the other factories do not cover, from new to old or from old to new.
    /// The span is copied. Each value must be -1 or a node ID, and no two nodes may map to the
    /// same node; without a graph, this method can only reject a value below -1.
    /// <see cref="Graphs.Remapped"/> rejects a value that is not a node of its graph, and both it and
    /// <see cref="Inverted"/> reject two nodes that map to the same node.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(mapping.Length).<br/>
    /// <b>Memory:</b> 4 x mapping.Length bytes for the copy, which the result keeps.
    /// </para>
    /// </remarks>
    /// <param name="mapping">The node ID that each node maps to, or -1.</param>
    /// <returns>The mapping.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A value is less than -1.</exception>
    public static NodeMapping Of(ReadOnlySpan<int> mapping)
    {
        int[] copy = mapping.ToArray();
        for (int node = 0; node < copy.Length; node++)
        {
            if (copy[node] < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(mapping), copy[node],
                    string.Create(CultureInfo.InvariantCulture,
                        $"a mapping value must be -1 or a node ID, but node {node} maps to {copy[node]}"));
            }
        }
        return new NodeMapping(copy, CountValid(copy));
    }

    /// <summary>
    /// Returns a mapping from the nodes of a subgraph to the selected nodes, given as the set bits of
    /// <paramref name="selected"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The selected nodes get the new node IDs <c>0..k-1</c>, in ascending order of their node IDs,
    /// where k is the number of set bits. The selected node IDs are checked against a graph only when
    /// the mapping is used: <see cref="Graphs.Remapped"/> rejects one that is not a node of its
    /// graph.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(selected.Length).<br/>
    /// <b>Memory:</b> 4 x k bytes for the result.
    /// </para>
    /// </remarks>
    /// <param name="selected">The selected node IDs.</param>
    /// <returns>The mapping from new to old node IDs.</returns>
    public static NodeMapping ForSubgraph(BitArray selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        int k = 0;
        for (int bit = 0; bit < selected.Length; bit++)
        {
            if (selected[bit])
            {
                k++;
            }
        }
        var map = new int[k];
        int idx = 0;
        for (int bit = 0; bit < selected.Length; bit++)
        {
            if (selected[bit])
            {
                map[idx++] = bit;
            }
        }
        return new NodeMapping(map, k);
    }

    /// <summary>
    /// Returns a mapping from the nodes of a subgraph to the selected nodes, given as a
    /// <see cref="SortedIntView"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The selected nodes get the new node IDs <c>0..selected.Count-1</c>, in ascending order of
    /// their node IDs. A <see cref="SortedIntView"/>, as
    /// <see cref="Graphs.ReachableFrom(Graph, int)"/> and <see cref="NodePartition.NodesOf"/> return
    /// it, is taken as it is; any other list goes to <see cref="ForSubgraph(ReadOnlySpan{int})"/>,
    /// which sorts it in a copy. As with <see cref="ForSubgraph(BitArray)"/>, the node IDs are checked
    /// against a graph only when the mapping is used.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(selected.Count).<br/>
    /// <b>Memory:</b> 4 x selected.Count bytes for the result.
    /// </para>
    /// </remarks>
    /// <param name="selected">The selected node IDs.</param>
    /// <returns>The mapping from new to old node IDs.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A node ID is negative.</exception>
    /// <exception cref="ArgumentException">A node ID occurs more than once.</exception>
    public static NodeMapping ForSubgraph(SortedIntView selected)
    {
        int[] map = selected.ToArray();
        CheckSelection(map);
        return new NodeMapping(map, map.Length);
    }

    /// <summary>
    /// Returns a mapping from the nodes of a subgraph to the selected nodes, given as node IDs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The selected nodes get the new node IDs <c>0..selected.Length-1</c>, in ascending order of
    /// their node IDs, whatever the order of the arguments; the span is sorted in a copy. Takes loose
    /// arguments, an array or a span. As with <see cref="ForSubgraph(BitArray)"/>, the node IDs are
    /// checked against a graph only when the mapping is used.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(k log k), where k is <c>selected.Length</c>.<br/>
    /// <b>Memory:</b> 4 x selected.Length bytes for the result.
    /// </para>
    /// </remarks>
    /// <param name="selected">The selected node IDs.</param>
    /// <returns>The mapping from new to old node IDs.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A node ID is negative.</exception>
    /// <exception cref="ArgumentException">A node ID occurs more than once.</exception>
    public static NodeMapping ForSubgraph(params ReadOnlySpan<int> selected)
    {
        int[] map = selected.ToArray();
        Array.Sort(map);
        CheckSelection(map);
        return new NodeMapping(map, map.Length);
    }

    /// <summary>
    /// Returns a mapping that numbers the nodes in the given order, from new to old node IDs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>order[newNode]</c> is the node that gets the new node ID <c>newNode</c>.
    /// <paramref name="order"/> holds every node ID in <c>0..order.Length-1</c> once, so the mapping
    /// maps every node. For example, <c>NodeMapping.ForPermutation(TopOrder.Of(dag).AsSpan())</c>
    /// numbers the nodes of a DAG (directed acyclic graph) in topological order, so that every edge
    /// of the remapped graph goes from a lower to a higher node ID.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(order.Length).<br/>
    /// <b>Memory:</b> 4 x order.Length bytes for the result; while it runs, 1 bit per node.
    /// </para>
    /// </remarks>
    /// <param name="order">The node IDs in their new order.</param>
    /// <returns>The mapping from new to old node IDs.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A node ID is not in <c>0..order.Length-1</c>.</exception>
    /// <exception cref="ArgumentException">A node ID occurs more than once.</exception>
    public static NodeMapping ForPermutation(ReadOnlySpan<int> order)
    {
        int n = order.Length;
        var map = new int[n];
        var seen = new ulong[(n >> 6) + 1];
        for (int i = 0; i < n; i++)
        {
            int old = order[i];
            if ((uint)old >= (uint)n)
            {
                throw new ArgumentOutOfRangeException(nameof(order), old,
                    string.Create(CultureInfo.InvariantCulture,
                        $"order value out of range 0..{n - 1} at position {i}: {old}"));
            }
            if ((seen[old >> 6] & (1UL << old)) != 0)
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                    $"order contains {old} more than once"), nameof(order));
            }
            seen[old >> 6] |= 1UL << old;
            map[i] = old;
        }
        return new NodeMapping(map, n);
    }

    /// <summary>Returns the inverse mapping, which maps <c>Get(node)</c> back to <c>node</c>.</summary>
    /// <remarks>
    /// <para>
    /// The inverse maps the <paramref name="targetCount"/> nodes of the graph that this mapping maps
    /// to; a node that no node maps to is not mapped, and the inverse is then partial. For a mapping
    /// from new to old node IDs, as the factories return it, <paramref name="targetCount"/> is the
    /// node count of the old graph, and the inverse gives the new node ID of an old node:
    /// <c>newToOld.Inverted(oldGraph.NodeCount).Get(oldNode)</c>.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(IndexCount + targetCount).<br/>
    /// <b>Memory:</b> 4 x targetCount bytes for the result.
    /// </para>
    /// </remarks>
    /// <param name="targetCount">The node count of the graph that this mapping maps to.</param>
    /// <returns>The inverse mapping, with an index count of <paramref name="targetCount"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="targetCount"/> is negative or larger than <c>Array.MaxLength</c>, or two nodes
    /// map to the same node.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A node maps to <paramref name="targetCount"/> or more.</exception>
    public NodeMapping Inverted(int targetCount) => new(InvertArray(Mapping, targetCount), MappedCount);

    /// <summary>
    /// Returns the mapping that applies <paramref name="inner"/> first and then this mapping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If <paramref name="inner"/> maps the nodes of a graph A to those of B, and this mapping maps B
    /// to C, the result maps A to C. A node is not mapped in the result if <paramref name="inner"/>
    /// does not map it, or if this mapping does not map the node that <paramref name="inner"/> maps
    /// it to. For mappings from new to old node IDs, <c>firstToOriginal.Compose(secondToFirst)</c>
    /// maps the nodes of a graph remapped twice back to those of the original one.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(inner.IndexCount).<br/>
    /// <b>Memory:</b> 4 x inner.IndexCount bytes for the result.
    /// </para>
    /// </remarks>
    /// <param name="inner">The mapping to apply first.</param>
    /// <returns>The composed mapping.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="inner"/> maps a node to a node ID outside <c>0..IndexCount-1</c>.
    /// </exception>
    public NodeMapping Compose(NodeMapping inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        int[] composed = ComposeArrays(Mapping, inner.Mapping);
        return new NodeMapping(composed, CountValid(composed));
    }

    /// <summary>
    /// Returns <paramref name="values"/> renumbered by this mapping: the value at position <c>i</c>
    /// of the result is <c>values[Get(i)]</c>, or <c>default</c> (<c>0</c> for a number) if <c>i</c>
    /// is not mapped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a mapping from new to old node IDs, as the factories return it, this carries data on the
    /// nodes of the old graph over to the new graph. One generic method serves <c>int</c>,
    /// <c>long</c>, <c>double</c> and any other unmanaged value type.
    /// <see cref="Gather{T}(ReadOnlySpan{T}, Span{T})"/> does the same for objects, such as names.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(IndexCount).<br/>
    /// <b>Memory:</b> one element per index for the result, 4 x IndexCount bytes for <c>int</c>.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The element type: an unmanaged value type such as <c>int</c>.</typeparam>
    /// <param name="values">The value of each node of the graph that this mapping maps to.</param>
    /// <returns>The renumbered values, of length <see cref="IndexMapping.IndexCount"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A node maps to a node ID outside <paramref name="values"/>.
    /// </exception>
    public T[] Gather<T>(ReadOnlySpan<T> values)
        where T : unmanaged
        => GatherArray(Mapping, values);

    /// <summary>
    /// Writes <paramref name="values"/> renumbered by this mapping into
    /// <paramref name="newValues"/>, for objects such as names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>newValues[i]</c> becomes <c>values[Get(i)]</c>, and stays as it is if <c>i</c> is not
    /// mapped. References are copied, not the objects. If an exception is thrown,
    /// <paramref name="newValues"/> may be partly written.
    /// </para>
    /// <para><b>Time:</b> O(IndexCount).</para>
    /// </remarks>
    /// <typeparam name="T">The element type, nullable or not.</typeparam>
    /// <param name="values">The value of each node of the graph that this mapping maps to.</param>
    /// <param name="newValues">
    /// Receives the renumbered values; length <see cref="IndexMapping.IndexCount"/>, and not
    /// overlapping <paramref name="values"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <c>newValues.Length != IndexCount</c>, or the two overlap.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A node maps to a node ID outside <paramref name="values"/>.
    /// </exception>
    public void Gather<T>(ReadOnlySpan<T> values, Span<T> newValues) => GatherArray(Mapping, values, newValues);

    /// <summary>
    /// Returns a mapping that numbers the nodes of <paramref name="graph"/> in depth-first preorder,
    /// from new to old node IDs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The search starts at node 0, follows the out-nodes of each node in ascending order, and starts
    /// again at the smallest node not yet visited until it has visited every node; each node gets the
    /// next new node ID when the search reaches it. Nodes that the search visits one after another
    /// thus get adjacent node IDs. Traversals of the remapped graph then read its arrays more in
    /// order, which can make them faster on large graphs.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 4 x NodeCount bytes for the result; while it runs, 1 bit per node and up to
    /// 8 x NodeCount bytes for the stack, which grows with the deepest path.
    /// </para>
    /// </remarks>
    /// <param name="graph">The graph whose nodes are numbered.</param>
    /// <returns>The mapping from new to old node IDs.</returns>
    /// <exception cref="ArgumentException">A depth-first path is longer than 2^30 - 29 nodes.</exception>
    public static NodeMapping Reindex(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;
        int[] outStart = graph.OutStart;
        int[] adj = graph.OutAdj;
        // Visited bits, one per node, tested and set with a single word load and store.
        var visited = new ulong[(n >> 6) + 1];
        var order = new int[n];
        int pos = 0;
        int[] frames = DfsFrames.Initial(n);

        for (int root = NextClearBit(visited, 0); root < n; root = NextClearBit(visited, root + 1))
        {
            visited[root >> 6] |= 1UL << root;
            order[pos++] = root;
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
                    int w = v >> 6;
                    ulong bits = visited[w];
                    ulong mask = 1UL << v;
                    if ((bits & mask) == 0)
                    {
                        visited[w] = bits | mask;
                        order[pos++] = v;
                        frames[2 * top + 1] = i;
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
                if (!descended)
                {
                    top--;
                }
            }
        }
        return new NodeMapping(order, n);
    }

    // The index of the first clear bit at or after from: words of set bits are skipped 64 at a time,
    // and a result beyond the node count is left for the caller's bound check to reject.
    private static int NextClearBit(ulong[] bits, int from)
    {
        int word = from >> 6;
        ulong candidates = ~bits[word] & (ulong.MaxValue << from);
        while (candidates == 0)
        {
            if (++word == bits.Length)
            {
                return bits.Length << 6;
            }
            candidates = ~bits[word];
        }
        return (word << 6) + BitOperations.TrailingZeroCount(candidates);
    }

    // Rejects a repeated ID in a sorted selection, and then a negative first ID, the only place one
    // can be.
    private static void CheckSelection(int[] sorted)
    {
        for (int i = 1; i < sorted.Length; i++)
        {
            if (sorted[i] == sorted[i - 1])
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                    $"selected contains node {sorted[i]} more than once"), "selected");
            }
        }
        if (sorted.Length > 0 && sorted[0] < 0)
        {
            throw new ArgumentOutOfRangeException("selected", sorted[0],
                string.Create(CultureInfo.InvariantCulture, $"selected node ID must be non-negative: {sorted[0]}"));
        }
    }
}
