using System.Collections;
using System.Collections.Immutable;

namespace Graphity.Core.Tests;

/// <remarks>
/// Not ported: <c>findCycleIsUnmodifiable</c> - Java checks that the returned <c>IntList</c> rejects
/// <c>add</c>; <see cref="ImmutableArray{T}"/> has no mutator, so the type itself guarantees it.
/// </remarks>
public sealed class GraphsTests
{
    // ---- Shape checks ----

    [Fact]
    public void IsDagTrueForEmptyGraph() => Assert.True(Graphs.IsDag(TestGraphs.Empty()));

    [Fact]
    public void IsDagTrueForIsolatedNodes() => Assert.True(Graphs.IsDag(TestGraphs.Isolated(5)));

    [Fact]
    public void IsDagTrueForPath() => Assert.True(Graphs.IsDag(TestGraphs.Path(10)));

    [Fact]
    public void IsDagTrueForDiamond() => Assert.True(Graphs.IsDag(TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3)));

    [Fact]
    public void IsDagFalseForCycle() => Assert.False(Graphs.IsDag(TestGraphs.Cycle(3)));

    [Fact]
    public void IsDagFalseForSelfLoop() => Assert.False(Graphs.IsDag(TestGraphs.Of(1, 0, 0)));

    [Fact]
    public void IsDagFalseForCycleBetweenOtherwiseDagComponents()
    {
        // DAG 0->1 plus cycle 2->3->2.
        Assert.False(Graphs.IsDag(TestGraphs.Of(4, 0, 1, 2, 3, 3, 2)));
    }

    [Fact]
    public void IsForestTrueForEmptyGraph() => Assert.True(Graphs.IsForest(TestGraphs.Empty()));

    [Fact]
    public void IsForestTrueForIsolatedNodes() => Assert.True(Graphs.IsForest(TestGraphs.Isolated(3)));

    [Fact]
    public void IsForestTrueForOutStar() => Assert.True(Graphs.IsForest(TestGraphs.OutStar(5)));

    [Fact]
    public void IsForestTrueForPath() => Assert.True(Graphs.IsForest(TestGraphs.Path(5)));

    [Fact]
    public void IsForestTrueForDisjointTrees()
    {
        // Two disjoint trees: {0->1, 0->2} and {3->4}.
        Assert.True(Graphs.IsForest(TestGraphs.Of(5, 0, 1, 0, 2, 3, 4)));
    }

    [Fact]
    public void IsForestFalseForDiamond()
    {
        // Diamond: node 3 has in-degree 2.
        Assert.False(Graphs.IsForest(TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3)));
    }

    [Fact]
    public void IsForestFalseForInStar()
    {
        // In-star: center node 0 has in-degree n-1.
        Assert.False(Graphs.IsForest(TestGraphs.InStar(4)));
    }

    [Fact]
    public void IsTreeTrueForEmptyGraph() => Assert.True(Graphs.IsTree(TestGraphs.Empty()));

    [Fact]
    public void IsTreeTrueForSingleNode() => Assert.True(Graphs.IsTree(TestGraphs.Isolated(1)));

    [Fact]
    public void IsTreeTrueForPath() => Assert.True(Graphs.IsTree(TestGraphs.Path(5)));

    [Fact]
    public void IsTreeTrueForOutStar() => Assert.True(Graphs.IsTree(TestGraphs.OutStar(5)));

    [Fact]
    public void IsTreeFalseForDisjointTrees()
    {
        // Two roots: {0->1} and {2->3}.
        Assert.False(Graphs.IsTree(TestGraphs.Of(4, 0, 1, 2, 3)));
    }

    [Fact]
    public void IsTreeFalseForIsolatedMultipleNodes() => Assert.False(Graphs.IsTree(TestGraphs.Isolated(3)));

    [Fact]
    public void IsTreeFalseForInStar()
    {
        // Edges point toward the center, so there is no root with in-degree 0 except the leaves.
        Assert.False(Graphs.IsTree(TestGraphs.InStar(4)));
    }

    [Fact]
    public void IsPathTrueForEmptyGraph() => Assert.True(Graphs.IsPath(TestGraphs.Empty()));

    [Fact]
    public void IsPathTrueForSingleNode() => Assert.True(Graphs.IsPath(TestGraphs.Isolated(1)));

    [Fact]
    public void IsPathTrueForChain() => Assert.True(Graphs.IsPath(TestGraphs.Path(5)));

    [Fact]
    public void IsPathFalseForOutStar()
    {
        // Center has out-degree 4 > 1.
        Assert.False(Graphs.IsPath(TestGraphs.OutStar(5)));
    }

    [Fact]
    public void ShapeHierarchyConsistencyForCycle()
    {
        // A cycle is none of DAG/forest/tree/path.
        var g = TestGraphs.Cycle(3);
        Assert.False(Graphs.IsDag(g));
        Assert.False(Graphs.IsForest(g));
        Assert.False(Graphs.IsTree(g));
        Assert.False(Graphs.IsPath(g));
    }

    [Fact]
    public void ShapeHierarchyConsistencyForPath()
    {
        // A path is DAG, forest, tree, and path.
        var g = TestGraphs.Path(4);
        Assert.True(Graphs.IsDag(g));
        Assert.True(Graphs.IsForest(g));
        Assert.True(Graphs.IsTree(g));
        Assert.True(Graphs.IsPath(g));
    }

    // ---- Undirected shape checks ----

    [Fact]
    public void IsBipartiteTrueForEmptyGraph() => Assert.True(Graphs.IsBipartite(TestGraphs.Empty()));

    [Fact]
    public void IsBipartiteTrueForIsolatedNodes() => Assert.True(Graphs.IsBipartite(TestGraphs.Isolated(3)));

    [Fact]
    public void IsBipartiteTrueForPath() => Assert.True(Graphs.IsBipartite(TestGraphs.Path(5)));

    [Fact]
    public void IsBipartiteTrueForStars()
    {
        // A star is bipartite either way round: center in one class, leaves in the other.
        Assert.True(Graphs.IsBipartite(TestGraphs.OutStar(5)));
        Assert.True(Graphs.IsBipartite(TestGraphs.InStar(5)));
    }

    [Fact]
    public void IsBipartiteTrueForEvenCycle() => Assert.True(Graphs.IsBipartite(TestGraphs.Cycle(4)));

    [Fact]
    public void IsBipartiteFalseForOddCycle() => Assert.False(Graphs.IsBipartite(TestGraphs.Cycle(3)));

    [Fact]
    public void IsBipartiteTrueForReciprocalPair()
    {
        // 0->1 and 1->0 are one undirected edge, not a 2-cycle - and a 2-cycle would be even anyway.
        Assert.True(Graphs.IsBipartite(TestGraphs.Of(2, 0, 1, 1, 0)));
    }

    [Fact]
    public void IsBipartiteFalseForSymmetricallyStoredOddCycle()
    {
        // The triangle stored in both directions: collapsing the pairs still leaves an odd cycle.
        var g = TestGraphs.Of(3, 0, 1, 1, 0, 1, 2, 2, 1, 0, 2, 2, 0);
        Assert.False(Graphs.IsBipartite(g));
    }

    [Fact]
    public void IsBipartiteFalseForSelfLoop()
    {
        // The one place where a self-loop is not ignored: a node adjacent to itself has no class.
        Assert.False(Graphs.IsBipartite(TestGraphs.Of(1, 0, 0)));
        Assert.False(Graphs.IsBipartite(TestGraphs.Of(3, 0, 1, 1, 2, 2, 2)));
    }

    [Fact]
    public void IsUndirectedTreeTrueForEmptyGraph() => Assert.True(Graphs.IsUndirectedTree(TestGraphs.Empty()));

    [Fact]
    public void IsUndirectedTreeTrueForSingleNode() => Assert.True(Graphs.IsUndirectedTree(TestGraphs.Isolated(1)));

    [Fact]
    public void IsUndirectedTreeTrueForPath() => Assert.True(Graphs.IsUndirectedTree(TestGraphs.Path(5)));

    [Fact]
    public void IsUndirectedTreeTrueForInStar()
    {
        // Edges point toward the center, so this is no directed tree - undirected it is a star.
        var g = TestGraphs.InStar(4);
        Assert.False(Graphs.IsTree(g));
        Assert.True(Graphs.IsUndirectedTree(g));
    }

    [Fact]
    public void IsUndirectedTreeTrueForMixedDirections()
    {
        // 0->1 and 2->1: a DAG, but node 1 has two parents, so neither forest nor tree. Undirected
        // it is three nodes joined by two edges.
        var g = TestGraphs.Of(3, 0, 1, 2, 1);
        Assert.True(Graphs.IsDag(g));
        Assert.False(Graphs.IsForest(g));
        Assert.True(Graphs.IsUndirectedTree(g));
    }

    [Fact]
    public void IsUndirectedTreeTrueForSymmetricallyStoredPath()
    {
        // Every edge in both directions: no DAG at all, yet the same tree when read as undirected.
        var g = TestGraphs.Of(3, 0, 1, 1, 0, 1, 2, 2, 1);
        Assert.False(Graphs.IsDag(g));
        Assert.True(Graphs.IsUndirectedTree(g));
    }

    [Fact]
    public void IsUndirectedTreeTrueForReciprocalPair() => Assert.True(Graphs.IsUndirectedTree(TestGraphs.Of(2, 0, 1, 1, 0)));

    [Fact]
    public void IsUndirectedTreeIgnoresSelfLoops()
    {
        // A self-loop on every node of a path leaves the path a tree.
        var g = TestGraphs.Of(3, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2);
        Assert.True(Graphs.IsUndirectedTree(g));
    }

    [Fact]
    public void IsUndirectedTreeFalseForDisconnectedGraph()
    {
        Assert.False(Graphs.IsUndirectedTree(TestGraphs.Isolated(3)));
        Assert.False(Graphs.IsUndirectedTree(TestGraphs.Of(4, 0, 1, 2, 3)));
    }

    [Fact]
    public void IsUndirectedTreeFalseForCycle() => Assert.False(Graphs.IsUndirectedTree(TestGraphs.Cycle(3)));

    [Fact]
    public void IsUndirectedTreeFalseForSymmetricallyStoredCycle()
    {
        // The triangle in both directions. Every edge here is a reciprocal pair, so a check that
        // discounted pairs without asking whether it has seen this one before would count three
        // edges as two and report a tree.
        var g = TestGraphs.Of(3, 0, 1, 1, 0, 1, 2, 2, 1, 0, 2, 2, 0);
        Assert.False(Graphs.IsUndirectedTree(g));
        Assert.False(Graphs.IsUndirectedForest(g));
    }

    [Fact]
    public void IsUndirectedTreeTrueAtTheEdgeBound()
    {
        // A symmetrically stored path with a self-loop on every node holds exactly
        // 2 x (nodeCount - 1) + nodeCount = 3 x nodeCount - 2 edges: the most a tree can store, and
        // therefore the largest edge count the O(1) rejection must still let through.
        var g = TestGraphs.Of(4, 0, 0, 0, 1, 1, 0, 1, 1, 1, 2, 2, 1, 2, 2, 2, 3, 3, 2, 3, 3);
        Assert.Equal((3 * g.NodeCount) - 2, g.EdgeCount);
        Assert.True(Graphs.IsUndirectedTree(g));
    }

    [Fact]
    public void IsUndirectedTreeFalseForDiamond()
    {
        // Four edges, and undirected they close a cycle.
        Assert.False(Graphs.IsUndirectedTree(TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3)));
    }

    [Fact]
    public void IsUndirectedForestTrueForEmptyGraph() => Assert.True(Graphs.IsUndirectedForest(TestGraphs.Empty()));

    [Fact]
    public void IsUndirectedForestTrueForIsolatedNodes() => Assert.True(Graphs.IsUndirectedForest(TestGraphs.Isolated(3)));

    [Fact]
    public void IsUndirectedForestTrueForDisjointTrees() => Assert.True(Graphs.IsUndirectedForest(TestGraphs.Of(5, 0, 1, 0, 2, 3, 4)));

    [Fact]
    public void IsUndirectedForestTrueForInStar()
    {
        var g = TestGraphs.InStar(4);
        Assert.False(Graphs.IsForest(g));
        Assert.True(Graphs.IsUndirectedForest(g));
    }

    [Fact]
    public void IsUndirectedForestTrueForSelfLoopsOnly() => Assert.True(Graphs.IsUndirectedForest(TestGraphs.Of(2, 0, 0, 1, 1)));

    [Fact]
    public void IsUndirectedForestFalseForCycle() => Assert.False(Graphs.IsUndirectedForest(TestGraphs.Cycle(3)));

    [Fact]
    public void IsUndirectedForestFalseForDiamond() => Assert.False(Graphs.IsUndirectedForest(TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3)));

    [Fact]
    public void IsUndirectedForestFalseForCliqueRejectedByEdgeBound()
    {
        // 5 nodes, 20 edges - above 3 x nodeCount - 2, so this is decided without reading an edge.
        Assert.False(Graphs.IsUndirectedForest(TestGraphs.Clique(5)));
        Assert.False(Graphs.IsUndirectedTree(TestGraphs.Clique(5)));
    }

    [Fact]
    public void IsStronglyConnectedTrueForEmptyGraphAndSingleNode()
    {
        Assert.True(Graphs.IsStronglyConnected(TestGraphs.Empty()));
        Assert.True(Graphs.IsStronglyConnected(TestGraphs.Isolated(1)));
    }

    [Fact]
    public void IsStronglyConnectedOnCyclesAndPaths()
    {
        Assert.True(Graphs.IsStronglyConnected(TestGraphs.Cycle(5)));
        Assert.True(Graphs.IsStronglyConnected(TestGraphs.Clique(4)));
        Assert.False(Graphs.IsStronglyConnected(TestGraphs.Path(5)));
        Assert.False(Graphs.IsStronglyConnected(TestGraphs.Isolated(2)));
    }

    [Fact]
    public void IsStronglyConnectedFalseWhenNodeZeroReachesAllButNotBack()
    {
        // 0 <-> 1 and 1 -> 2: node 2 is reached, but closes a component of its own.
        Assert.False(Graphs.IsStronglyConnected(TestGraphs.Of(3, 0, 1, 1, 0, 1, 2)));
        // 1 -> 0 only: node 0 reaches nothing.
        Assert.False(Graphs.IsStronglyConnected(TestGraphs.Of(2, 1, 0)));
    }

    [Fact]
    public void IsStronglyConnectedOnDeepCycle()
    {
        Assert.True(Graphs.IsStronglyConnected(TestGraphs.Cycle(1_000_000)));
        Assert.False(Graphs.IsStronglyConnected(TestGraphs.Path(1_000_000)));
    }

    [Fact]
    public void IsWeaklyConnectedTrueForEmptyGraph() => Assert.True(Graphs.IsWeaklyConnected(TestGraphs.Empty()));

    [Fact]
    public void IsWeaklyConnectedTrueForSingleNode() => Assert.True(Graphs.IsWeaklyConnected(TestGraphs.Isolated(1)));

    [Fact]
    public void IsWeaklyConnectedTrueForPathAndStars()
    {
        Assert.True(Graphs.IsWeaklyConnected(TestGraphs.Path(5)));
        Assert.True(Graphs.IsWeaklyConnected(TestGraphs.OutStar(5)));
        Assert.True(Graphs.IsWeaklyConnected(TestGraphs.InStar(5)));
    }

    [Fact]
    public void IsWeaklyConnectedTrueForCycle() => Assert.True(Graphs.IsWeaklyConnected(TestGraphs.Cycle(3)));

    [Fact]
    public void IsWeaklyConnectedFalseForIsolatedNodes() => Assert.False(Graphs.IsWeaklyConnected(TestGraphs.Isolated(3)));

    [Fact]
    public void IsWeaklyConnectedFalseForTwoComponents() => Assert.False(Graphs.IsWeaklyConnected(TestGraphs.Of(4, 0, 1, 2, 3)));

    [Fact]
    public void IsWeaklyConnectedIgnoresSelfLoops()
    {
        // Self-loops connect nothing, so two nodes carrying one each stay two components.
        Assert.False(Graphs.IsWeaklyConnected(TestGraphs.Of(2, 0, 0, 1, 1)));
    }

    // ---- RootNodes ----

    [Fact]
    public void RootNodesReturnsAllSources()
    {
        var g = BiAdjacentGraph.Of(TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3));
        Assert.Equal([0], Graphs.RootNodes(g).ToArray());
    }

    [Fact]
    public void RootNodesOnIsolatedGraphReturnsAllNodes()
    {
        var g = BiAdjacentGraph.Of(TestGraphs.Isolated(3));
        Assert.Equal([0, 1, 2], Graphs.RootNodes(g).ToArray());
    }

    [Fact]
    public void RootNodesOnCycleIsEmpty()
    {
        var g = BiAdjacentGraph.Of(TestGraphs.Cycle(4));
        Assert.True(Graphs.RootNodes(g).IsEmpty);
    }

    [Fact]
    public void RootNodesOnEmptyGraphIsEmpty() => Assert.True(Graphs.RootNodes(BiAdjacentGraph.Of(TestGraphs.Empty())).IsEmpty);

    [Fact]
    public void RootNodesOfTheTransposeAreTheSinks()
    {
        // 0 -> 1, 0 -> 2, 1 -> 3, 2 -> 3, and 4 -> 3: the sink is 3 alone.
        var g = BiAdjacentGraph.Of(TestGraphs.Of(5, 0, 1, 0, 2, 1, 3, 2, 3, 4, 3));
        Assert.Equal([0, 4], Graphs.RootNodes(g).ToArray());
        Assert.Equal([3], Graphs.RootNodes(g.Transposed()).ToArray());
    }

    // ---- Remapped ----

    [Fact]
    public void RemappedRejectsAMappingToANodeOutsideTheGraph()
    {
        var g = TestGraphs.Path(2);
        // Two distinct values for two nodes: shaped like a total mapping, but 5 is not a node.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Graphs.Remapped(g, NodeMapping.Of([0, 5])));
        Assert.Contains("5", ex.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => Graphs.Remapped(TestGraphs.Path(3), NodeMapping.ForSubgraph(0, 9)));
    }

    [Fact]
    public void RemappedRejectsAHugeTargetWithoutAllocatingForIt()
    {
        var g = TestGraphs.Path(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => Graphs.Remapped(g, NodeMapping.Of([0, int.MaxValue])));
        Assert.Throws<ArgumentOutOfRangeException>(() => Graphs.Remapped(g, NodeMapping.ForSubgraph(0, int.MaxValue)));
    }

    [Fact]
    public void RemappedRejectsAnUnmappedNewNode()
    {
        var ex = Assert.Throws<ArgumentException>(() => Graphs.Remapped(TestGraphs.Path(3), NodeMapping.Of([0, -1, 1])));
        Assert.Contains("unmapped", ex.Message);
    }

    [Fact]
    public void RemappedRejectsTwoNewNodesForOneOriginalNode()
    {
        Assert.Throws<ArgumentException>(() => Graphs.Remapped(TestGraphs.Path(3), NodeMapping.Of([0, 2, 0])));
    }

    [Fact]
    public void RemappedWithIdentityYieldsIsomorphicGraph()
    {
        var g = TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3);
        var remapped = Graphs.Remapped(g, NodeMapping.ForPermutation([0, 1, 2, 3]));
        Assert.Equal(g, remapped);
    }

    [Fact]
    public void RemappedWithPermutationPreservesEdgeCount()
    {
        var g = TestGraphs.Path(5);
        var remapped = Graphs.Remapped(g, NodeMapping.ForPermutation([4, 3, 2, 1, 0]));
        Assert.Equal(g.NodeCount, remapped.NodeCount);
        Assert.Equal(g.EdgeCount, remapped.EdgeCount);
    }

    [Fact]
    public void RemappedWithReversePermutationReversesPath()
    {
        // Original path: 0 -> 1 -> 2. After permutation {2, 1, 0}, new node 0 is old node 2 etc.
        var g = TestGraphs.Path(3);
        var remapped = Graphs.Remapped(g, NodeMapping.ForPermutation([2, 1, 0]));
        // New edges: old 0->1 becomes new 2->1, old 1->2 becomes new 1->0.
        Assert.Equal([0], remapped.OutNodes(1).ToArray());
        Assert.Equal([1], remapped.OutNodes(2).ToArray());
        Assert.Empty(remapped.OutNodes(0).ToArray());
    }

    [Fact]
    public void RemappedWithSubgraphDropsEdgesToExcludedNodes()
    {
        // Original: 0 -> 1, 1 -> 2, 2 -> 3. Select {0, 1, 3}: 1->2 is dropped, and so is 2->3.
        var g = TestGraphs.Path(4);
        var selected = new BitArray(4);
        selected[0] = true;
        selected[1] = true;
        selected[3] = true;
        var sub = Graphs.Remapped(g, NodeMapping.ForSubgraph(selected));
        Assert.Equal(3, sub.NodeCount);
        // Only edge is the old 0->1, mapped to new 0->1.
        Assert.Equal(1, sub.EdgeCount);
        Assert.Equal([1], sub.OutNodes(0).ToArray());
    }

    [Fact]
    public void RemappedSubgraphWithTargetsBeyondSelectionRange()
    {
        // Path 0 -> 1 -> 2 -> 3. Select {0, 1}: edge 1 -> 2 has a target outside the selection and
        // must be dropped.
        var g = TestGraphs.Path(4);
        var selected = new BitArray(4);
        selected[0] = true;
        selected[1] = true;
        var sub = Graphs.Remapped(g, NodeMapping.ForSubgraph(selected));
        Assert.Equal(2, sub.NodeCount);
        Assert.Equal(1, sub.EdgeCount);
        Assert.Equal([1], sub.OutNodes(0).ToArray());
        Assert.Empty(sub.OutNodes(1).ToArray());
    }

    [Fact]
    public void RemappedEmptyMappingReturnsEmptyGraph()
    {
        var g = TestGraphs.Path(3);
        var sub = Graphs.Remapped(g, NodeMapping.ForSubgraph(new BitArray(0)));
        Assert.Equal(0, sub.NodeCount);
        Assert.Equal(0, sub.EdgeCount);
    }

    [Fact]
    public void RemappedPreservesSortedAdjacencyLists()
    {
        // Original: 0 -> 1, 0 -> 2, 0 -> 3. Permutation {3, 1, 2, 0}: old node 0 becomes new node 3,
        // and its out-nodes (1, 2, 3) map to new (1, 2, 0) - sorted (0, 1, 2).
        var g = TestGraphs.OutStar(4);
        var remapped = Graphs.Remapped(g, NodeMapping.ForPermutation([3, 1, 2, 0]));
        Assert.Equal([0, 1, 2], remapped.OutNodes(3).ToArray());
    }

    // ---- EdgeMapping ----

    [Fact]
    public void EdgeMappingOfReversedPath()
    {
        var g = TestGraphs.Path(3);
        var nm = NodeMapping.ForPermutation([2, 1, 0]);
        var newGraph = Graphs.Remapped(g, nm);
        var em = Graphs.EdgeMapping(newGraph, g, nm);
        Assert.Equal(newGraph.EdgeCount, em.MappedCount);
        // Every new edge index must map to a valid old edge index.
        for (int i = 0; i < em.MappedCount; i++)
        {
            Assert.InRange(em.Get(i), 0, g.EdgeCount - 1);
        }
    }

    [Fact]
    public void EdgeMappingRecordsHolesForEdgesNotInOldGraph()
    {
        // newGraph has a self-loop 0->0 that oldGraph (a 2-node path) lacks: -1 for it, partial, and
        // no valid entry.
        var oldGraph = TestGraphs.Path(2);
        var newGraph = TestGraphs.Of(2, 0, 0);
        var em = Graphs.EdgeMapping(newGraph, oldGraph, NodeMapping.ForPermutation([0, 1]));
        Assert.Equal(-1, em.Get(0));
        Assert.Equal(0, em.MappedCount);
        Assert.True(em.IsPartial);
    }

    [Fact]
    public void EdgeMappingOfEmptyGraphHasZeroMappedCount()
    {
        var g = TestGraphs.Empty();
        var em = Graphs.EdgeMapping(g, g, NodeMapping.Of([]));
        Assert.Equal(0, em.MappedCount);
    }

    [Fact]
    public void EdgeMappingMapsTheEdgesOfANewNodeWithoutOldCounterpartToMinusOne()
    {
        // New node 2 has no old counterpart: its edge 2->0 has none either, just like 0->2.
        var oldGraph = TestGraphs.Path(2);
        var newGraph = TestGraphs.Of(3, 0, 1, 0, 2, 2, 0);
        var em = Graphs.EdgeMapping(newGraph, oldGraph, NodeMapping.Of([0, 1, -1]));
        Assert.Equal([0, -1, -1], Enumerable.Range(0, newGraph.EdgeCount).Select(em.Get).ToArray());
        Assert.Equal(1, em.MappedCount);
        Assert.True(em.IsPartial);
    }

    [Fact]
    public void EdgeMappingFromAGraphOntoItsSubgraphMapsTheEdgesOfRemovedNodesToMinusOne()
    {
        // Node 2 is removed and has edges both ways: 1->2 loses its target, 2->3 its source.
        var graph = TestGraphs.Of(4, 0, 1, 1, 2, 1, 3, 2, 3, 3, 0);
        var keep = NodeMapping.ForSubgraph(0, 1, 3);
        var sub = Graphs.Remapped(graph, keep);
        var em = Graphs.EdgeMapping(graph, sub, keep.Inverted(graph.NodeCount));
        Assert.Equal([0, -1, 1, -1, 2], Enumerable.Range(0, graph.EdgeCount).Select(em.Get).ToArray());
        for (int e = 0; e < graph.EdgeCount; e++)
        {
            int s = em.Get(e);
            if (s >= 0)
            {
                Assert.Equal(graph.EdgeSource(e), keep.Get(sub.EdgeSource(s)));
                Assert.Equal(graph.EdgeTarget(e), keep.Get(sub.EdgeTarget(s)));
            }
        }
    }

    [Fact]
    public void EdgeMappingRejectsAMappingToANodeOutsideTheOldGraph()
    {
        var oldGraph = TestGraphs.Path(2);
        // As the source of an edge.
        var asSource = Assert.Throws<ArgumentOutOfRangeException>(
            () => Graphs.EdgeMapping(TestGraphs.Of(3, 2, 0), oldGraph, NodeMapping.Of([0, 1, 5])));
        Assert.Contains("new node 2 maps to 5", asSource.Message);
        // As the target of an edge only: 2 is oldGraph.NodeCount.
        var asTarget = Assert.Throws<ArgumentOutOfRangeException>(
            () => Graphs.EdgeMapping(TestGraphs.Of(3, 0, 2), oldGraph, NodeMapping.Of([0, 1, 2])));
        Assert.Contains("new node 2 maps to 2", asTarget.Message);
    }

    [Fact]
    public void EdgeMappingRejectsAMappingShorterThanTheNewGraph()
    {
        var g = TestGraphs.Path(3);
        var ex = Assert.Throws<ArgumentException>(() => Graphs.EdgeMapping(g, g, NodeMapping.Of([0, 1])));
        Assert.Contains("3 nodes", ex.Message);
    }

    [Fact]
    public void EdgeMappingRejectsAMappingLongerThanTheNewGraph()
    {
        // Graph and subgraph swapped: the inverse of keep is over the 4 nodes of graph, not over the
        // 3 of sub, and would otherwise give a plausible but wrong mapping.
        var graph = TestGraphs.Of(4, 0, 1, 1, 2, 2, 3, 3, 0);
        var keep = NodeMapping.ForSubgraph(1, 2, 3);
        var sub = Graphs.Remapped(graph, keep);
        var ex = Assert.Throws<ArgumentException>(() => Graphs.EdgeMapping(sub, graph, keep.Inverted(graph.NodeCount)));
        Assert.Contains("index count of 4", ex.Message);
        Assert.Contains("3 nodes", ex.Message);
    }

    // ---- TransposedEdgeMapping ----

    [Fact]
    public void TransposedEdgeMappingRelabelsEdges()
    {
        // 0->2 is edge 0, 1->0 is edge 1. In the transpose 0->1 comes first, so the two swap.
        var g = TestGraphs.Of(3, 1, 0, 0, 2);
        var em = Graphs.TransposedEdgeMapping(g);
        Assert.Equal(1, em.Get(0));
        Assert.Equal(0, em.Get(1));
    }

    [Fact]
    public void TransposedEdgeMappingIsTotal()
    {
        var g = TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3, 3, 3);
        var em = Graphs.TransposedEdgeMapping(g);
        Assert.Equal(g.EdgeCount, em.MappedCount);
        Assert.False(em.IsPartial);
    }

    [Fact]
    public void TransposedEdgeMappingIdentifiesTheSwappedEdge()
    {
        var g = TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3);
        var transposed = BiAdjacentGraph.Of(g).Transposed();
        var em = Graphs.TransposedEdgeMapping(g);
        for (int q = 0; q < transposed.EdgeCount; q++)
        {
            int p = em.Get(q);
            Assert.Equal(transposed.EdgeTarget(q), g.EdgeSource(p));
            Assert.Equal(transposed.EdgeSource(q), g.EdgeTarget(p));
        }
    }

    [Fact]
    public void TransposedEdgeMappingInverseEqualsTransposedEdgeIndex()
    {
        var g = TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3, 0, 0);
        var inverse = Graphs.TransposedEdgeMapping(g).Inverted(g.EdgeCount);
        var biAdjacent = BiAdjacentGraph.Of(g);
        for (int p = 0; p < g.EdgeCount; p++)
        {
            Assert.Equal(biAdjacent.TransposedEdgeIndex(p), inverse.Get(p));
        }
    }

    [Fact]
    public void TransposedEdgeMappingAgreesOnUniAndBiAdjacentInput()
    {
        var g = TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3);
        var fromUniAdjacent = Graphs.TransposedEdgeMapping(g);
        var fromBiAdjacent = Graphs.TransposedEdgeMapping(BiAdjacentGraph.Of(g));
        for (int q = 0; q < g.EdgeCount; q++)
        {
            Assert.Equal(fromUniAdjacent.Get(q), fromBiAdjacent.Get(q));
        }
    }

    [Fact]
    public void TransposedEdgeMappingOfEmptyGraphHasZeroMappedCount()
    {
        var em = Graphs.TransposedEdgeMapping(TestGraphs.Empty());
        Assert.Equal(0, em.MappedCount);
        Assert.False(em.IsPartial);
    }

    [Fact]
    public void TransposedEdgeMappingOfIsolatedNodesHasZeroMappedCount() =>
        Assert.Equal(0, Graphs.TransposedEdgeMapping(TestGraphs.Isolated(5)).MappedCount);

    // ---- EdgePartition ----

    [Fact]
    public void EdgePartitionOmitsIntraGroupEdges()
    {
        // Graph: 0->1 (intra), 0->2 (inter), 1->2 (inter). Partition: {0,1}/{2}.
        var g = TestGraphs.Of(3, 0, 1, 0, 2, 1, 2);
        var partition = new NodePartition(g, [0, 0, 1], [0, 1, 2], [0, 2, 3]);
        var condensed = partition.Condense();
        var ep = Graphs.EdgePartition(condensed, partition);
        Assert.Equal(g.EdgeCount, ep.EdgeCount);
        Assert.Equal(-1, ep.Get(0));
        Assert.Equal(0, ep.Get(1));
        Assert.Equal(0, ep.Get(2));
    }

    [Fact]
    public void EdgePartitionGroupsMembersSortedAscending()
    {
        // 0->2, 1->2, 0->3: all cross-group {0,1}/{2}/{3}. Two condensed edges: 0->1 from {0,1}->{2},
        // 0->2 from {0,1}->{3}.
        var g = TestGraphs.Of(4, 0, 2, 0, 3, 1, 2);
        var partition = new NodePartition(g, [0, 0, 1, 2], [0, 1, 2, 3], [0, 2, 3, 4]);
        var condensed = partition.Condense();
        var ep = Graphs.EdgePartition(condensed, partition);
        // The condensed edge to group 1 ({2}) collects 0->2 (edge 0) and 1->2 (edge 2).
        Assert.Equal([0, 2], ep.EdgesOf(0).ToArray());
        Assert.Equal([1], ep.EdgesOf(1).ToArray());
    }

    [Fact]
    public void EdgePartitionRejectsACondensedGraphWithAnotherNodeCountThanGroups()
    {
        var sccs = Components.SccsOf(TestGraphs.Path(4));
        var ex = Assert.Throws<ArgumentException>(() => Graphs.EdgePartition(TestGraphs.Isolated(1), sccs));
        Assert.Contains("4 groups", ex.Message);
        Assert.Throws<ArgumentException>(() => Graphs.EdgePartition(TestGraphs.Isolated(5), sccs));
    }

    [Fact]
    public void AdjacencyListOfListsEveryNode()
    {
        Assert.Equal("0 -> [1, 2]\n1 -> []\n2 -> [0]\n", Graphs.AdjacencyListOf(TestGraphs.Of(3, 0, 1, 0, 2, 2, 0)));
        Assert.Empty(Graphs.AdjacencyListOf(TestGraphs.Empty()));
        Assert.Equal(40, Graphs.AdjacencyListOf(TestGraphs.Isolated(40)).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void AdjacencyListOfReservesAtMostTheLongestString()
    {
        // The cap on the builder's capacity: a string of one character more is never created. The
        // runtime refuses it with OutOfMemoryException on x64 and with OverflowException on Arm64.
        Exception? refused = Record.Exception(() => new string('x', Graphs.MaxStringLength + 1));
        Assert.True(refused is OutOfMemoryException or OverflowException, $"refused with {refused}");
    }

    [Fact]
    public void AdjacencyListOfABiAdjacentGraphListsItsOutAdjacency()
    {
        var bi = BiAdjacentGraph.Of(TestGraphs.Of(3, 0, 1, 0, 2, 2, 0));
        Assert.Equal("0 -> [2]\n1 -> [0]\n2 -> [0]\n", Graphs.AdjacencyListOf(bi.Transposed()));
    }

    // ---- FindCycle ----

    private static void AssertIsCycle(Graph graph, ImmutableArray<int> cycle)
    {
        Assert.False(cycle.IsEmpty);
        for (int k = 0; k < cycle.Length; k++)
        {
            int from = cycle[k];
            int to = cycle[(k + 1) % cycle.Length];
            Assert.True(graph.HasOutNode(from, to), $"edge {from} -> {to} of [{string.Join(", ", cycle)}]");
        }
        Assert.Equal(cycle.Length, cycle.Distinct().Count());
    }

    [Fact]
    public void FindCycleOnADagIsEmpty()
    {
        Assert.Empty(Graphs.FindCycle(TestGraphs.Path(5)).ToArray());
        Assert.Empty(Graphs.FindCycle(TestGraphs.Empty()).ToArray());
    }

    [Fact]
    public void FindCycleReturnsTheCycleClosedByTheFirstBackEdge()
    {
        // 0 -> 1 -> 2 -> 3, and 3 -> 0, 3 -> 1 back: 3's out-nodes are visited in ascending order,
        // so 3 -> 0 closes the first cycle.
        Graph graph = TestGraphs.Of(4, 0, 1, 1, 2, 2, 3, 3, 1, 3, 0);
        Assert.Equal([0, 1, 2, 3], Graphs.FindCycle(graph).ToArray());
    }

    [Fact]
    public void FindCycleStartsAtTheTargetOfTheClosingEdgeUnderALaterRoot()
    {
        // Root 0 is acyclic (0 -> 1); from root 2: 2 -> 3 -> 4 -> 5 -> 3 closes at 3, mid-stack.
        Graph graph = TestGraphs.Of(6, 0, 1, 2, 3, 3, 4, 4, 5, 5, 3);
        Assert.Equal([3, 4, 5], Graphs.FindCycle(graph).ToArray());
        Assert.Equal([3, 4, 5], Graphs.FindCycle(BiAdjacentGraph.Of(graph)).ToArray());
    }

    [Fact]
    public void FindCycleFindsASelfLoop() => Assert.Equal([1], Graphs.FindCycle(TestGraphs.Of(3, 0, 1, 1, 1)).ToArray());

    [Fact]
    public void FindCycleAgreesWithIsDagOnRandomGraphs()
    {
        for (long seed = 0; seed < 300; seed++)
        {
            var labeled = RandomGraphs.AnyGraph(seed);
            Graph graph = labeled.Graph;
            var cycle = Graphs.FindCycle(graph);
            Assert.True(cycle.IsEmpty == Graphs.IsDag(graph), labeled.Describe());
            if (!cycle.IsEmpty)
            {
                AssertIsCycle(graph, cycle);
            }
        }
    }

    [Fact]
    public void FindCycleOnADeepCycleDoesNotOverflowTheStack()
    {
        const int n = 1_000_000;
        var cycle = Graphs.FindCycle(TestGraphs.Cycle(n));
        Assert.Equal(n, cycle.Length);
        Assert.Equal(0, cycle[0]);
        Assert.Equal(n - 1, cycle[n - 1]);
    }

    // ---- Transitive closure and reduction ----

    /// <summary>Whether <c>v</c> is reachable from <c>u</c> by a non-empty path, for every pair.</summary>
    private static bool[][] Reachability(Graph graph)
    {
        int n = graph.NodeCount;
        var reach = new bool[n][];
        for (int u = 0; u < n; u++)
        {
            reach[u] = new bool[n];
            var queue = new List<int>();
            foreach (int t in graph.OutNodes(u))
            {
                if (!reach[u][t])
                {
                    reach[u][t] = true;
                    queue.Add(t);
                }
            }
            for (int i = 0; i < queue.Count; i++)
            {
                foreach (int t in graph.OutNodes(queue[i]))
                {
                    if (!reach[u][t])
                    {
                        reach[u][t] = true;
                        queue.Add(t);
                    }
                }
            }
        }
        return reach;
    }

    [Fact]
    public void TransitiveClosureMatchesReachability()
    {
        for (long seed = 0; seed < 120; seed++)
        {
            Graph graph = RandomGraphs.Gnp(2 + (int)(seed % 40), 0.06, seed);
            bool[][] expected = Reachability(graph);
            Graph closure = Graphs.TransitiveClosure(graph);
            Assert.Equal(graph.NodeCount, closure.NodeCount);
            for (int u = 0; u < graph.NodeCount; u++)
            {
                for (int v = 0; v < graph.NodeCount; v++)
                {
                    Assert.True(closure.HasOutNode(u, v) == expected[u][v], $"{u} -> {v}, seed {seed}");
                }
            }
        }
    }

    [Fact]
    public void TransitiveClosureHasASelfLoopExactlyOnCycles()
    {
        // 0 -> 1 -> 2 -> 1, 3 -> 3, 4 alone.
        Graph closure = Graphs.TransitiveClosure(TestGraphs.Of(5, 0, 1, 1, 2, 2, 1, 3, 3));
        Assert.False(closure.HasOutNode(0, 0));
        Assert.True(closure.HasOutNode(1, 1));
        Assert.True(closure.HasOutNode(2, 2));
        Assert.True(closure.HasOutNode(3, 3));
        Assert.Equal(0, closure.OutDegree(4));
        Assert.Equal([1, 2], closure.OutNodes(0).ToArray());
    }

    [Fact]
    public void TransitiveClosureOfAnEmptyGraph() => Assert.Equal(0, Graphs.TransitiveClosure(TestGraphs.Empty()).NodeCount);

    [Fact]
    public void TransitiveReductionKeepsReachabilityWithoutRedundantEdges()
    {
        for (long seed = 0; seed < 120; seed++)
        {
            // Renumbered, so that ascending IDs are not already a topological order.
            Graph sorted = RandomGraphs.RandomDag(2 + (int)(seed % 40), 0.15, seed);
            Graph dag = Graphs.Remapped(sorted, NodeMapping.ForPermutation(Shuffled(sorted.NodeCount, seed)));
            Graph reduced = Graphs.TransitiveReduction(dag);
            bool[][] expected = Reachability(dag);
            bool[][] actual = Reachability(reduced);
            for (int u = 0; u < dag.NodeCount; u++)
            {
                Assert.True(expected[u].SequenceEqual(actual[u]), $"reachability from {u}, seed {seed}");
            }
            // Every kept edge is an edge of the DAG, and none is implied by the other kept edges.
            long currentSeed = seed;
            reduced.ForEachEdge((source, target, edgeIndex) =>
            {
                Assert.True(dag.HasOutNode(source, target));
                var without = new GraphBuilder();
                without.AddNodes(reduced.NodeCount);
                reduced.ForEachEdge((s, t, e) =>
                {
                    if (e != edgeIndex)
                    {
                        without.AddEdge(s, t);
                    }
                });
                Assert.False(Reachability(without.Build())[source][target], $"{source} -> {target} is implied, seed {currentSeed}");
            });
        }
    }

    private static int[] Shuffled(int n, long seed)
    {
        int[] order = [.. Enumerable.Range(0, n)];
        var random = new JavaRandom(seed);
        for (int i = n - 1; i > 0; i--)
        {
            int j = random.NextInt(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        return order;
    }

    [Fact]
    public void TransitiveClosureAndReductionAcrossSeveralBitWords()
    {
        for (long seed = 0; seed < 6; seed++)
        {
            Graph graph = RandomGraphs.Gnp(150, 0.012, seed);
            bool[][] expected = Reachability(graph);
            Graph closure = Graphs.TransitiveClosure(graph);
            for (int u = 0; u < 150; u++)
            {
                for (int v = 0; v < 150; v++)
                {
                    Assert.True(closure.HasOutNode(u, v) == expected[u][v], $"{u} -> {v}, seed {seed}");
                }
            }
            Graph sorted = RandomGraphs.RandomDag(150, 0.03, seed);
            Graph dag = Graphs.Remapped(sorted, NodeMapping.ForPermutation(Shuffled(150, seed)));
            Graph reduced = Graphs.TransitiveReduction(dag);
            bool[][] dagReach = Reachability(dag);
            bool[][] reducedReach = Reachability(reduced);
            for (int u = 0; u < 150; u++)
            {
                Assert.True(dagReach[u].SequenceEqual(reducedReach[u]), $"from {u}, seed {seed}");
            }
            Assert.Equal(reduced, Graphs.TransitiveReduction(Graphs.TransitiveClosure(reduced)));
        }
    }

    [Fact]
    public void TransitiveClosureRejectsMoreEdgesThanAnArrayHolds()
    {
        // One cycle through 46341 nodes: every node reaches every node, and 46341^2 edges exceed the
        // largest array (Array.MaxLength = 2^31 - 57 here, Java: 2^31 - 9).
        var ex = Assert.Throws<ArgumentException>(() => Graphs.TransitiveClosure(TestGraphs.Cycle(46_341)));
        Assert.StartsWith($"the transitive closure has more than {ArrayLimits.MaxArrayLength} edges, the largest array length", ex.Message);
    }

    [Fact]
    public void TransitiveReductionOfATransitiveTriangle()
    {
        Graph reduced = Graphs.TransitiveReduction(TestGraphs.Of(3, 0, 1, 1, 2, 0, 2));
        Assert.Equal(TestGraphs.Of(3, 0, 1, 1, 2), reduced);
    }

    [Fact]
    public void TransitiveReductionOfTheClosureOfAPathIsThePath()
    {
        Graph path = TestGraphs.Path(30);
        Assert.Equal(path, Graphs.TransitiveReduction(Graphs.TransitiveClosure(path)));
    }

    [Fact]
    public void TransitiveReductionRejectsCycles()
    {
        Assert.Equal("dag", Assert.Throws<ArgumentException>(() => Graphs.TransitiveReduction(TestGraphs.Cycle(3))).ParamName);
        Assert.Throws<ArgumentException>(() => Graphs.TransitiveReduction(TestGraphs.Of(2, 0, 1, 1, 1)));
    }
}
