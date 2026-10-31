using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Graphity;

/// <summary>
/// A topological order of a DAG (directed acyclic graph) with the level of each node. A root node has
/// level 0, and every other node has one more than the highest level of its in-nodes.
/// </summary>
/// <remarks>
/// <para>
/// The level of a node is the number of edges on a longest path to it from a root node, so no path
/// connects two nodes of the same level. <see cref="Order"/> lists the nodes level by level,
/// <see cref="LevelOf"/> and <see cref="Levels"/> return their levels, and <see cref="LevelCount"/>
/// the number of levels.
/// </para>
/// <para>
/// Uses <see href="https://en.wikipedia.org/wiki/Topological_sorting#Kahn%27s_algorithm">Kahn's algorithm</see>:
/// <see cref="Of"/> starts with the root nodes in ascending order, places a
/// node once all its in-nodes are placed and takes the out-nodes of each node in ascending order,
/// so a graph always gets the same order. For the order alone, <see cref="TopOrder.Of(Graph)"/> needs less
/// memory.
/// </para>
/// <para>Instances are immutable and safe to share between threads.</para>
/// <para><b>Memory:</b> 8 x NodeCount bytes.</para>
/// </remarks>
public sealed class TopOrderAndLevels
{
    private TopOrderAndLevels(int[] order, int[] levels, int levelCount)
    {
        Order = ImmutableCollectionsMarshal.AsImmutableArray(order);
        Levels = ImmutableCollectionsMarshal.AsImmutableArray(levels);
        LevelCount = levelCount;
    }

    /// <summary>Returns the topological order and the levels of <paramref name="graph"/>.</summary>
    /// <remarks>
    /// <para>
    /// On a <see cref="BiAdjacentGraph"/>, it reads the in-degrees instead of counting them in a pass
    /// over the edges.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount + EdgeCount).<br/>
    /// <b>Memory:</b> 8 x NodeCount bytes for the result; while it runs, 8 x NodeCount bytes more.
    /// </para>
    /// </remarks>
    /// <param name="graph">The DAG to sort.</param>
    /// <returns>The order and the levels.</returns>
    /// <exception cref="ArgumentException"><paramref name="graph"/> has a cycle.</exception>
    public static TopOrderAndLevels Of(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        int n = graph.NodeCount;

        // 1) In-degrees; a BiAdjacentGraph has them in its offsets.
        var inDegree = new int[n];
        if (graph is BiAdjacentGraph biGraph)
        {
            int[] inStart = biGraph.InStart;
            for (int i = 0; i < n; i++)
            {
                inDegree[i] = inStart[i + 1] - inStart[i];
            }
        }
        else
        {
            foreach (int target in graph.OutAdj)
            {
                inDegree[target]++;
            }
        }

        // 2) Roots.
        var queue = new int[n];
        int queueHead = 0;
        int queueTail = 0;
        for (int i = 0; i < n; i++)
        {
            if (inDegree[i] == 0)
            {
                queue[queueTail++] = i;
            }
        }

        // 3) Kahn, one wave per level: a node joins the wave after that of its last predecessor.
        var order = new int[n];
        var levels = new int[n];
        int orderPos = 0;
        int level = 0;
        int[] adjacency = graph.OutAdj;
        int[] adjacencyStart = graph.OutStart;
        while (queueHead < queueTail)
        {
            int levelEnd = queueTail;
            while (queueHead < levelEnd)
            {
                int node = queue[queueHead++];
                order[orderPos++] = node;
                levels[node] = level;
                for (int i = adjacencyStart[node], end = adjacencyStart[node + 1]; i < end; i++)
                {
                    int target = adjacency[i];
                    if (--inDegree[target] == 0)
                    {
                        queue[queueTail++] = target;
                    }
                }
            }
            level++;
        }
        if (orderPos != n)
        {
            throw new ArgumentException("graph contains a cycle", nameof(graph));
        }
        return new TopOrderAndLevels(order, levels, level);
    }

    /// <summary>
    /// Gets the nodes in topological order, level by level: for every edge <c>u -> v</c>, <c>u</c>
    /// comes before <c>v</c>.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public ImmutableArray<int> Order { get; }

    /// <summary>Gets the level of every node, by node ID.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public ImmutableArray<int> Levels { get; }

    /// <summary>
    /// Gets the number of levels, or 0 for a graph without nodes. It equals the number of nodes on a
    /// longest path.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public int LevelCount { get; }

    /// <summary>Returns the level of <paramref name="node"/>. Equivalent to <c>Levels[node]</c>.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The level.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int LevelOf(int node)
    {
        Checks.Index(node, Levels.Length);
        return Levels[node];
    }
}
