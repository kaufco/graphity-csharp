namespace Graphity.ControlFlow.Tests;

public sealed class IteratedDominanceFrontierTests
{
    private static IteratedDominanceFrontier Of(Graph graph) =>
        IteratedDominanceFrontier.Of(Dominators.Of(BiAdjacentGraph.Of(graph), 0));

    [Fact]
    public void DefinitionInOneBranchOfADiamondNeedsAPhiAtTheJoin()
    {
        var idf = Of(TestGraphs.Of(4, 0, 1, 0, 2, 1, 3, 2, 3));
        Assert.Equal([3], idf.FrontierOf([1]).ToArray());
        Assert.Equal([3], idf.FrontierOf([1, 2]).ToArray());
        Assert.Empty(idf.FrontierOf([0]).ToArray());
    }

    [Fact]
    public void DefinitionInALoopBodyNeedsAPhiAtTheHeader()
    {
        // 0 -> 1 -> 2 -> 1, 2 -> 3
        var idf = Of(TestGraphs.Of(4, 0, 1, 1, 2, 2, 1, 2, 3));
        Assert.Equal([1], idf.FrontierOf([2]).ToArray());
        Assert.Equal([1], idf.FrontierOf([1]).ToArray());
    }

    [Fact]
    public void IterationFollowsFrontiersOfFrontiers()
    {
        // Nested diamonds: 0 -> {1, 2}, 1 -> {3, 4}, {3, 4} -> 5, {5, 2} -> 6.
        // DF(3) = {5}, DF(5) = {6}: one definition in 3 needs phis at 5 and 6.
        var idf = Of(TestGraphs.Of(7, 0, 1, 0, 2, 1, 3, 1, 4, 3, 5, 4, 5, 5, 6, 2, 6));
        Assert.Equal([5, 6], idf.FrontierOf([3]).ToArray());
    }

    [Fact]
    public void LadderDefinitionReachesEveryJoinBelowIt()
    {
        const int rungs = 10;
        var idf = Of(ControlFlowGraphs.Ladder(rungs));
        // A definition in a_1 needs phis at b_1 .. b_9 and at the exit.
        int[] phis = idf.FrontierOf([1]).ToArray();
        Assert.Equal(rungs, phis.Length);
        Assert.Equal(rungs + 1, phis[0]);
        Assert.Equal(2 * rungs, phis[rungs - 1]);
    }

    [Fact]
    public void UnreachableAndDuplicateNodesAreHarmless()
    {
        var idf = Of(TestGraphs.Of(5, 0, 1, 0, 2, 1, 3, 2, 3));
        Assert.Empty(idf.FrontierOf([4]).ToArray());
        Assert.Equal([3], idf.FrontierOf([1, 1, 4]).ToArray());
    }

    [Fact]
    public void EmptyInputGivesAnEmptyFrontier()
    {
        var idf = Of(TestGraphs.Of(2, 0, 1));
        Assert.True(idf.FrontierOf([]).IsEmpty);
    }

    [Fact]
    public void EmptyGraph()
    {
        var idf = IteratedDominanceFrontier.Of(Dominators.Of(BiAdjacentGraph.Of(TestGraphs.Empty()), []));
        Assert.True(idf.FrontierOf([]).IsEmpty);
        Assert.Throws<ArgumentOutOfRangeException>(() => idf.FrontierOf([0]));
    }

    [Fact]
    public void OutOfRangeNodesAreRejected()
    {
        var idf = Of(TestGraphs.Of(2, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => idf.FrontierOf([2]));
        Assert.Throws<ArgumentOutOfRangeException>(() => idf.FrontierOf([-1]));
    }

    [Fact]
    public void NullDominatorsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => IteratedDominanceFrontier.Of(null!));
    }

    [Fact]
    public void SeveralEntries()
    {
        // Entries 0 and 3 both lead to 1; a definition in 0 needs a phi at 1.
        var idf = IteratedDominanceFrontier.Of(
            Dominators.Of(BiAdjacentGraph.Of(TestGraphs.Of(4, 0, 1, 3, 1, 1, 2)), [0, 3]));
        Assert.Equal([1], idf.FrontierOf([0]).ToArray());
    }

    [Fact]
    public void LongInputOnADeepChainGrowsTheHeap()
    {
        // A chain with one back edge from its end to its middle h: every node from h on has h in its
        // frontier. More given nodes than the initial heap holds, deepest last, so the heap grows and
        // the walk from the deepest root covers the rest in one step each.
        const int deep = 5000;
        var builder = new GraphBuilder();
        builder.AddNodes(deep);
        for (int i = 0; i + 1 < deep; i++)
        {
            builder.AddEdge(i, i + 1);
        }
        builder.AddEdge(deep - 1, deep / 2);
        var idf = Of(builder.Build());
        var nodes = new int[deep];
        for (int i = 0; i < deep; i++)
        {
            nodes[i] = i;
        }
        Assert.Equal([deep / 2], idf.FrontierOf(nodes).ToArray());
        Assert.Empty(idf.FrontierOf(nodes.AsSpan(0, deep / 2)).ToArray());
    }
}
