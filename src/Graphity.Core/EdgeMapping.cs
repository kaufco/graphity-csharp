namespace Graphity;

/// <summary>
/// A mapping from the edge indices of one graph to those of another, to carry data on edges over
/// to a subgraph, a renumbered graph or the transposed graph.
/// </summary>
/// <remarks>
/// <para>
/// <c>Get(e)</c> is the edge index that edge <c>e</c> maps to, or -1 if edge <c>e</c> is not mapped.
/// The factories return mappings from a new graph to an old one. These map each new edge to the edge
/// it comes from. <see cref="Graphs.EdgeMapping"/> derives one from the <see cref="NodeMapping"/> of
/// a subgraph or a renumbering, and leaves a new edge without a counterpart in the old graph
/// unmapped. <see cref="Graphs.TransposedEdgeMapping"/> maps each edge of the transposed graph to
/// the edge it reverses.
/// </para>
/// <para>
/// <c>Gather</c> carries data on the old edges over to the new ones. <see cref="Inverted"/> returns
/// the mapping from old to new, and <see cref="Compose"/> chains two mappings.
/// </para>
/// <para>Instances are immutable and safe to share between threads.</para>
/// <para><b>Memory:</b> 4 x IndexCount bytes.</para>
/// </remarks>
/// <example>
/// Here, <c>siteCount</c> holds the number of call sites of each call by edge index of <c>calls</c>,
/// and the subgraph <c>loading</c> from the example at <see cref="NodeMapping"/> takes these counts
/// along:
/// <!-- example: Mappings.cs#edgeMapping -->
/// <code language="csharp">
/// int[] loadingSiteCount =
///     Graphs.EdgeMapping(loading, calls, newToOld).Gather(siteCount);
/// // 1 for load -> parse and for load -> read, 2 for parse -> parse
/// </code>
/// </example>
public sealed class EdgeMapping : IndexMapping
{
    // Takes the array as it is, not copied: the caller hands over an array that nobody else holds,
    // and validCount matches it.
    internal EdgeMapping(int[] mapping, int validCount)
        : base(mapping, validCount)
    {
    }

    // The body of Graphs.TransposedEdgeMapping, here so that the algorithms that need it do not
    // call the facade.
    internal static EdgeMapping ForTransposed(Graph graph)
    {
        int n = graph.NodeCount;
        int m = graph.EdgeCount;
        int[] targets = graph.OutAdj;
        int[] start = graph.OutStart;

        // Write pointer per target node: where its next in-edge slot goes. A bi-adjacent graph has
        // those offsets; otherwise count and prefix-sum them.
        int[] writePos;
        if (graph is BiAdjacentGraph biAdjacent)
        {
            writePos = biAdjacent.InStart[..n];
        }
        else
        {
            writePos = new int[n];
            foreach (int target in targets)
            {
                writePos[target]++;
            }
            int nextStart = 0;
            for (int node = 0; node < n; node++)
            {
                int inDegree = writePos[node];
                writePos[node] = nextStart;
                nextStart += inDegree;
            }
        }

        // The counting sort of BiAdjacentGraph.Of, with the edge index as payload.
        var mapping = new int[m];
        for (int node = 0; node < n; node++)
        {
            for (int edgeIndex = start[node], end = start[node + 1]; edgeIndex < end; edgeIndex++)
            {
                mapping[writePos[targets[edgeIndex]]++] = edgeIndex;
            }
        }
        return new EdgeMapping(mapping, m);
    }

