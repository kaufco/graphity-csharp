using System.Diagnostics;

namespace Graphity;

/// <summary>
/// The bridges, articulation points and biconnected components of a graph read as undirected, as
/// <see cref="Components.BiconnectivityOf"/> returns them.
/// </summary>
/// <remarks>
/// <para>
/// A bridge is an edge whose removal increases the number of connected components, and an
/// articulation point is a node whose removal does the same. A biconnected component, also called a
/// block, is a maximal group of edges in which any two edges lie on a common cycle. The blocks
/// partition the edges, not the nodes: an articulation point belongs to every block it touches.
/// </para>
/// <para>
/// The graph is read as undirected, as described at <see cref="Components"/>: the edges <c>a -> b</c>
/// and <c>b -> a</c> are one undirected edge. So a graph that stores its edges in both directions
/// still has bridges, and the two directions of an edge lie in the same block. Self-loops are in no
/// block and never a bridge.
/// </para>
/// <para>Instances are immutable and safe to share between threads.</para>
/// <para><b>Memory:</b> up to 8 x EdgeCount + 20 x NodeCount bytes.</para>
/// </remarks>
/// <example>
/// Here, the control flow of the method <c>load</c>, whose loop is connected to the rest only through
/// <c>header</c>:
/// <!-- example: Connectivity.cs#biconnectivity -->
/// <code language="csharp">
/// Biconnectivity biconnectivity =
///     Components.BiconnectivityOf(BiAdjacentGraph.Of(cfg));
/// Edges bridges = biconnectivity.Bridges;
/// // bridges: entry -> header, header -> exit
/// SortedIntView articulationPoints = biconnectivity.ArticulationPoints;
/// // articulationPoints: [1] - header
/// </code>
/// </example>
public sealed class Biconnectivity
{
    private Biconnectivity(Edges bridges, SortedIntView articulationPoints, EdgePartition blocks)
    {
        Bridges = bridges;
        ArticulationPoints = articulationPoints;
        Blocks = blocks;
    }

    /// <summary>Computes bridges, articulation points and blocks of the graph, read as undirected.</summary>
    internal static Biconnectivity Of(BiAdjacentGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var finder = new BlockFinder(graph);
        finder.Run();
        return new Biconnectivity(finder.Bridges(), FlaggedNodes(finder.IsArticulation), finder.Blocks());
    }

    /// <summary>Gets the bridges, in edge index order.</summary>
    /// <remarks>
    /// <para>
    /// Of a bridge stored in both directions, only the edge from the lower to the higher node ID is
    /// listed; <see cref="Blocks"/> puts the other direction in the same block.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    public Edges Bridges { get; }

    /// <summary>Gets the articulation points, in ascending order.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public SortedIntView ArticulationPoints { get; }

    /// <summary>Gets the blocks, as a partition of the edges.</summary>
    /// <remarks>
    /// <para>
    /// Each block is a group of the partition: <c>Blocks.Get(e)</c> returns the block of edge <c>e</c>,
    /// and <c>Blocks.EdgesOf(block)</c> the edges of a block. Self-loops are in no block and map to -1,
    /// so <see cref="IndexMapping.IsPartial"/> is <see langword="true"/> exactly if the graph has
    /// self-loops. Which block gets which group ID is not specified, but the same graph always yields the
    /// same group IDs.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    public EdgePartition Blocks { get; }

    /// <summary>The nodes whose flag is set, ascending; <see cref="StrongConnectivity"/> uses it as well.</summary>
    internal static SortedIntView FlaggedNodes(bool[] isFlagged)
    {
        int count = 0;
        foreach (bool flag in isFlagged)
        {
            if (flag)
            {
                count++;
            }
        }
        if (count == 0)
        {
            return SortedIntView.Empty;
        }
        var nodes = new int[count];
        int pos = 0;
        for (int node = 0; node < isFlagged.Length; node++)
        {
            if (isFlagged[node])
            {
                nodes[pos++] = node;
            }
        }
        return new SortedIntView(nodes);
    }

    /// <summary>
    /// Hopcroft-Tarjan biconnectivity on an explicit stack.
    /// </summary>
    /// <remarks>
    /// Neighbors are read as undirected by merging a node's out- and in-adjacency, both sorted
    /// ascending, emitting each neighbor once. The merge also yields a block's edge indices: for one
    /// undirected edge it sees the out-adjacency slot - already an edge index - and the in-adjacency
    /// slot in the same step, and the latter translates to an edge index through
    /// <see cref="EdgeMapping.ForTransposed"/>, the body of <see cref="Graphs.TransposedEdgeMapping"/>.
    /// Collapsing the reciprocal pair makes the parent
    /// appear exactly once among a node's neighbors, so skipping the tree edge back up needs no
    /// per-frame flag. Per-node state lives in arrays because a node is on the depth-first stack at
    /// most once; the edge stack grows on demand.
    /// </remarks>
    private sealed class BlockFinder
    {
        private const int Unvisited = 0;

