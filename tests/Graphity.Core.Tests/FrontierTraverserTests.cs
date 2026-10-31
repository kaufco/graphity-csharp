namespace Graphity.Core.Tests;

public sealed class FrontierTraverserTests
{
    [Fact]
    public void RunOnPathGivesBfsOrder()
    {
        var t = new RecordingFrontierTraverser(TestGraphs.Path(4));
        t.Run(0);
        Assert.Equal([0, 1, 2, 3], t.VisitOrder);
        Assert.Equal([0, 1, 2, 3], t.LevelAtVisit);
    }

    [Fact]
    public void RunOnBinaryTreeGivesLevelOrder()
    {
        // 0 -> 1, 2; 1 -> 3, 4; 2 -> 5, 6.
        var t = new RecordingFrontierTraverser(TestGraphs.Of(7, 0, 1, 0, 2, 1, 3, 1, 4, 2, 5, 2, 6));
        t.Run(0);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6], t.VisitOrder);
        Assert.Equal([0, 1, 1, 2, 2, 2, 2], t.LevelAtVisit);
    }

    [Fact]
    public void RunCoversDisconnectedForestInNodeOrder()
    {
        // Two components: 0->1 and 2->3.
        var t = new RecordingFrontierTraverser(TestGraphs.Of(4, 0, 1, 2, 3));
        t.Run();
        Assert.Equal([0, 1, 2, 3], t.VisitOrder);
    }

    [Fact]
    public void RunOnEmptyGraphIsNoOp()
    {
        var t = new RecordingFrontierTraverser(TestGraphs.Empty());
        t.Run();
        Assert.Empty(t.VisitOrder);
        Assert.Empty(t.CompletedLevels);
    }

    [Fact]
    public void RunOnCycleTerminates()
    {
        var t = new RecordingFrontierTraverser(TestGraphs.Cycle(5));
        t.Run(0);
        Assert.Equal([0, 1, 2, 3, 4], t.VisitOrder);
        Assert.Equal([0, 1, 2, 3, 4], t.LevelAtVisit);
    }

    [Fact]
    public void RunOnOutStarGivesRootAtLevelZero()
    {
        var t = new RecordingFrontierTraverser(TestGraphs.OutStar(5));
        t.Run(0);
        Assert.Equal([0, 1, 2, 3, 4], t.VisitOrder);
        Assert.Equal([0, 1, 1, 1, 1], t.LevelAtVisit);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void RunFromNodeOutOfRangeThrows(int startNode)
    {
        var t = new RecordingFrontierTraverser(TestGraphs.Isolated(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => t.Run(startNode));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void RunFromNodeListOutOfRangeThrowsBeforeAnySeed(int startNode)
    {
        var t = new RecordingFrontierTraverser(TestGraphs.Isolated(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => t.Run([0, startNode]));
        Assert.Empty(t.VisitOrder);
        Assert.False(t.WasVisited(0), "valid seed before the bad one");
    }

    [Fact]
    public void RunFromNodeListPutsAllSeedsAtLevelZero()
    {
        // Disjoint: 0->1, 2->3, 4->5.
        var t = new RecordingFrontierTraverser(TestGraphs.Of(6, 0, 1, 2, 3, 4, 5));
        t.Run([4, 0, 2]);
        Assert.Equal([4, 0, 2, 5, 1, 3], t.VisitOrder);
        Assert.Equal([0, 0, 0, 1, 1, 1], t.LevelAtVisit);
        Assert.Equal([0, 1], t.CompletedLevels);
    }

    [Fact]
    public void RunFromTheRootNodesTreatsAllRootsAsLevelZero()
    {
        // Roots 0 and 3: 0 -> 1 -> 2, 3 -> 2.
        var g = BiAdjacentGraph.Of(TestGraphs.Of(4, 0, 1, 1, 2, 3, 2));
        var t = new RecordingFrontierTraverser(g);
        t.Run(Graphs.RootNodes(g));
        Assert.Equal([0, 3, 1, 2], t.VisitOrder);
        Assert.Equal([0, 0, 1, 1], t.LevelAtVisit);
        Assert.Equal([0, 1], t.CompletedLevels);
    }

    [Fact]
    public void RunFromNodeListSkipsAlreadyVisited()
    {
        var t = new RecordingFrontierTraverser(TestGraphs.Path(4));
        t.Run(0);
        t.Run([1, 2]);
        Assert.Equal([0, 1, 2, 3], t.VisitOrder);
    }

    [Fact]
    public void EveryEntryPointSkipsVisitedNodes()
    {
        // 0 -> 1, plus isolated 2 and 3; duplicates and repeated starts must not revisit.
        var t = new RecordingFrontierTraverser(TestGraphs.Of(4, 0, 1));
        t.Run(0);
        t.Run(1);
        t.Run([0, 2, 2]);
        t.Run();
        Assert.Equal([0, 1, 2, 3], t.VisitOrder);
        Assert.Equal([0, 1, 0, 0], t.CompletedLevels);
    }

    [Fact]
    public void RunFromEmptyNodeListIsNoOp()
    {
        var t = new RecordingFrontierTraverser(TestGraphs.Path(3));
        t.Run(ReadOnlySpan<int>.Empty);
        Assert.Empty(t.VisitOrder);
    }

    [Fact]
    public void MultipleRunCallsDoNotRevisit()
    {
        var t = new RecordingFrontierTraverser(TestGraphs.Path(3));
        t.Run(0);
        t.Run(0);
        t.Run();
        Assert.Equal([0, 1, 2], t.VisitOrder);
    }

    [Fact]
    public void PreMarkedNodesAreExcludedFromTraversal()
    {
        var t = new RecordingFrontierTraverser(TestGraphs.Path(5));
        t.PreMark(2);
        t.PreMark(3);
        t.Run(0);
        // From 0: visits 0, 1; stops at pre-marked 2.
        Assert.Equal([0, 1], t.VisitOrder);
    }

    [Fact]
    public void CurrentLevelIsAvailableDuringVisit()
    {
        // Path 0->1->2->3. Check CurrentLevel at each visit.
        var t = new LevelRecorder(TestGraphs.Path(4));
        t.Run(0);
        Assert.Equal([0, 1, 2, 3], t.Levels);
    }

    [Fact]
    public void LeaveLevelFiresForEachLevel()
    {
        var t = new RecordingFrontierTraverser(TestGraphs.Path(4));
        t.Run(0);
        Assert.Equal([0, 1, 2, 3], t.CompletedLevels);
    }

    [Fact]
    public void LeaveLevelFiresOncePerTreeInForest()
    {
        // Two components: 0->1 (2 levels) and 2->3->4 (3 levels).
        var t = new RecordingFrontierTraverser(TestGraphs.Of(5, 0, 1, 2, 3, 3, 4));
        t.Run();
        // First tree: levels 0, 1. Second tree: levels 0, 1, 2.
        Assert.Equal([0, 1, 0, 1, 2], t.CompletedLevels);
    }

    [Fact]
    public void LeaveLevelNotFiredForEmptyGraph()
    {
        var t = new RecordingFrontierTraverser(TestGraphs.Empty());
        t.Run();
        Assert.Empty(t.CompletedLevels);
    }

    [Fact]
    public void PruningByOmittingEnqueueChildren()
    {
        // Binary tree: 0 -> 1, 2; 1 -> 3, 4; 2 -> 5, 6. Prune at node 1.
        var t = new PruningFrontier(TestGraphs.Of(7, 0, 1, 0, 2, 1, 3, 1, 4, 2, 5, 2, 6), 1);
        t.Run(0);
        Assert.Equal([0, 1, 2, 5, 6], t.Seen);
    }

    [Fact]
    public void EnqueueReturnsTrueForNewNodesFalseForVisited()
    {
        var t = new ExposedEnqueueFrontier(TestGraphs.Isolated(3));
        Assert.True(t.CallEnqueue(1));
        Assert.False(t.CallEnqueue(1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(1000)]
    public void EnqueueOutOfRangeThrows(int node)
    {
        var t = new ExposedEnqueueFrontier(TestGraphs.Isolated(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => t.CallEnqueue(node));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(32)]
    [InlineData(1000)]
    public void EnqueueChildrenAndIsVisitedRejectOutOfRangeNodes(int node)
    {
        // Java's enqueueChildren throws IndexOutOfBoundsException, and isVisited returns false or
        // throws it; here both throw always, the exception Java's maps to.
        var t = new ExposedEnqueueFrontier(TestGraphs.Isolated(3));
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => t.CallEnqueueChildren(node));
        Assert.StartsWith($"Index {node} out of bounds for length 3", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => t.CallIsVisited(node));
        Assert.StartsWith($"Index {node} out of bounds for length 3", e.Message, StringComparison.Ordinal);
        Assert.False(t.CallIsVisited(2));
    }

    [Fact]
    public void EnqueueSchedulesCustomFrontier()
    {
        // Disjoint: 0->1, 2->3, 4->5. Use Enqueue() to schedule 2 and 4 explicitly.
        var t = new CustomFrontier(TestGraphs.Of(6, 0, 1, 2, 3, 4, 5));
        t.Run(0);
        Assert.Equal([0, 2, 4, 3, 5], t.Visits);
    }

    [Fact]
    public void IsVisitedReflectsEnqueuedState()
    {
        var t = new EnqueueCheckFrontier(TestGraphs.Of(3, 0, 1, 0, 2));
        t.Run(0);
        // After EnqueueChildren, 1 and 2 are marked visited even before VisitNode fires.
        Assert.True(t.AllVisitedAfterRoot);
    }

    [Fact]
    public void RunOnDiamondVisitsTargetOnce()
    {
        // Diamond 0->1, 0->2, 1->3, 2->3. Node 3 enqueued by 1, skipped by 2.
        var t = new RecordingFrontierTraverser(TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3));
        t.Run(0);
        Assert.Equal([0, 1, 2, 3], t.VisitOrder);
        Assert.Equal([0, 1, 1, 2], t.LevelAtVisit);
    }

    // ---- Enqueue outside a run ----

    [Fact]
    public void NodesEnqueuedBeforeARunFormItsLevelZeroInEveryRunVariant()
    {
        // 0 -> 1 and 3 -> 2. After Run(0), the 3 is enqueued outside a run; the start 0 of Run(0)
        // and Run([0]) is visited already.
        for (int variant = 0; variant < 3; variant++)
        {
            var t = new RecordingFrontierTraverser(TestGraphs.Of(4, 0, 1, 3, 2));
            t.Run(0);
            t.EnqueueOutsideRun(3);
            switch (variant)
            {
                case 0:
                    t.Run();
                    break;
                case 1:
                    t.Run(0);
                    break;
                default:
                    t.Run([0]);
                    break;
            }
            Assert.Equal([0, 1, 3, 2], t.VisitOrder);
            Assert.Equal([0, 1, 0, 1], t.LevelAtVisit);
            Assert.Equal([0, 1, 0, 1], t.CompletedLevels);
        }
    }

    [Fact]
    public void RunVisitsANodeEnqueuedBeforeItEvenWithNoRootLeft()
    {
        var t = new RecordingFrontierTraverser(TestGraphs.Isolated(2));
        t.Run(0);
        t.EnqueueOutsideRun(1);
        t.Run();
        Assert.Equal([0, 1], t.VisitOrder);
    }

    [Fact]
    public void NodesEnqueuedBeforeARunComeAheadOfItsStartNode()
    {
        // 0 -> 1 and 3 -> 2: the 3 joins the start 0 in level 0, ahead of it.
        var t = new RecordingFrontierTraverser(TestGraphs.Of(4, 0, 1, 3, 2));
        t.EnqueueOutsideRun(3);
        t.Run(0);
        Assert.Equal([3, 0, 2, 1], t.VisitOrder);
        Assert.Equal([0, 0, 1, 1], t.LevelAtVisit);
    }

    // ---- Stop() ----

    [Fact]
    public void StopAtTheFirstSeedDropsTheOtherSeedsOfTheSameLevel()
    {
        var t = new StoppingFrontier(TestGraphs.Of(4, 0, 1, 2, 3)) { StopOnVisit = 0 };
        t.Run([0, 2]);
        Assert.Equal([0], t.Visits);
        Assert.Empty(t.Levels);
    }

    [Fact]
    public void NodesEnqueuedWhenTheLastLevelCompletesFormTheNextLevel()
    {
        // 0 -> 1, plus isolated 2 and 3; level 1 is the last level of Run(0) and enqueues 2.
        var t = new StoppingFrontier(TestGraphs.Of(4, 0, 1)) { EnqueueOnLevel = 1, NodeToEnqueue = 2 };
        t.Run(0);
        Assert.Equal([0, 1, 2], t.Visits);
        Assert.Equal([0, 1, 2], t.VisitLevels);
        Assert.Equal([0, 1, 2], t.Levels);
    }

    [Fact]
    public void NodesEnqueuedWhenAnInnerLevelCompletesJoinTheNextLevel()
    {
        // 0 -> 1, plus isolated 2 and 3; level 0 enqueues 2 next to the child 1.
        var t = new StoppingFrontier(TestGraphs.Of(4, 0, 1)) { EnqueueOnLevel = 0, NodeToEnqueue = 2 };
        t.Run(0);
        Assert.Equal([0, 1, 2], t.Visits);
        Assert.Equal([0, 1, 1], t.VisitLevels);
        Assert.Equal([0, 1], t.Levels);
    }

    [Fact]
    public void StopWhenTheLastLevelCompletesDropsTheNodesItEnqueued()
    {
        var t = new StoppingFrontier(TestGraphs.Of(4, 0, 1)) { EnqueueOnLevel = 1, NodeToEnqueue = 2, StopOnLevel = 1 };
        t.Run(0);
        Assert.Equal([0, 1], t.Visits);
        Assert.True(t.WasVisited(2), "enqueued before the stop");

        t.EnqueueOnLevel = -1;
        t.StopOnLevel = -1;
        t.Run();
        Assert.Equal([0, 1, 3], t.Visits);
        Assert.Equal([0, 1, 0], t.VisitLevels);
    }

    [Fact]
    public void StopInVisitNodeDropsTheRestOfTheFrontier()
    {
        // 0 -> 1, 0 -> 2, 1 -> 3; stop while visiting 1: 2 and 3 are enqueued but dropped.
        var t = new StoppingFrontier(TestGraphs.Of(5, 0, 1, 0, 2, 1, 3)) { StopOnVisit = 1 };
        t.Run();
        Assert.Equal([0, 1], t.Visits);
        Assert.Equal([0], t.Levels);
        Assert.True(t.WasVisited(2), "enqueued before the stop");
        Assert.False(t.WasVisited(4));

        t.StopOnVisit = -1;
        t.Run();
        Assert.Equal([0, 1, 4], t.Visits);
    }

    [Fact]
    public void StopInLeaveLevelEndsTheRun()
    {
        // Binary tree: 0 -> 1, 2; 1 -> 3, 4; 2 -> 5, 6.
        var t = new StoppingFrontier(TestGraphs.Of(7, 0, 1, 0, 2, 1, 3, 1, 4, 2, 5, 2, 6)) { StopOnLevel = 1 };
        t.Run(0);
        Assert.Equal([0, 1, 2], t.Visits);
        Assert.Equal([0, 1], t.Levels);
    }

    [Fact]
    public void StopEndsTheWholeRunOverAllRoots()
    {
        var t = new StoppingFrontier(TestGraphs.Isolated(3)) { StopOnLevel = 0 };
        t.Run();
        Assert.Equal([0], t.Visits);
        t.StopOnLevel = -1;
        t.Run();
        Assert.Equal([0, 1, 2], t.Visits);
    }

    [Fact]
    public void RunOfOneNodeClearsAnEarlierStop()
    {
        var t = new StoppingFrontier(TestGraphs.Of(3, 1, 2)) { StopOnVisit = 0 };
        t.Run(0);
        t.StopOnVisit = -1;
        t.Run(1);
        Assert.Equal([0, 1, 2], t.Visits);
    }

    [Fact]
    public void RunFromNodeListClearsAnEarlierStop()
    {
        var t = new StoppingFrontier(TestGraphs.Of(3, 1, 2)) { StopOnVisit = 0 };
        t.Run(0);
        t.StopOnVisit = -1;
        t.Run([1]);
        Assert.Equal([0, 1, 2], t.Visits);
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
                var t = new NestingFrontier(TestGraphs.Of(4, 0, 1, 2, 3)) { NestIn = hook, Variant = variant };
                Assert.Throws<InvalidOperationException>(() => t.RunVariant(0));
                Assert.False(t.WasVisited(3), $"hook {hook}, variant {variant}");

                t.Nest = false;
                t.Run(2);
                Assert.Contains(2, t.Visits);
                Assert.Contains(3, t.Visits);
            }
        }
    }

    // ---- Helpers ----

    private sealed class LevelRecorder(Graph graph) : FrontierTraverser(graph)
    {
        public List<int> Levels { get; } = [];

        protected override void VisitNode(int node)
        {
            Levels.Add(CurrentLevel);
            EnqueueChildren(node);
        }
    }

    private sealed class PruningFrontier(Graph graph, int pruned) : FrontierTraverser(graph)
    {
        public List<int> Seen { get; } = [];

        protected override void VisitNode(int node)
        {
            Seen.Add(node);
            if (node != pruned)
            {
                EnqueueChildren(node);
            }
        }
    }

    private sealed class ExposedEnqueueFrontier(Graph graph) : FrontierTraverser(graph)
    {
        public bool CallEnqueue(int node) => Enqueue(node);

        public void CallEnqueueChildren(int node) => EnqueueChildren(node);

        public bool CallIsVisited(int node) => IsVisited(node);

        protected override void VisitNode(int node)
        {
        }
    }

    private sealed class CustomFrontier(Graph graph) : FrontierTraverser(graph)
    {
        public List<int> Visits { get; } = [];

        protected override void VisitNode(int node)
        {
            Visits.Add(node);
            if (node == 0)
            {
                Enqueue(2);
                Enqueue(4);
            }
            else
            {
                EnqueueChildren(node);
            }
        }
    }

    private sealed class EnqueueCheckFrontier(Graph graph) : FrontierTraverser(graph)
    {
        public bool AllVisitedAfterRoot { get; private set; }

        protected override void VisitNode(int node)
        {
            if (node == 0)
            {
                EnqueueChildren(node);
                AllVisitedAfterRoot = IsVisited(1) && IsVisited(2);
            }
        }
    }

    /// <summary>
    /// Records visits, their levels and completed levels; stops when visiting a node or completing a
    /// level, and enqueues a node when completing a level.
    /// </summary>
    private sealed class StoppingFrontier(Graph graph) : FrontierTraverser(graph)
    {
        public List<int> Visits { get; } = [];

        public List<int> VisitLevels { get; } = [];

        public List<int> Levels { get; } = [];

        public int StopOnVisit { get; set; } = -1;

        public int StopOnLevel { get; set; } = -1;

        public int EnqueueOnLevel { get; set; } = -1;

        public int NodeToEnqueue { get; set; } = -1;

        public bool WasVisited(int node) => IsVisited(node);

        protected override void VisitNode(int node)
        {
            Visits.Add(node);
            VisitLevels.Add(CurrentLevel);
            EnqueueChildren(node);
            if (node == StopOnVisit)
            {
                Stop();
            }
        }

        protected override void LeaveLevel(int level)
        {
            Levels.Add(level);
            if (level == EnqueueOnLevel)
            {
                Enqueue(NodeToEnqueue);
            }
            if (level == StopOnLevel)
            {
                Stop();
            }
        }
    }

    /// <summary>Calls the run method <see cref="Variant"/> of its own from the hook <see cref="NestIn"/> while <see cref="Nest"/> is set.</summary>
    private sealed class NestingFrontier(Graph graph) : FrontierTraverser(graph)
    {
        public List<int> Visits { get; } = [];

        /// <summary>0: VisitNode, 1: LeaveLevel.</summary>
        public int NestIn { get; set; }

        /// <summary>0: Run(), 1: Run(int), 2: Run(ReadOnlySpan).</summary>
        public int Variant { get; set; }

        public bool Nest { get; set; } = true;

        public bool WasVisited(int node) => IsVisited(node);

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

        protected override void VisitNode(int node)
        {
            Visits.Add(node);
            EnqueueChildren(node);
            if (NestIn == 0 && Nest)
            {
                RunVariant(3);
            }
        }

        protected override void LeaveLevel(int level)
        {
            if (NestIn == 1 && Nest)
            {
                RunVariant(3);
            }
        }
    }
}
