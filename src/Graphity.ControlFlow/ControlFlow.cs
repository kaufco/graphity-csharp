using System.Globalization;

namespace Graphity.ControlFlow;

/// <summary>
/// Relations of control flow that follow from <see cref="Dominators"/>, returned as graphs: the
/// dominance frontiers and, from them, control dependence.
/// </summary>
/// <remarks>
/// <para>
/// A block is control dependent on another if that block decides whether it runs. Control dependence
/// is the transposed graph of the dominance frontiers of the post-dominators. The post-dominators are
/// the dominators of the transposed graph with the exits as entries. For the placement of phi-nodes,
/// <see cref="IteratedDominanceFrontier"/> answers without building the dominance frontiers.
/// </para>
/// <para>
/// The class shares its name with the namespace <c>Graphity.ControlFlow</c>, as the Java class shares
/// it with its package. Code in that namespace writes <c>ControlFlow.DominanceFrontiers</c>, and so
/// does code outside the <c>Graphity</c> namespace that imports it with
/// <c>using Graphity.ControlFlow;</c>. Code in another namespace below <c>Graphity</c> finds the
/// namespace first and needs <c>Graphity.ControlFlow.ControlFlow</c> or a <c>using</c> alias.
/// </para>
/// </remarks>
/// <example>
/// Here, for a method <c>load</c> with a for loop and an if in it, whose control flow <c>cfg</c> ends
/// in the block <c>exit</c>:
/// <!-- example: AnalyzingControlFlow.cs#controlDependence -->
/// <code language="csharp">
/// Dominators postDominators =
///     Dominators.Of(BiAdjacentGraph.Of(cfg).Transposed(), exit);
/// Graph controlDependence =
///     BiAdjacentGraph.Of(ControlFlow.DominanceFrontiers(postDominators))
///         .Transposed();
/// // header -> [header, body, next]: the loop condition decides whether they run
/// // body -> [then]: the if decides whether then runs
/// </code>
/// </example>
public static class ControlFlow
{
    // The largest array .NET allocates, Array.MaxLength (0x7FFFFFC7), the one array limit of the
    // library; the package derives its other limits from it. Follows ArrayLimits.MaxArrayLength in the
    // core, which a package cannot see.
    internal const int MaxArrayLength = 0x7FFFFFC7;

    /// <summary>
    /// Returns the dominance frontiers as a graph: an edge <c>n -> m</c> for every node <c>m</c> in the
    /// dominance frontier of <c>n</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dominance frontier of <c>n</c> holds the nodes where its dominance ends: <c>m</c> is in it if
    /// <c>n</c> dominates an in-node of <c>m</c> but does not strictly dominate <c>m</c>. A node can be in
    /// its own dominance frontier, as the header of a natural loop is, so the result can have self-loops.
    /// Nodes that the entries do not reach have empty dominance frontiers and are in none.
    /// </para>
    /// <para>
    /// Uses the algorithm of Cooper, Harvey and Kennedy. The result is computed anew on every call, not
    /// cached: keep it to use it more than once. It can grow quadratically with the node count, for
    /// example on deeply nested do-while loops.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount + frontierCount), where frontierCount is the number of edges
    /// of the result.<br/>
    /// <b>Memory:</b> 4 x (NodeCount + frontierCount) bytes for the result; while it runs, as much again
    /// and 8 x NodeCount bytes more.
    /// </para>
    /// </remarks>
    /// <param name="dominators">The dominators of the control-flow graph.</param>
    /// <returns>The dominance frontiers, with the node IDs of the graph of <paramref name="dominators"/>.</returns>
    /// <exception cref="ArgumentException">The result would have more edges than a graph can have.</exception>
    public static Graph DominanceFrontiers(Dominators dominators)
    {
        ArgumentNullException.ThrowIfNull(dominators);
        BiAdjacentGraph graph = dominators.Graph;
        int n = graph.NodeCount;
        ReadOnlySpan<int> inStart = Csr.InAdjacencyStart(graph);
        ReadOnlySpan<int> inAdj = Csr.InAdjacency(graph);
        var idom = new int[n];
        for (int node = 0; node < n; node++)
        {
            idom[node] = dominators.ImmediateDominatorOf(node);
        }

        // Walk up from every predecessor of m to just below idom(m), adding m on the way (Cooper,
        // Harvey and Kennedy). A node already holding m means the rest of the walk is done, so every
        // step adds a new pair. Run twice - count, then fill - so that each frontier comes out sorted
        // without an intermediate pair buffer.
        var lastAdded = new int[n];
        var start = new int[n + 1];
        Array.Fill(lastAdded, -1);
        long total = 0;
        for (int m = 0; m < n; m++)
        {
            if (!dominators.IsReachable(m))
            {
                continue;
            }
            int stop = idom[m];
            for (int i = inStart[m], end = inStart[m + 1]; i < end; i++)
            {
                int runner = inAdj[i];
                if (!dominators.IsReachable(runner))
                {
                    continue;
                }
                while (runner != stop && lastAdded[runner] != m)
                {
                    lastAdded[runner] = m;
                    start[runner]++;
                    total++;
                    runner = idom[runner];
                }
            }
        }
        if (total > MaxArrayLength)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"the dominance frontier relation has more than "
                + $"{MaxArrayLength} pairs, the largest array length"));
        }
        int next = 0;
        for (int node = 0; node < n; node++)
        {
            int count = start[node];
            start[node] = next;
            next += count;
        }
        start[n] = next;
        var frontier = new int[next];
        Array.Fill(lastAdded, -1);
        // start[runner] doubles as write pointer and is shifted back afterwards.
        for (int m = 0; m < n; m++)
        {
            if (!dominators.IsReachable(m))
            {
                continue;
            }
            int stop = idom[m];
            for (int i = inStart[m], end = inStart[m + 1]; i < end; i++)
            {
                int runner = inAdj[i];
                if (!dominators.IsReachable(runner))
                {
                    continue;
                }
                while (runner != stop && lastAdded[runner] != m)
                {
                    lastAdded[runner] = m;
                    frontier[start[runner]++] = m;
                    runner = idom[runner];
                }
            }
        }
        for (int node = n; node > 0; node--)
        {
            start[node] = start[node - 1];
        }
        start[0] = 0;
        return Graph.Of(start, frontier);
    }
}
