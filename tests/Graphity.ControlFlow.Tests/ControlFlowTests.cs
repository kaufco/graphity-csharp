namespace Graphity.ControlFlow.Tests;

public sealed class ControlFlowTests
{
    private static BiAdjacentGraph Bi(int n, params ReadOnlySpan<int> edges) => BiAdjacentGraph.Of(TestGraphs.Of(n, edges));

    [Fact]
    public void EmptyGraph()
    {
        var d = Dominators.Of(BiAdjacentGraph.Of(TestGraphs.Empty()), []);
        Assert.Equal(0, ControlFlow.DominanceFrontiers(d).NodeCount);
    }

    [Fact]
    public void DominanceFrontiersOfALoop()
    {
        // 0 -> 1 -> 2 -> 1, 2 -> 3: the loop header is in its own frontier and in its body's.
        var d = Dominators.Of(Bi(4, 0, 1, 1, 2, 2, 1, 2, 3), 0);
        var df = ControlFlow.DominanceFrontiers(d);
        Assert.Empty(df.OutNodes(0).ToArray());
        Assert.Equal([1], df.OutNodes(1).ToArray());
        Assert.Equal([1], df.OutNodes(2).ToArray());
        Assert.Empty(df.OutNodes(3).ToArray());
    }

    [Fact]
    public void DominanceFrontiersOfADiamond()
    {
        var d = Dominators.Of(Bi(4, 0, 1, 0, 2, 1, 3, 2, 3), 0);
        var df = ControlFlow.DominanceFrontiers(d);
        Assert.Equal([3], df.OutNodes(1).ToArray());
        Assert.Equal([3], df.OutNodes(2).ToArray());
        Assert.Equal(2, df.EdgeCount);
    }

    [Fact]
    public void UnreachableNodesHaveEmptyFrontiersAndAppearInNone()
    {
        // 3 is unreachable and has an edge into the join point 2.
        var d = Dominators.Of(Bi(4, 0, 1, 1, 2, 0, 2, 3, 2), 0);
        var df = ControlFlow.DominanceFrontiers(d);
        Assert.Empty(df.OutNodes(3).ToArray());
        Assert.Equal([2], df.OutNodes(1).ToArray());
        Assert.Equal(1, df.EdgeCount);
    }

    [Fact]
    public void LadderFrontierGrowsQuadratically()
    {
        const int rungs = 50;
        var df = ControlFlow.DominanceFrontiers(
            Dominators.Of(BiAdjacentGraph.Of(ControlFlowGraphs.Ladder(rungs)), 0));
        // b_i (= rungs + i) is in the frontier of every a_j with 1 <= j <= i, plus the exit.
        Assert.True(df.EdgeCount > rungs * (rungs - 1) / 2);
    }

    [Fact]
    public void ControlDependenceIsTheTransposedFrontierOfThePostDominators()
    {
        // if (0) { 1 } ; 2   -  0 -> 1, 0 -> 2, 1 -> 2, exit 2.
        var cfg = Bi(3, 0, 1, 0, 2, 1, 2);
        var postDominators = Dominators.Of(cfg.Transposed(), 2);
        var controlDependence = BiAdjacentGraph.Of(ControlFlow.DominanceFrontiers(postDominators)).Transposed();
        Assert.Equal(1, controlDependence.EdgeCount);
        Assert.True(controlDependence.HasOutNode(0, 1));
    }

    [Fact]
    public void NullDominatorsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ControlFlow.DominanceFrontiers(null!));
    }

    [Fact]
    public void ThePairLimitIsTheLargestDotNetArray()
    {
        // DominanceFrontiers documents more than 2^31 - 57 pairs as rejected (Java: 2^31 - 9), the
        // core's array limit, which the module repeats.
        Assert.Equal(ControlFlow.MaxArrayLength, Array.MaxLength);
        Assert.True(ControlFlow.MaxArrayLength == int.MaxValue - 56);
    }

    [Fact]
    public void DeepChainDoesNotOverflowStack()
    {
        const int deep = 1_000_000;
        var dominators = Dominators.Of(BiAdjacentGraph.Of(TestGraphs.Path(deep)), 0);
        Assert.Equal(0, ControlFlow.DominanceFrontiers(dominators).EdgeCount);

        // One back edge from the end of the chain to its middle h: h is in the frontier of every
        // node from h to the end.
        var builder = new GraphBuilder();
        builder.AddNodes(deep);
        for (int i = 0; i + 1 < deep; i++)
        {
            builder.AddEdge(i, i + 1);
        }
        builder.AddEdge(deep - 1, deep / 2);
        var looped = Dominators.Of(BiAdjacentGraph.Of(builder.Build()), 0);
        var df = ControlFlow.DominanceFrontiers(looped);
        Assert.Equal([deep / 2], df.OutNodes(deep - 1).ToArray());
        Assert.Equal(deep - (deep / 2), df.EdgeCount);
    }
}
