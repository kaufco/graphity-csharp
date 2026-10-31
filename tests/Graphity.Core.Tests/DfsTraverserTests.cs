namespace Graphity.Core.Tests;

public sealed class DfsTraverserTests
{
    // ---- Basic order ----

    [Fact]
    public void RunVisitsSinglePathInPreOrder()
    {
        var t = new RecordingDfsTraverser(TestGraphs.Path(4));
        t.Run();
        Assert.Equal([0, 1, 2, 3], t.PreOrder);
        Assert.Equal([3, 2, 1, 0], t.PostOrder);
    }

    [Fact]
    public void RunVisitsBinaryTreeInSortedPreOrder()
    {
        // Binary tree: 0 -> 1, 2; 1 -> 3, 4; 2 -> 5, 6.
        var g = TestGraphs.Of(7, 0, 1, 0, 2, 1, 3, 1, 4, 2, 5, 2, 6);
        var t = new RecordingDfsTraverser(g);
        t.Run();
        Assert.Equal([0, 1, 3, 4, 2, 5, 6], t.PreOrder);
        Assert.Equal([3, 4, 1, 5, 6, 2, 0], t.PostOrder);
    }

    [Fact]
    public void RunOnEmptyGraphIsNoOp()
    {
        var t = new RecordingDfsTraverser(TestGraphs.Empty());
        t.Run();
        Assert.Empty(t.PreOrder);
        Assert.Empty(t.PostOrder);
    }

    [Fact]
    public void RunTerminatesOnCycle()
    {
        var t = new RecordingDfsTraverser(TestGraphs.Cycle(5));
        t.Run();
        Assert.Equal([0, 1, 2, 3, 4], t.PreOrder);
        Assert.Equal([4, 3, 2, 1, 0], t.PostOrder);
    }

    [Fact]
    public void RunOnCliqueVisitsAllNodes()
    {
        var t = new RecordingDfsTraverser(TestGraphs.Clique(4));
        t.Run();
        Assert.Equal([0, 1, 2, 3], t.PreOrder);
    }

    // ---- Run contract ----

