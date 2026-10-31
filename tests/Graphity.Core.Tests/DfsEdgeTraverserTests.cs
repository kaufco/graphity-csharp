using EdgeAction = Graphity.DfsEdgeTraverser.EdgeAction;
using TargetState = Graphity.DfsEdgeTraverser.TargetState;

namespace Graphity.Core.Tests;

public sealed class DfsEdgeTraverserTests
{
    // ---- Default behaviour ----

    [Fact]
    public void DefaultOrderMatchesNodeCentricTraverserOnRandomGraphs()
    {
        for (long seed = 0; seed < 300; seed++)
        {
            var lg = RandomGraphs.AnyGraph(seed);
            var g = lg.Graph;
            var nodeCentric = new RecordingDfsTraverser(g);
            nodeCentric.Run();
            var edgeCentric = new Recorder(g);
            edgeCentric.Run();
            Assert.True(nodeCentric.PreOrder.SequenceEqual(edgeCentric.PreOrder), $"preorder ({lg.Describe()})");
            Assert.True(nodeCentric.PostOrder.SequenceEqual(edgeCentric.PostOrder), $"postorder ({lg.Describe()})");
            Assert.True(edgeCentric.Edges.Count == g.EdgeCount, $"every edge once ({lg.Describe()})");
        }
    }

    [Fact]
    public void TargetStatesAgreeWithEdgeTypesOnRandomGraphs()
    {
        for (long seed = 0; seed < 300; seed++)
        {
            var lg = RandomGraphs.AnyGraph(seed);
            var g = lg.Graph;
            var types = EdgeTypes.Of(g);
            var t = new Recorder(g);
            t.Run();
            for (int k = 0; k < t.Edges.Count; k++)
            {
                var expected = types.TypeOf(t.Edges[k]) switch
                {
                    EdgeType.Tree => TargetState.Unvisited,
                    EdgeType.Back => TargetState.OnStack,
                    _ => TargetState.Finished,
                };
                Assert.True(expected == t.States[k], $"edge {t.Edges[k]} ({lg.Describe()})");
            }
        }
    }

    [Fact]
    public void SelfLoopTargetIsOnStack()
    {
        var t = new Recorder(TestGraphs.Of(1, 0, 0));
        t.Run();
        Assert.Equal([TargetState.OnStack], t.States);
    }

    [Fact]
    public void EnterNodeDefaultsToDescending()
    {
        var t = new LeaveOnly(TestGraphs.Path(3));
        t.Run();
        Assert.Equal([2, 1, 0], t.Left);
    }

    // ---- Actions ----

    [Fact]
    public void SkippingAnUnvisitedTargetLeavesItForLaterEdgesAndRoots()
    {
        // 0 -> 1, 0 -> 2, 2 -> 1, 3 isolated. Skip 0 -> 1: node 1 is then reached via 2.
        var g = TestGraphs.Of(4, 0, 1, 0, 2, 2, 1);
        var t = new Recorder(g)
        {
            Decide = (_, source, target, _, _) => source == 0 && target == 1 ? EdgeAction.Skip : null,
        };
        t.Run(0);
        Assert.Equal([0, 2, 1], t.PreOrder);
        Assert.Equal([TargetState.Unvisited, TargetState.Unvisited, TargetState.Unvisited], t.States);

        var confined = new Recorder(g)
        {
            Decide = (_, _, target, _, _) => target == 1 ? EdgeAction.Skip : null,
        };
        confined.Run(0);
        Assert.False(confined.Visited(1));
        confined.Run();
        Assert.Equal([0, 2, 1, 3], confined.PreOrder);
    }

    [Fact]
    public void DescendIntoVisitedTargetActsLikeSkip()
    {
        var t = new Recorder(TestGraphs.Cycle(3)) { Decide = (_, _, _, _, _) => EdgeAction.Descend };
        t.Run();
        Assert.Equal([0, 1, 2], t.PreOrder);
        Assert.Equal([2, 1, 0], t.PostOrder);
    }

    [Fact]
    public void StopEndsTheRunWithoutLeavingThePath()
    {
        // Path 0 -> 1 -> 2 -> 3; stop on the edge 1 -> 2.
        var t = new Recorder(TestGraphs.Path(4)) { Decide = (_, source, _, _, _) => source == 1 ? EdgeAction.Stop : null };
        t.Run();
        Assert.Equal([0, 1], t.PreOrder);
        Assert.Empty(t.PostOrder);
        Assert.True(t.Visited(0));
        Assert.False(t.OnStack(0));
        Assert.False(t.OnStack(1));
        Assert.False(t.Visited(2));
    }

