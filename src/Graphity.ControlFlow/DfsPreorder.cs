using System.Globalization;

namespace Graphity.ControlFlow;

/// <summary>
/// Depth-first preorder of the nodes reachable from a list of entries - the first step of
/// <see cref="Loops"/>. The core keeps its own copy for <see cref="Dominators"/>: a module has no
/// access to the core's internal helpers, so this one reads the graph through <see cref="Csr"/>.
/// </summary>
/// <remarks>
/// Preorder numbers start at 1. Number 0 is a virtual root above all entries, so that several
/// entries need no special case downstream: every entry the search starts from is a child of the
/// virtual root, and <see cref="IsEntry"/> marks every entry, also one that the search reached from
/// an earlier entry. Successors are visited in ascending node order and entries in list order.
/// Iterative; the frame stack lives on the heap.
/// </remarks>
internal sealed class DfsPreorder
{
    /// <summary>
    /// Largest supported node count, 2^30 - 30 (Java: 2^30 - 6). The control-flow analyses hold two
    /// ints per preorder number, the virtual root included, so <c>2 x (MaxNodeCount + 1)</c> has to
    /// stay within the largest array, <see cref="ControlFlow.MaxArrayLength"/>. Follows
    /// <c>DfsPreorder.MaxNodeCount</c> in the core, which derives it from the one array limit there.
    /// </summary>
    internal const int MaxNodeCount = (ControlFlow.MaxArrayLength / 2) - 1;

    /// <summary>Number of nodes reachable from the entries.</summary>
    internal readonly int ReachedCount;

    /// <summary>Node with each preorder number, <c>Order[0] == -1</c> for the virtual root.</summary>
    internal readonly int[] Order;

    /// <summary>Preorder number of each node, 0 for a node not reachable from any entry.</summary>
    internal readonly int[] PreorderNumber;

    /// <summary>Preorder number of each node's parent in the search tree, 0 below the virtual root.</summary>
    internal readonly int[] Parent;

    /// <summary>Bit set over preorder numbers: set for every entry.</summary>
    private readonly ulong[] _entryBits;

    private DfsPreorder(int reachedCount, int[] order, int[] preorderNumber, int[] parent, ulong[] entryBits)
    {
        ReachedCount = reachedCount;
        Order = order;
        PreorderNumber = preorderNumber;
        Parent = parent;
        _entryBits = entryBits;
    }

    // The message names the reason of the limit, which lies in Dominators, not in the caller.
    internal static void CheckNodeCount(int nodeCount, string analysis)
    {
        if (nodeCount > MaxNodeCount)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"{analysis} takes the node limit of Dominators, which keeps two ints per node in one"
                + $" array, counting its virtual root: nodeCount must be <= {MaxNodeCount}, but was"
                + $" {nodeCount}"));
        }
    }

    /// <summary>Runs the search.</summary>
    /// <param name="graph">The graph to search.</param>
    /// <param name="entries">The entries, in the order the search starts from them.</param>
    /// <param name="analysis">Name of the calling analysis, for the exception message.</param>
    /// <exception cref="ArgumentOutOfRangeException">An entry is out of range.</exception>
    /// <exception cref="ArgumentException">The graph has more than <see cref="MaxNodeCount"/> nodes.</exception>
    internal static DfsPreorder Of(Graph graph, ReadOnlySpan<int> entries, string analysis)
    {
        int n = graph.NodeCount;
        CheckNodeCount(n, analysis);
        foreach (int entry in entries)
        {
            Checks.Index(entry, n, nameof(entries));
        }
        ReadOnlySpan<int> outStart = Csr.OutAdjacencyStart(graph);
        ReadOnlySpan<int> adj = Csr.OutAdjacency(graph);

        var preorderNumber = new int[n];
        var order = new int[n + 1];
        var parent = new int[n + 1];
        order[0] = -1;
        parent[0] = -1;
        // A frame is (node, resume cursor), interleaved, so both reads of a frame hit one cache line.
        var frames = new int[2 * Math.Max(16, Math.Min(n, 1024))];
        int count = 0;

        foreach (int entry in entries)
        {
            if (preorderNumber[entry] != 0)
            {
                continue;
            }
            preorderNumber[entry] = ++count;
            order[count] = entry;
            parent[count] = 0;
            frames[0] = entry;
            frames[1] = outStart[entry];
            int top = 0;

            while (top >= 0)
            {
                int u = frames[2 * top];
                int i = frames[(2 * top) + 1];
                int end = outStart[u + 1];
                bool descended = false;
                while (i < end)
                {
                    int v = adj[i++];
                    if (preorderNumber[v] == 0)
                    {
                        frames[(2 * top) + 1] = i;
                        preorderNumber[v] = ++count;
                        order[count] = v;
                        parent[count] = preorderNumber[u];
                        if (2 * (++top + 1) > frames.Length)
                        {
                            // At most one frame per node, so 2 x nodeCount suffices and cannot overflow.
                            Array.Resize(ref frames, (int)Math.Min(2L * frames.Length, 2L * n));
                        }
                        frames[2 * top] = v;
                        frames[(2 * top) + 1] = outStart[v];
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

        var entryBits = new ulong[(count >> 6) + 1];
        foreach (int entry in entries)
        {
            int number = preorderNumber[entry];
            entryBits[number >> 6] |= 1UL << number;
        }
        return new DfsPreorder(count, order, preorderNumber, parent, entryBits);
    }

    /// <summary>Whether the node with this preorder number is one of the entries.</summary>
    internal bool IsEntry(int number) => (_entryBits[number >> 6] & (1UL << number)) != 0;
}