    [Fact]
    public void RunFromSingleNodeVisitsReachableOnly()
    {
        var g = TestGraphs.Of(5, 0, 1, 1, 2, 3, 4);
        var t = new RecordingDfsTraverser(g);
        t.Run(0);
        Assert.Equal([0, 1, 2], t.PreOrder);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void RunFromNodeOutOfRangeThrows(int startNode)
    {
        var t = new RecordingDfsTraverser(TestGraphs.Isolated(4));
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
        var t = new RecordingDfsTraverser(TestGraphs.Isolated(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => t.Run([0, startNode]));
        Assert.Empty(t.PreOrder);
        Assert.False(t.WasVisited(0));
    }

    [Fact]
    public void RunFromNodeListVisitsInListOrder()
    {
        // Disjoint: {0->1}, {2->3}, {4->5}.
        var g = TestGraphs.Of(6, 0, 1, 2, 3, 4, 5);
        var t = new RecordingDfsTraverser(g);
        t.Run([4, 0, 2]);
        Assert.Equal([4, 5, 0, 1, 2, 3], t.PreOrder);
    }

    [Fact]
    public void RunFromNodeListSkipsAlreadyVisitedStartNodes()
    {
        var t = new RecordingDfsTraverser(TestGraphs.Path(4));
        t.Run(0); // visits 0, 1, 2, 3
        t.Run([1, 2]); // all already visited
        Assert.Equal([0, 1, 2, 3], t.PreOrder);
    }

    [Fact]
    public void EveryEntryPointSkipsVisitedNodes()
    {
        // 0 -> 1, plus isolated 2 and 3; duplicates and repeated starts must not revisit.
        var t = new RecordingDfsTraverser(TestGraphs.Of(4, 0, 1));
        t.Run(0);
        t.Run(1);
        t.Run([0, 2, 2]);
        t.Run();
        Assert.Equal([0, 1, 2, 3], t.PreOrder);
    }

    [Fact]
    public void RunFromEmptyNodeListIsNoOp()
    {
        var t = new RecordingDfsTraverser(TestGraphs.Path(3));
        t.Run(ReadOnlySpan<int>.Empty);
        Assert.Empty(t.PreOrder);
    }

    [Fact]
    public void RunFromSortedIntViewVisitsTheRootNodes()
    {
        // Not in Java as such: the view of Graphs.RootNodes feeds Run(ReadOnlySpan<int>) directly.
        var g = BiAdjacentGraph.Of(TestGraphs.Of(4, 0, 1, 3, 2));
        var t = new RecordingDfsTraverser(g);
        t.Run(Graphs.RootNodes(g));
        Assert.Equal([0, 1, 3, 2], t.PreOrder);
    }

    [Fact]
    public void MultipleRunCallsDoNotRevisit()
    {
        var t = new RecordingDfsTraverser(TestGraphs.Path(4));
        t.Run(0);
        t.Run(0);
        t.Run();
        Assert.Equal([0, 1, 2, 3], t.PreOrder);
    }

    [Fact]
    public void RunThenRunCombinesTraversals()
    {
        // Two components.
        var g = TestGraphs.Of(5, 0, 1, 2, 3, 3, 4);
        var t = new RecordingDfsTraverser(g);
        t.Run(2);
        t.Run();
        Assert.Equal([2, 3, 4, 0, 1], t.PreOrder);
    }

    // ---- Pruning via EnterNode returning false ----

    [Fact]
    public void PruneSkipsSubtreeButStillLeavesNode()
    {
        // Binary tree: 0 -> 1, 2; 1 -> 3, 4; 2 -> 5, 6. Prune at node 1: subtree 3, 4 skipped.
        var g = TestGraphs.Of(7, 0, 1, 0, 2, 1, 3, 1, 4, 2, 5, 2, 6);
        var t = new LambdaDfs(g, node => node != 1);
        t.Run(0);
        // Node 1 is entered (and left) but its children 3, 4 are never discovered.
        Assert.Equal([0, 1, 2, 5, 6], t.Enter);
        // LeaveNode fires for every entered node, pruned node 1 included.
        Assert.Equal([1, 5, 6, 2, 0], t.Leave);
    }

    [Fact]
    public void PruningRootLeavesItImmediately()
    {
        var t = new LambdaDfs(TestGraphs.Path(3), _ => false);
        t.Run(0);
        Assert.Equal([0], t.Enter);
        Assert.Equal([0], t.Leave);
    }

    // ---- IsOnStack / IsVisited ----

    [Fact]
    public void AncestorsAreOnStackDuringEnterNode()
    {
        // Path 0 -> 1 -> 2 -> 3: when node k is entered, exactly 0..k are on the stack.
        var t = new CheckingDfs(TestGraphs.Path(4), (self, node) =>
        {
            for (int i = 0; i <= node; i++)
            {
                Assert.True(self.OnStack(i), $"node {i} on stack when entering {node}");
            }
            for (int i = node + 1; i < 4; i++)
            {
                Assert.False(self.OnStack(i), $"node {i} not on stack when entering {node}");
            }
        });
        t.Run(0);
        Assert.Equal(4, t.Entered);
    }

    [Fact]
    public void NodeIsOffStackAfterLeave()
    {
        // Diamond 0 -> 1, 2; 1 -> 3; 2 -> 3. After leaving 1, it must be off-stack before 2 is entered.
        var t = new CheckingDfs(TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3), (self, node) =>
        {
            if (node == 2)
            {
                Assert.False(self.OnStack(1), "sibling 1 already left");
                Assert.True(self.Visited(1), "sibling 1 already visited");
                Assert.True(self.OnStack(0), "ancestor 0 still on stack");
            }
        });
        t.Run(0);
        Assert.Equal(4, t.Entered);
    }

    // ---- Order parity with a recursive depth-first search ----

    [Fact]
    public void OrderMatchesRecursiveOnDeterministicShapes()
    {
        Graph[] shapes =
        [
            TestGraphs.Path(6),
            TestGraphs.Cycle(6),
            TestGraphs.Clique(5),
            TestGraphs.OutStar(6),
            TestGraphs.InStar(6),
            TestGraphs.Of(7, 0, 1, 0, 2, 1, 3, 1, 4, 2, 5, 2, 6),
            TestGraphs.Of(6, 0, 1, 0, 2, 1, 3, 2, 3, 3, 4, 4, 5), // diamond + tail
            TestGraphs.Isolated(4),
        ];
        foreach (var g in shapes)
        {
            AssertSameOrderAsRecursive(g, "deterministic shape");
        }
    }

    [Fact]
    public void OrderMatchesRecursiveOnRandomGraphs()
    {
        for (long seed = 0; seed < 300; seed++)
        {
            var lg = RandomGraphs.AnyGraph(seed);
            AssertSameOrderAsRecursive(lg.Graph, lg.Describe());
        }
    }

    private static void AssertSameOrderAsRecursive(Graph g, string desc)
    {
        var recursive = new RecursiveDfsOrder(g);
        recursive.Run();
        var iterative = new RecordingDfsTraverser(g);
        iterative.Run();
        Assert.True(recursive.PreOrder.SequenceEqual(iterative.PreOrder), $"preorder parity ({desc})");
        Assert.True(recursive.PostOrder.SequenceEqual(iterative.PostOrder), $"postorder parity ({desc})");
    }

    // ---- Stack safety (regression guard, mirrors DeepGraphStackSafetyTests) ----

    [Fact]
    public void DeepPathDoesNotOverflowStack()
    {
        const int deep = 1_000_000;
        var counter = new CountingDfs(TestGraphs.Path(deep));
        counter.Run();
        Assert.Equal(deep, counter.Entered);
        Assert.Equal(deep, counter.Left);
    }

    // ---- Stop() ----

    [Fact]
    public void StopInEnterNodeEndsTheRunWithoutLeavingThePath()
    {
        var t = new StoppingDfs(TestGraphs.Path(5)) { StopOnEnter = 2 };
        t.Run();
        Assert.Equal([0, 1, 2], t.Enter);
        Assert.Empty(t.Leave);
        for (int v = 0; v < 5; v++)
        {
            Assert.False(t.OnStack(v), $"node {v} on stack");
        }
        Assert.True(t.Visited(2));
        Assert.False(t.Visited(3));

        t.StopOnEnter = -1;
        t.Run();
        Assert.Equal([0, 1, 2, 3, 4], t.Enter);
        Assert.Equal([4, 3], t.Leave);
    }

    [Fact]
    public void StopInLeaveNodeEndsTheRun()
    {
        // Binary tree: 0 -> 1, 2; 1 -> 3, 4; 2 -> 5, 6.
        var t = new StoppingDfs(TestGraphs.Of(7, 0, 1, 0, 2, 1, 3, 1, 4, 2, 5, 2, 6)) { StopOnLeave = 3 };
        t.Run();
        Assert.Equal([0, 1, 3], t.Enter);
        Assert.Equal([3], t.Leave);
        Assert.False(t.OnStack(0));
        Assert.False(t.OnStack(1));
    }

    [Fact]
    public void StopOnAPrunedNodeSkipsItsLeaveNode()
    {
        var t = new StoppingDfs(TestGraphs.Isolated(3)) { Prune = true, StopOnEnter = 1 };
        t.Run();
        Assert.Equal([0, 1], t.Enter);
        Assert.Equal([0], t.Leave);
    }

    [Fact]
    public void StopEndsTheWholeRunFromANodeList()
    {
        var t = new StoppingDfs(TestGraphs.Isolated(4)) { StopOnLeave = 3 };
        t.Run([3, 1, 2]);
        Assert.Equal([3], t.Enter);
    }

    [Fact]
    public void StopOutsideARunHasNoEffect()
    {
        var t = new StoppingDfs(TestGraphs.Path(3));
        t.StopNow();
        t.Run(0);
        Assert.Equal([0, 1, 2], t.Enter);
    }

    [Fact]
    public void StopInLeaveNodeOfAPrunedRootEndsTheRun()
    {
        var t = new StoppingDfs(TestGraphs.Isolated(3)) { Prune = true, StopOnLeave = 0 };
        t.Run();
        Assert.Equal([0], t.Enter);
        Assert.Equal([0], t.Leave);
    }

    [Fact]
    public void StopInLeaveNodeOfAPrunedChildEndsTheRun()
    {
        // 0 -> 1, 0 -> 2: node 1 is pruned and stops the run when it is left.
        var t = new StoppingDfs(TestGraphs.Of(3, 0, 1, 0, 2)) { PruneNode = 1, StopOnLeave = 1 };
        t.Run();
        Assert.Equal([0, 1], t.Enter);
        Assert.Equal([1], t.Leave);
        Assert.False(t.OnStack(0));
        Assert.False(t.Visited(2));
    }

    [Fact]
    public void RunFromNodeListClearsAnEarlierStop()
    {
        var t = new StoppingDfs(TestGraphs.Isolated(3)) { StopOnEnter = 0 };
        t.Run([0, 1]);
        Assert.Equal([0], t.Enter);
        t.StopOnEnter = -1;
        t.Run([1, 2]);
        Assert.Equal([0, 1, 2], t.Enter);
    }

    // ---- Run from a hook ----

    [Fact]
    public void RunFromAHookThrowsAndALaterRunStillStarts()
    {
        // 0 -> 1, 2 -> 3: a nested run during a run from 0 is rejected, then Run(2) works as usual.
        for (int hook = 0; hook < 2; hook++)
        {
            for (int variant = 0; variant < 3; variant++)
            {
                var t = new NestingDfs(TestGraphs.Of(4, 0, 1, 2, 3)) { NestIn = hook, Variant = variant };
                Assert.Throws<InvalidOperationException>(() => t.RunVariant(0));
                Assert.False(t.Visited(3), $"hook {hook}, variant {variant}");

                t.Nest = false;
                t.Run(2);
                Assert.Equal([0, 1, 2, 3], t.Enter);
            }
        }
    }

    [Fact]
    public void RunFromEnterNodeOfTheRootThrows()
    {
        var t = new RootNestingDfs(TestGraphs.Of(2, 0, 1));
        Assert.Throws<InvalidOperationException>(() => t.Run(0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(16)]
    [InlineData(1000)]
    public void IsVisitedAndIsOnStackRejectOutOfRangeNodes(int node)
    {
        // Java returns false or throws IndexOutOfBoundsException; here both throw always, the
        // exception Java's maps to, inside a run and outside.
        var t = new CheckingDfs(TestGraphs.Isolated(3), (self, _) =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => self.Visited(node));
            Assert.Throws<ArgumentOutOfRangeException>(() => self.OnStack(node));
        });
        t.Run();
        Assert.Equal(3, t.Entered);
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => t.Visited(node));
        Assert.StartsWith($"Index {node} out of bounds for length 3", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => t.OnStack(node));
        Assert.StartsWith($"Index {node} out of bounds for length 3", e.Message, StringComparison.Ordinal);
    }