    /// <summary>Returns the inverse mapping, which maps <c>Get(e)</c> back to <c>e</c>.</summary>
    /// <remarks>
    /// <para>
    /// The inverse maps the <paramref name="targetCount"/> edges of the graph that this mapping maps
    /// to; an edge that no edge maps to is not mapped, and the inverse is then partial. For a
    /// mapping from new to old edge indices, as the factories return it,
    /// <paramref name="targetCount"/> is the edge count of the old graph, and the inverse gives the
    /// new edge index of an old edge: <c>newToOld.Inverted(oldGraph.EdgeCount).Get(oldEdge)</c>.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(IndexCount + targetCount).<br/>
    /// <b>Memory:</b> 4 x targetCount bytes for the result.
    /// </para>
    /// </remarks>
    /// <param name="targetCount">The edge count of the graph that this mapping maps to.</param>
    /// <returns>The inverse mapping, with an index count of <paramref name="targetCount"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="targetCount"/> is negative or larger than <c>Array.MaxLength</c>, or two edges
    /// map to the same edge.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">An edge maps to <paramref name="targetCount"/> or more.</exception>
    public EdgeMapping Inverted(int targetCount) => new(InvertArray(Mapping, targetCount), MappedCount);

    /// <summary>
    /// Returns the mapping that applies <paramref name="inner"/> first and then this mapping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If <paramref name="inner"/> maps the edges of a graph A to those of B, and this mapping maps B
    /// to C, the result maps A to C. An edge is not mapped in the result if
    /// <paramref name="inner"/> does not map it, or if this mapping does not map the edge that
    /// <paramref name="inner"/> maps it to.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(inner.IndexCount).<br/>
    /// <b>Memory:</b> 4 x inner.IndexCount bytes for the result.
    /// </para>
    /// </remarks>
    /// <param name="inner">The mapping to apply first.</param>
    /// <returns>The composed mapping.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="inner"/> maps an edge to an edge index outside <c>0..IndexCount-1</c>.
    /// </exception>
    public EdgeMapping Compose(EdgeMapping inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        int[] composed = ComposeArrays(Mapping, inner.Mapping);
        return new EdgeMapping(composed, CountValid(composed));
    }

    /// <summary>
    /// Returns <paramref name="values"/> renumbered by this mapping: the value at position <c>i</c>
    /// of the result is <c>values[Get(i)]</c>, or <c>default</c> (<c>0</c> for a number) if edge
    /// <c>i</c> is not mapped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a mapping from new to old edge indices, as the factories return it, this carries data on
    /// the edges of the old graph over to the new graph. One generic method serves <c>int</c>,
    /// <c>long</c>, <c>double</c> and any other unmanaged value type.
    /// <see cref="Gather{T}(ReadOnlySpan{T}, Span{T})"/> does the same for objects.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(IndexCount).<br/>
    /// <b>Memory:</b> one element per index for the result, 4 x IndexCount bytes for <c>int</c>.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The element type: an unmanaged value type such as <c>int</c>.</typeparam>
    /// <param name="values">The value of each edge of the graph that this mapping maps to.</param>
    /// <returns>The renumbered values, of length <see cref="IndexMapping.IndexCount"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An edge maps to an edge index outside <paramref name="values"/>.
    /// </exception>
    public T[] Gather<T>(ReadOnlySpan<T> values)
        where T : unmanaged
        => GatherArray(Mapping, values);

    /// <summary>
    /// Writes <paramref name="values"/> renumbered by this mapping into <paramref name="newValues"/>,
    /// for objects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>newValues[i]</c> becomes <c>values[Get(i)]</c>, and stays as it is if edge <c>i</c> is not
    /// mapped. References are copied, not the objects. If an exception is thrown,
    /// <paramref name="newValues"/> may be partly written.
    /// </para>
    /// <para><b>Time:</b> O(IndexCount).</para>
    /// </remarks>
    /// <typeparam name="T">The element type, nullable or not.</typeparam>
    /// <param name="values">The value of each edge of the graph that this mapping maps to.</param>
    /// <param name="newValues">
    /// Receives the renumbered values; length <see cref="IndexMapping.IndexCount"/>, and not
    /// overlapping <paramref name="values"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <c>newValues.Length != IndexCount</c>, or the two overlap.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An edge maps to an edge index outside <paramref name="values"/>.
    /// </exception>
    public void Gather<T>(ReadOnlySpan<T> values, Span<T> newValues) => GatherArray(Mapping, values, newValues);
}