        private readonly BiAdjacentGraph _graph;
        private readonly int[] _outAdjacency;
        private readonly int[] _outAdjacencyStart;
        private readonly int[] _inAdjacency;
        private readonly int[] _inAdjacencyStart;

        /// <summary>In-adjacency slot → edge index of the same edge in the graph.</summary>
        private readonly int[] _inSlotToEdge;

        /// <summary>Discovery time, <see cref="Unvisited"/> until the node is reached; counting starts at 1.</summary>
        private readonly int[] _disc;
        private readonly int[] _low;
        private readonly int[] _dfsParent;

        /// <summary>Edge-stack height before the tree edge into this node was pushed.</summary>
        private readonly int[] _blockMark;

        private readonly int[] _outCursor;
        private readonly int[] _inCursor;

        private readonly int[] _dfsStack;
        private int[] _edgeStack;
        private int _edgeTop;

        /// <summary>Bridges as parallel lists of source, target and edge index, each a stored edge.</summary>
        private readonly IntArrayList _bridgeSources = new();
        private readonly IntArrayList _bridgeTargets = new();
        private readonly IntArrayList _bridgeEdgeIndices = new();

        /// <summary>Edge index → block ID, -1 for a self-loop.</summary>
        private readonly int[] _blockOfEdge;
        private int _blockCount;

        private int _timer;

        /// <summary>Neighbor found by <see cref="Advance"/>, with the edge indices of its two directions.</summary>
        private int _pendingNode;
        private int _pendingOutEdge;
        private int _pendingInSlot;

        internal BlockFinder(BiAdjacentGraph graph)
        {
            _graph = graph;
            int n = graph.NodeCount;
            int m = graph.EdgeCount;
            _outAdjacency = graph.OutAdj;
            _outAdjacencyStart = graph.OutStart;
            _inAdjacency = graph.InAdj;
            _inAdjacencyStart = graph.InStart;
            _inSlotToEdge = EdgeMapping.ForTransposed(graph).Mapping;
            _disc = new int[n];
            _low = new int[n];
            _dfsParent = new int[n];
            _blockMark = new int[n];
            _outCursor = new int[n];
            _inCursor = new int[n];
            _dfsStack = new int[n];
            // Sized like DfsFrames.Initial: one slot per edge, but at least 16 and at most 1024.
            _edgeStack = new int[Math.Max(16, Math.Min(m, 1024))];
            IsArticulation = new bool[n];
            _blockOfEdge = new int[m];
            Array.Fill(_blockOfEdge, -1);
        }

        internal bool[] IsArticulation { get; }

        internal void Run()
        {
            int n = _graph.NodeCount;
            int[] disc = _disc;
            int[] low = _low;
            int[] dfsParent = _dfsParent;
            int[] dfsStack = _dfsStack;
            for (int root = 0; root < n; root++)
            {
                if (disc[root] != Unvisited)
                {
                    continue;
                }
                Discover(root, -1);
                dfsStack[0] = root;
                int top = 0;
                int rootChildren = 0;

                while (top >= 0)
                {
                    int u = dfsStack[top];
                    bool descended = false;
                    while (Advance(u))
                    {
                        int v = _pendingNode;
                        // The one occurrence of the parent is the tree edge back up; it is already pushed.
                        if (v == dfsParent[u])
                        {
                            continue;
                        }
                        if (disc[v] == Unvisited)
                        {
                            int mark = _edgeTop;
                            PushPendingEdges();
                            Discover(v, u);
                            _blockMark[v] = mark;
                            if (u == root)
                            {
                                rootChildren++;
                            }
                            dfsStack[++top] = v;
                            descended = true;
                            break;
                        }
                        if (disc[v] < disc[u])
                        {
                            // Back edge to an ancestor. Undirected depth-first search has no cross
                            // edges, so the other case - a finished descendant - was handled from its
                            // own side.
                            PushPendingEdges();
                            if (disc[v] < low[u])
                            {
                                low[u] = disc[v];
                            }
                        }
                    }
                    if (descended)
                    {
                        continue;
                    }

                    top--;
                    int parent = dfsParent[u];
                    if (parent >= 0)
                    {
                        if (low[u] < low[parent])
                        {
                            low[parent] = low[u];
                        }
                        if (low[u] >= disc[parent])
                        {
                            if (low[u] > disc[parent])
                            {
                                AddBridge(parent, u, _blockMark[u]);
                            }
                            CloseBlock(_blockMark[u]);
                            // The root is an articulation point only if it has two or more subtrees.
                            if (parent != root)
                            {
                                IsArticulation[parent] = true;
                            }
                        }
                    }
                }
                if (rootChildren >= 2)
                {
                    IsArticulation[root] = true;
                }
            }
        }