    // ---- Helpers ----

    /// <summary>Records enter and leave; <see cref="Descend"/> decides whether to descend.</summary>
    private sealed class LambdaDfs(Graph graph, Func<int, bool> descend) : DfsTraverser(graph)
    {
        public List<int> Enter { get; } = [];

        public List<int> Leave { get; } = [];

        private Func<int, bool> Descend { get; } = descend;

        protected override bool EnterNode(int node)
        {
            Enter.Add(node);
            return Descend(node);
        }

        protected override void LeaveNode(int node) => Leave.Add(node);
    }

    /// <summary>Calls a check from <see cref="EnterNode"/> and exposes the state queries.</summary>
    private sealed class CheckingDfs(Graph graph, Action<CheckingDfs, int> check) : DfsTraverser(graph)
    {
        public int Entered { get; private set; }

        public bool OnStack(int node) => IsOnStack(node);

        public bool Visited(int node) => IsVisited(node);

        protected override bool EnterNode(int node)
        {
            check(this, node);
            Entered++;
            return true;
        }
    }

    private sealed class CountingDfs(Graph graph) : DfsTraverser(graph)
    {
        public int Entered { get; private set; }

        public int Left { get; private set; }

        protected override bool EnterNode(int node)
        {
            Entered++;
            return true;
        }

