namespace Graphity.Core.Tests;

/// <summary>
/// Regression guard for the iterative DFS cores: on very deep graphs (DFS depth 10^6) no algorithm
/// may overflow the call stack. .NET's default main-thread stack is 1 MB (8 MB on Linux), far too
/// small for a recursive search of this depth.
/// </summary>
public sealed class DeepGraphStackSafetyTests
{
    private const int Deep = 1_000_000;

    [Fact]
    public void DeepPathDoesNotOverflowStack()
    {
        // 0 -> 1 -> ... -> Deep-1: a single chain, DFS descends Deep levels deep.
        var path = TestGraphs.Path(Deep);

        Assert.Equal(Deep, Components.SccsOf(path).SccCount);
        Assert.Equal(Deep, TopOrder.Of(path).Length);
        Assert.Empty(BackEdges.Of(path).SourceNodes);
        Assert.Equal(EdgeType.Tree, EdgeTypes.Of(path).TypeOf(Deep - 2));
        Assert.True(Graphs.IsDag(path));
        Assert.Equal(Deep - 1, NodeMapping.Reindex(path).Get(Deep - 1));
    }

    [Fact]
    public void DeepAncestryDoesNotOverflowStack()
    {
        // A chain of Deep nodes is one heavy path; construction and queries must stay iterative.
        var path = TestGraphs.Path(Deep);

        var ancestry = Ancestry.Of(path);
        Assert.Equal(Deep - 1, ancestry.DepthOf(Deep - 1));
        Assert.Equal(Deep / 2, ancestry.LowestCommonAncestorOf(Deep - 1, Deep / 2));
        Assert.True(ancestry.IsAncestorOf(0, Deep - 1));

        var preorder = PreorderAncestry.Of(ancestry);
        Assert.Equal(Deep, preorder.SubtreeSizeOf(0));
        Assert.Equal(Deep - 1, preorder.PreorderIndexOf(Deep - 1));
        Assert.True(preorder.IsAncestorOf(Deep / 2, Deep - 1));
    }

    [Fact]
    public void DeepDominatorsDoNotOverflowStack()
    {
        // A chain makes both the search and the dominator tree Deep levels deep, and path
        // compression walks the full depth.
        var path = BiAdjacentGraph.Of(TestGraphs.Path(Deep));

        var dominators = Dominators.Of(path, 0);
        Assert.Equal(Deep - 2, dominators.ImmediateDominatorOf(Deep - 1));
        Assert.Equal(Deep - 1, Graphs.DominatorTree(dominators).EdgeCount);
        Assert.True(Ancestry.Of(dominators).IsAncestorOf(0, Deep - 1));

        // Back edges from the end of the chain to every 1000th node force long compressions.
        var builder = new GraphBuilder();
        builder.AddNodes(Deep);
        for (int i = 0; i + 1 < Deep; i++)
        {
            builder.AddEdge(i, i + 1);
        }
        for (int i = 0; i < Deep; i += 1000)
        {
            builder.AddEdge(Deep - 1, i);
        }
        var looped = Dominators.Of(BiAdjacentGraph.Of(builder.Build()), 0);
        Assert.Equal(Deep - 2, looped.ImmediateDominatorOf(Deep - 1));
    }

    [Fact]
    public void DeepBiconnectivityDoesNotOverflowStack()
    {
        // Every edge of a path is a bridge and its own block, so the traversal descends Deep levels
        // before the first block closes.
        var path = TestGraphs.Path(Deep);

        var biconnectivity = Components.BiconnectivityOf(BiAdjacentGraph.Of(path));
        Assert.Equal(Deep - 1, biconnectivity.Bridges.Count);
        Assert.Equal(Deep - 2, biconnectivity.ArticulationPoints.Count);
        Assert.Equal(Deep - 1, biconnectivity.Blocks.GroupCount);
    }

    [Fact]
    public void DeepStrongConnectivityDoesNotOverflowStack()
    {
        // One cycle of Deep nodes: both dominator searches descend Deep levels, and every edge and
        // node is critical.
        var cycle = TestGraphs.Cycle(Deep);

        var strong = Components.StrongConnectivityOf(BiAdjacentGraph.Of(cycle));
        Assert.Equal(Deep, strong.StrongBridges.Count);
        Assert.Equal(Deep, strong.StrongArticulationPoints.Count);
    }

    [Fact]
    public void DeepCycleDoesNotOverflowStack()
    {
        // One giant SCC: DFS descends Deep levels, then a single back edge closes the cycle.
        // Stresses the deep Tarjan stack pop and the deep on-stack unwind.
        var cycle = TestGraphs.Cycle(Deep);

        var sccs = Components.SccsOf(cycle);
        Assert.Equal(1, sccs.GroupCount);
        Assert.True(sccs.IsStronglyConnected);

        Assert.Single(BackEdges.Of(cycle).SourceNodes);
        Assert.Equal(EdgeType.Back, EdgeTypes.Of(cycle).TypeOf(Deep - 1));
        Assert.Equal(Deep, NodeMapping.Reindex(cycle).IndexCount);

        Assert.False(Graphs.IsDag(cycle));

        // A cycle has no topological order: the documented ArgumentException, never a stack overflow.
        Assert.Throws<ArgumentException>(() => TopOrder.Of(cycle));
    }
}