    [Fact]
    public void LaterRunContinuesAfterStopAndSeesStoppedPathAsFinished()
    {
        // 0 -> 1, 1 -> 2, 2 -> 0; stop at the first edge, then continue from 2.
        bool stopped = false;
        var stopOnce = new Recorder(TestGraphs.Cycle(3))
        {
            Decide = (_, _, _, _, _) =>
            {
                if (!stopped)
                {
                    stopped = true;
                    return EdgeAction.Stop;
                }
                return null;
            },
        };
        stopOnce.Run(0);
        stopOnce.Run(2);
        Assert.Equal([0, 2], stopOnce.PreOrder);
        Assert.Equal([TargetState.Unvisited, TargetState.Finished], stopOnce.States);
        Assert.Equal([2], stopOnce.PostOrder);
    }

    [Fact]
    public void StopEndsTheWholeRunFromANodeList()
    {
        var t = new Recorder(TestGraphs.Of(4, 0, 1, 2, 3)) { Decide = (_, _, _, _, _) => EdgeAction.Stop };
        t.Run([0, 2]);
        Assert.Equal([0], t.PreOrder);
        t.Run();
        Assert.Equal([0, 1, 2], t.PreOrder);
    }

    // ---- Pruning ----

    [Fact]
    public void PrunedNodeReportsNoEdgesButIsLeft()
    {
        // 0 -> 1, 0 -> 2, 1 -> 2: prune 1, so 1 -> 2 is never reported.
        var t = new Recorder(TestGraphs.Of(3, 0, 1, 0, 2, 1, 2)) { PruneNode = 1 };
        t.Run();
        Assert.Equal([0, 1, 2], t.PreOrder);
        Assert.Equal([1, 2, 0], t.PostOrder);
        Assert.Equal(2, t.Edges.Count);
    }

    [Fact]
    public void PrunedRootReportsNoEdgesButIsLeft()
    {
        var t = new Recorder(TestGraphs.Path(3)) { PruneAll = true };
        t.Run(0);
        Assert.Equal([0], t.PreOrder);
        Assert.Equal([0], t.PostOrder);
        Assert.Empty(t.Edges);
        Assert.False(t.OnStack(0));
        Assert.False(t.Visited(1));
    }