        protected override void LeaveNode(int node) => Left++;
    }

    /// <summary>Records pre- and postorder and stops when entering or leaving the given node.</summary>
    private sealed class StoppingDfs(Graph graph) : DfsTraverser(graph)
    {
        public List<int> Enter { get; } = [];

        public List<int> Leave { get; } = [];

        public int StopOnEnter { get; set; } = -1;

        public int StopOnLeave { get; set; } = -1;

        public bool Prune { get; set; }

        public int PruneNode { get; set; } = -1;

        public bool OnStack(int node) => IsOnStack(node);

        public bool Visited(int node) => IsVisited(node);

        public void StopNow() => Stop();

        protected override bool EnterNode(int node)
        {
            Enter.Add(node);
            if (node == StopOnEnter)
            {
                Stop();
            }
            return !Prune && node != PruneNode;
        }

        protected override void LeaveNode(int node)
        {
            Leave.Add(node);
            if (node == StopOnLeave)
            {
                Stop();
            }
        }
    }

    /// <summary>
    /// Calls the run method <see cref="Variant"/> of its own from the hook <see cref="NestIn"/> of
    /// node 1 while <see cref="Nest"/> is set.
    /// </summary>
    private sealed class NestingDfs(Graph graph) : DfsTraverser(graph)
    {
        public List<int> Enter { get; } = [];

        /// <summary>0: EnterNode, 1: LeaveNode.</summary>
        public int NestIn { get; set; }

        /// <summary>0: Run(), 1: Run(int), 2: Run(ReadOnlySpan).</summary>
        public int Variant { get; set; }

        public bool Nest { get; set; } = true;

        public bool Visited(int node) => IsVisited(node);

        public void RunVariant(int node)
        {
            switch (Variant)
            {
                case 0:
                    Run();
                    break;
                case 1:
                    Run(node);
                    break;
                default:
                    Run([node]);
                    break;
            }
        }

        protected override bool EnterNode(int node)
        {
            Enter.Add(node);
            if (NestIn == 0 && Nest && node == 1)
            {
                RunVariant(3);
            }
            return true;
        }

        protected override void LeaveNode(int node)
        {
            if (NestIn == 1 && Nest && node == 1)
            {
                RunVariant(3);
            }
        }
    }

    private sealed class RootNestingDfs(Graph graph) : DfsTraverser(graph)
    {
        protected override bool EnterNode(int node)
        {
            if (node == 0)
            {
                Run(1);
            }
            return true;
        }
    }
}