        /// <summary>Discovers <paramref name="node"/> as a child of <paramref name="parent"/> and initializes its merge cursors.</summary>
        private void Discover(int node, int parent)
        {
            _disc[node] = ++_timer;
            _low[node] = _disc[node];
            _dfsParent[node] = parent;
            _outCursor[node] = _outAdjacencyStart[node];
            _inCursor[node] = _inAdjacencyStart[node];
        }

        /// <summary>
        /// Advances <paramref name="node"/>'s merge cursors to its next undirected neighbor, skipping
        /// self-loops, and reports it in <see cref="_pendingNode"/>; false once the neighbors are
        /// exhausted.
        /// </summary>
        private bool Advance(int node)
        {
            int outPos = _outCursor[node];
            int outEnd = _outAdjacencyStart[node + 1];
            int inPos = _inCursor[node];
            int inEnd = _inAdjacencyStart[node + 1];

            while (outPos < outEnd || inPos < inEnd)
            {
                int outNeighbor = outPos < outEnd ? _outAdjacency[outPos] : int.MaxValue;
                int inNeighbor = inPos < inEnd ? _inAdjacency[inPos] : int.MaxValue;
                int neighbor;
                int outEdge = -1;
                int inSlot = -1;
                if (outNeighbor < inNeighbor)
                {
                    neighbor = outNeighbor;
                    outEdge = outPos++;
                }
                else if (inNeighbor < outNeighbor)
                {
                    neighbor = inNeighbor;
                    inSlot = inPos++;
                }
                else
                {
                    // Both directions of one undirected edge; both indices belong to the same block.
                    neighbor = outNeighbor;
                    outEdge = outPos++;
                    inSlot = inPos++;
                }
                if (neighbor == node)
                {
                    continue; // self-loop: in no block, never a bridge
                }
                _outCursor[node] = outPos;
                _inCursor[node] = inPos;
                _pendingNode = neighbor;
                _pendingOutEdge = outEdge;
                _pendingInSlot = inSlot;
                return true;
            }
            _outCursor[node] = outPos;
            _inCursor[node] = inPos;
            return false;
        }

        /// <summary>Pushes the edge indices of the pending undirected edge - one per stored direction.</summary>
        private void PushPendingEdges()
        {
            if (_pendingOutEdge >= 0)
            {
                PushEdge(_pendingOutEdge);
            }
            if (_pendingInSlot >= 0)
            {
                PushEdge(_inSlotToEdge[_pendingInSlot]);
            }
        }

        private void PushEdge(int edgeIndex)
        {
            if (_edgeTop == _edgeStack.Length)
            {
                // Every non-self-loop edge is pushed at most once, so the edge count is the hard cap.
                int grown = (int)Math.Min((long)_edgeTop << 1, _blockOfEdge.Length);
                Array.Resize(ref _edgeStack, grown);
            }
            _edgeStack[_edgeTop++] = edgeIndex;
        }

        /// <summary>
        /// Records the bridge <c>parent - child</c> before its block closes. Every block below the
        /// child is closed already, so the edges above <paramref name="mark"/> are exactly the one or
        /// two stored directions of the bridge, pushed <c>parent → child</c> first. A bridge stored
        /// both ways is reported from the lower node ID.
        /// </summary>
        private void AddBridge(int parent, int child, int mark)
        {
            Debug.Assert(_edgeTop - mark is 1 or 2);
            int edge = _edgeTop - mark == 2 && parent > child ? _edgeStack[mark + 1] : _edgeStack[mark];
            int target = _outAdjacency[edge];
            _bridgeSources.Add(target == child ? parent : child);
            _bridgeTargets.Add(target);
            _bridgeEdgeIndices.Add(edge);
        }

        /// <summary>Turns the edges above <paramref name="mark"/> into one block.</summary>
        private void CloseBlock(int mark)
        {
            int block = _blockCount++;
            while (_edgeTop > mark)
            {
                _blockOfEdge[_edgeStack[--_edgeTop]] = block;
            }
        }

        internal Edges Bridges() => Edges.SortedEdges(_graph, _bridgeSources.Elements, _bridgeTargets.Elements,
            _bridgeEdgeIndices.Elements, _bridgeSources.Count, false);

        internal EdgePartition Blocks()
        {
            var groups = ParentForest.GroupIndices(_blockOfEdge, _blockCount);
            return new EdgePartition(_blockOfEdge, groups.Members, groups.Start);
        }
    }
}