    [Fact]
    public void AnUndefinedEdgeActionEndsTheRunWithAnException()
    {
        var t = new Recorder(TestGraphs.Path(3)) { Decide = (_, _, _, _, _) => (EdgeAction)7 };
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => t.Run());
        // The hook returned the value; no parameter of Run is at fault.
        Assert.Null(e.ParamName);
        Assert.Equal((EdgeAction)7, e.ActualValue);
        Assert.Single(t.Edges);
    }

    // ---- Run contract ----

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void RunFromNodeOutOfRangeThrows(int startNode)
    {
        var t = new Recorder(TestGraphs.Isolated(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => t.Run(startNode));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(1000)]
    public void RunFromNodeListOutOfRangeThrowsBeforeAnyHook(int startNode)
    {
        // 4 and 15 still fall into the packed state word of the valid nodes.
        var t = new Recorder(TestGraphs.Isolated(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => t.Run([0, startNode]));
        Assert.Empty(t.PreOrder);
        Assert.False(t.Visited(0));
    }

    [Fact]
    public void EveryEntryPointSkipsVisitedNodes()
    {
        var t = new Recorder(TestGraphs.Of(4, 0, 1));
        t.Run(0);
        t.Run(1);
        t.Run([0, 2, 2]);
        t.Run();
        Assert.Equal([0, 1, 2, 3], t.PreOrder);
    }

    [Fact]
    public void RunOnEmptyGraphIsNoOp()
    {
        var t = new Recorder(TestGraphs.Empty());
        t.Run();
        Assert.Empty(t.PreOrder);
    }

    [Fact]
    public void DeepPathDoesNotOverflowStack()
    {
        const int deep = 1_000_000;
        var counter = new CountingEdges(TestGraphs.Path(deep));
        counter.Run(0);
        Assert.Equal(deep - 1, counter.EdgeCount);
        Assert.Equal(deep, counter.Left);
    }

    // ---- Stop() ----

    [Fact]
    public void StopFromEveryHookEndsTheRun()
    {
        // Path 0 -> 1 -> 2 -> 3; stop in EnterNode(1), VisitEdge(1 -> 2) or LeaveNode(3).
        int[][] expectedEnter = [[0, 1], [0, 1], [0, 1, 2, 3]];
        int[][] expectedLeave = [[], [], [3]];
        int[][] expectedEdges = [[0], [0, 1], [0, 1, 2]];
        for (int hook = 0; hook < 3; hook++)
        {
            int stopIn = hook;
            var t = new Recorder(TestGraphs.Path(4))
            {
                OnEnter = (r, node) =>
                {
                    if (stopIn == 0 && node == 1)
                    {
                        r.StopNow();
                    }
                },
                OnLeave = (r, node) =>
                {
                    if (stopIn == 2 && node == 3)
                    {
                        r.StopNow();
                    }
                },
                Decide = (r, source, _, _, _) =>
                {
                    if (stopIn == 1 && source == 1)
                    {
                        r.StopNow();
                    }
                    return null;
                },
            };
            t.Run();
            Assert.Equal(expectedEdges[hook], t.Edges);
            Assert.Equal(expectedEnter[hook], t.PreOrder);
            Assert.Equal(expectedLeave[hook], t.PostOrder);
            for (int v = 0; v < 4; v++)
            {
                Assert.False(t.OnStack(v), $"hook {hook}, node {v} on stack");
            }
        }
    }

    [Fact]
    public void StopInEnterNodeOfTheRootReportsNothingElse()
    {
        var t = new Recorder(TestGraphs.Path(3)) { StopOnEnter = 0 };
        t.Run();
        Assert.Equal([0], t.PreOrder);
        Assert.Empty(t.PostOrder);
        Assert.Empty(t.Edges);
        Assert.False(t.OnStack(0));
    }

    [Fact]
    public void StopInLeaveNodeOfAPrunedRootEndsTheRun()
    {
        var t = new Recorder(TestGraphs.Isolated(3)) { PruneNode = 0, StopOnLeave = 0 };
        t.Run();
        Assert.Equal([0], t.PreOrder);
        Assert.Equal([0], t.PostOrder);
    }

    [Fact]
    public void StopInLeaveNodeOfAPrunedChildEndsTheRun()
    {
        // 0 -> 1, 0 -> 2: node 1 is pruned and stops the run when it is left.
        var t = new Recorder(TestGraphs.Of(3, 0, 1, 0, 2)) { PruneNode = 1, StopOnLeave = 1 };
        t.Run();
        Assert.Equal([0, 1], t.PreOrder);
        Assert.Equal([1], t.PostOrder);
        Assert.Equal([0], t.Edges);
        Assert.False(t.OnStack(0));
    }

    [Fact]
    public void EveryRunVariantClearsAnEarlierStop()
    {
        // 1 -> 2: after a stop at node 0, each variant must still descend from 1 to 2.
        for (int variant = 0; variant < 3; variant++)
        {
            var t = new Recorder(TestGraphs.Of(3, 1, 2)) { StopOnEnter = 0 };
            t.Run(0);
            t.StopOnEnter = -1;
            switch (variant)
            {
                case 0:
                    t.Run(1);
                    break;
                case 1:
                    t.Run([1]);
                    break;
                default:
                    t.Run();
                    break;
            }
            Assert.Equal([0, 1, 2], t.PreOrder);
        }
    }

    // ---- Run from a hook ----

    [Fact]
    public void RunFromAHookThrowsAndALaterRunStillStarts()
    {
        // 0 -> 1, 2 -> 3: a nested run during a run from 0 is rejected, then Run(2) works as usual.
        for (int hook = 0; hook < 3; hook++)
        {
            for (int variant = 0; variant < 3; variant++)
            {
                int nestIn = hook;
                int runVariant = variant;
                bool nest = true;
                var t = new Recorder(TestGraphs.Of(4, 0, 1, 2, 3))
                {
                    OnEnter = (r, node) =>
                    {
                        if (nestIn == 0 && nest && node == 1)
                        {
                            RunVariant(r, runVariant, 3);
                        }
                    },
                    OnLeave = (r, node) =>
                    {
                        if (nestIn == 1 && nest && node == 1)
                        {
                            RunVariant(r, runVariant, 3);
                        }
                    },
                    Decide = (r, _, target, _, _) =>
                    {
                        if (nestIn == 2 && nest && target == 1)
                        {
                            RunVariant(r, runVariant, 3);
                        }
                        return null;
                    },
                };
                Assert.Throws<InvalidOperationException>(() => RunVariant(t, runVariant, 0));
                Assert.False(t.Visited(3), $"hook {hook}, variant {variant}");

                nest = false;
                t.Run(2);
                Assert.Equal([2, 3], t.PreOrder[^2..]);
            }
        }
    }

    private static void RunVariant(Recorder r, int variant, int node)
    {
        switch (variant)
        {
            case 0:
                r.Run();
                break;
            case 1:
                r.Run(node);
                break;
            default:
                r.Run([node]);
                break;
        }
    }

    // ---- A recursive algorithm turned into hooks: subtree sizes ----

    [Fact]
    public void SubtreeSizesOnATree()
    {
        // 0 -> 1, 0 -> 2, 1 -> 3, 1 -> 4, 2 -> 5
        var sizes = new SubtreeSizes(TestGraphs.Of(6, 0, 1, 0, 2, 1, 3, 1, 4, 2, 5));
        sizes.Run();
        Assert.Equal([6, 3, 2, 1, 1, 1], sizes.Size);
    }

    [Fact]
    public void SubtreeSizesOnADeepPath()
    {
        const int n = 1_000_000;
        var sizes = new SubtreeSizes(TestGraphs.Path(n));
        sizes.Run();
        Assert.Equal(n, sizes.Size[0]);
        Assert.Equal(1, sizes.Size[n - 1]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(16)]
    [InlineData(1000)]
    public void IsVisitedAndIsOnStackRejectOutOfRangeNodes(int node)
    {
        // Java returns false or throws IndexOutOfBoundsException; here both throw always, the
        // exception Java's maps to.
        var t = new ExposedStates(TestGraphs.Isolated(3));
        t.Run();
        Assert.True(t.Visited(2));
        Assert.False(t.OnStack(2));
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => t.Visited(node));
        Assert.StartsWith($"Index {node} out of bounds for length 3", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => t.OnStack(node));
        Assert.StartsWith($"Index {node} out of bounds for length 3", e.Message, StringComparison.Ordinal);
    }

    // ---- Helpers ----

    /// <summary>
    /// Records every callback; descends by default, or answers from <see cref="Decide"/> (null for the
    /// default). Stops and prunes at the configured nodes, and calls the optional hooks after recording.
    /// </summary>
    private sealed class Recorder(Graph graph) : DfsEdgeTraverser(graph)
    {
        public List<int> PreOrder { get; } = [];

        public List<int> PostOrder { get; } = [];

        public List<int> Edges { get; } = [];

        public List<TargetState> States { get; } = [];

        public Func<Recorder, int, int, int, TargetState, EdgeAction?>? Decide { get; init; }

        public Action<Recorder, int>? OnEnter { get; init; }

        public Action<Recorder, int>? OnLeave { get; init; }

        public int StopOnEnter { get; set; } = -1;

        public int StopOnLeave { get; set; } = -1;

        public int PruneNode { get; set; } = -1;

        public bool PruneAll { get; set; }

        public bool Visited(int node) => IsVisited(node);

        public bool OnStack(int node) => IsOnStack(node);

        public void StopNow() => Stop();

        protected override bool EnterNode(int node)
        {
            PreOrder.Add(node);
            OnEnter?.Invoke(this, node);
            if (node == StopOnEnter)
            {
                Stop();
            }
            return !PruneAll && node != PruneNode;
        }

        protected override void LeaveNode(int node)
        {
            PostOrder.Add(node);
            OnLeave?.Invoke(this, node);
            if (node == StopOnLeave)
            {
                Stop();
            }
        }

        protected override EdgeAction VisitEdge(int source, int target, int edgeIndex, TargetState state)
        {
            Assert.Equal(source, Graph.EdgeSource(edgeIndex));
            Assert.Equal(target, Graph.EdgeTarget(edgeIndex));
            Assert.True(IsOnStack(source));
            Edges.Add(edgeIndex);
            States.Add(state);
            return Decide?.Invoke(this, source, target, edgeIndex, state)
                ?? base.VisitEdge(source, target, edgeIndex, state);
        }
    }

    private sealed class ExposedStates(Graph graph) : DfsEdgeTraverser(graph)
    {
        public bool Visited(int node) => IsVisited(node);

        public bool OnStack(int node) => IsOnStack(node);
    }

    private sealed class LeaveOnly(Graph graph) : DfsEdgeTraverser(graph)
    {
        public List<int> Left { get; } = [];

        protected override void LeaveNode(int node) => Left.Add(node);
    }

    private sealed class CountingEdges(Graph graph) : DfsEdgeTraverser(graph)
    {
        public int EdgeCount { get; private set; }

        public int Left { get; private set; }

        protected override EdgeAction VisitEdge(int source, int target, int edgeIndex, TargetState state)
        {
            EdgeCount++;
            return base.VisitEdge(source, target, edgeIndex, state);
        }

        protected override void LeaveNode(int node) => Left++;
    }

    private sealed class SubtreeSizes : DfsEdgeTraverser
    {
        private readonly int[] _parent;

        public SubtreeSizes(Graph tree)
            : base(tree)
        {
            _parent = new int[tree.NodeCount];
            Size = new int[tree.NodeCount];
            Array.Fill(_parent, -1);
        }

        public int[] Size { get; }

        protected override EdgeAction VisitEdge(int source, int target, int edgeIndex, TargetState state)
        {
            if (state == TargetState.Unvisited)
            {
                _parent[target] = source;
            }
            return EdgeAction.Descend;
        }

        protected override void LeaveNode(int node)
        {
            Size[node]++;
            if (_parent[node] >= 0)
            {
                Size[_parent[node]] += Size[node];
            }
        }
    }
}
