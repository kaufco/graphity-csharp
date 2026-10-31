namespace Graphity.Core.Tests;

public sealed class DominatorsTests
{
    private static int[] ImmediateDominators(Dominators d, int n)
    {
        var result = new int[n];
        for (int node = 0; node < n; node++)
        {
            result[node] = d.ImmediateDominatorOf(node);
        }
        return result;
    }

    private static BiAdjacentGraph Bi(int n, params ReadOnlySpan<int> edges) => BiAdjacentGraph.Of(TestGraphs.Of(n, edges));

    [Fact]
    public void DiamondJoinsAtTheEntry()
    {
        // 0 -> {1, 2} -> 3
        var d = Dominators.Of(Bi(4, 0, 1, 0, 2, 1, 3, 2, 3), 0);
        Assert.Equal([-1, 0, 0, 0], ImmediateDominators(d, 4));
    }

    [Fact]
    public void LoopHeaderDominatesItsBody()
    {
        // 0 -> 1 -> 2 -> 1, 2 -> 3
        var d = Dominators.Of(Bi(4, 0, 1, 1, 2, 2, 1, 2, 3), 0);
        Assert.Equal([-1, 0, 1, 2], ImmediateDominators(d, 4));
    }

    [Fact]
    public void IrreducibleCycleIsDominatedOnlyByTheEntry()
    {
        // 0 -> 1, 0 -> 2, 1 <-> 2: a cycle entered at both nodes.
        var d = Dominators.Of(Bi(3, 0, 1, 0, 2, 1, 2, 2, 1), 0);
        Assert.Equal([-1, 0, 0], ImmediateDominators(d, 3));
    }

    [Fact]
    public void TextbookExample()
    {
        // Lengauer and Tarjan's example, nodes R=0, A..L = 1..12.
        const int r = 0, a = 1, b = 2, c = 3, dd = 4, e = 5, f = 6, g = 7, h = 8, i = 9, j = 10, k = 11, l = 12;
        var graph = Bi(13,
            r, a, r, b, r, c, a, dd, b, a, b, dd, b, e, c, f, c, g, dd, l, e, h, f, i, g, i, g, j,
            h, e, h, k, i, k, j, i, k, r, k, i, l, h);
        var d = Dominators.Of(graph, r);
        Assert.Equal([-1, r, r, r, r, r, c, c, r, r, g, r, dd], ImmediateDominators(d, 13));
    }

    [Fact]
    public void SeveralEntriesShareAVirtualRoot()
    {
        // Entries 0 and 3 both reach 1; only the virtual root dominates it.
        var d = Dominators.Of(Bi(4, 0, 1, 3, 1, 1, 2), [0, 3]);
        Assert.Equal([-1, -1, 1, -1], ImmediateDominators(d, 4));
    }

    [Fact]
    public void AnEntryReachableFromAnotherEntryIsStillARoot()
    {
        var d = Dominators.Of(Bi(3, 0, 1, 1, 2), [0, 1]);
        Assert.Equal([-1, -1, 1], ImmediateDominators(d, 3));
    }

    [Fact]
    public void UnreachableNodesHaveNoDominator()
    {
        var d = Dominators.Of(Bi(4, 0, 1, 2, 3), 0);
        Assert.True(d.IsReachable(1));
        Assert.False(d.IsReachable(2));
        Assert.False(d.IsReachable(3));
        Assert.Equal(-1, d.ImmediateDominatorOf(3));
    }

    [Fact]
    public void EdgesFromUnreachableNodesAreIgnored()
    {
        // 2 -> 1 comes from a node no entry reaches and must not make 1 a join point.
        var d = Dominators.Of(Bi(4, 0, 1, 1, 3, 2, 1, 2, 3), 0);
        Assert.Equal([-1, 0, -1, 1], ImmediateDominators(d, 4));
    }

    [Fact]
    public void SelfLoopsChangeNothing()
    {
        var d = Dominators.Of(Bi(2, 0, 0, 0, 1, 1, 1), 0);
        Assert.Equal([-1, 0], ImmediateDominators(d, 2));
    }

    [Fact]
    public void EmptyEntryListLeavesEverythingUnreachable()
    {
        var d = Dominators.Of(Bi(2, 0, 1), []);
        Assert.False(d.IsReachable(0));
        Assert.False(d.IsReachable(1));
    }

    [Fact]
    public void DuplicateEntriesAreIgnored()
    {
        var graph = Bi(3, 0, 1, 1, 2);
        var d = Dominators.Of(graph, [0, 0, 0]);
        Assert.Equal([-1, 0, 1], ImmediateDominators(d, 3));
    }

    [Fact]
    public void EmptyGraph()
    {
        var d = Dominators.Of(BiAdjacentGraph.Of(TestGraphs.Empty()), []);
        Assert.Equal(0, Graphs.DominatorTree(d).NodeCount);
    }

    [Fact]
    public void OutOfRangeArgumentsAreRejected()
    {
        var graph = Bi(2, 0, 1);
        Assert.Equal("entry", Assert.Throws<ArgumentOutOfRangeException>(() => Dominators.Of(graph, 2)).ParamName);
        Assert.Equal("entries", Assert.Throws<ArgumentOutOfRangeException>(() => Dominators.Of(graph, [0, -1])).ParamName);
        var d = Dominators.Of(graph, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => d.ImmediateDominatorOf(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => d.IsReachable(-1));
    }

    [Fact]
    public void DominatorTreeHasAnEdgeFromEachImmediateDominator()
    {
        var d = Dominators.Of(Bi(4, 0, 1, 1, 2, 2, 1, 2, 3), 0);
        var tree = Graphs.DominatorTree(d);
        Assert.Equal(3, tree.EdgeCount);
        Assert.True(tree.HasOutNode(0, 1));
        Assert.True(tree.HasOutNode(1, 2));
        Assert.True(tree.HasOutNode(2, 3));
        Assert.True(Graphs.IsTree(tree));
    }

    [Fact]
    public void PostDominators()
    {
        // if (0) { 1 } ; 2   -  0 -> 1, 0 -> 2, 1 -> 2, exit 2.
        var cfg = Bi(3, 0, 1, 0, 2, 1, 2);
        var postDominators = Dominators.Of(cfg.Transposed(), 2);
        Assert.Equal(2, postDominators.ImmediateDominatorOf(0));
        Assert.Equal(2, postDominators.ImmediateDominatorOf(1));
    }

    [Fact]
    public void GraphIsTheAnalyzedGraph()
    {
        var cfg = Bi(3, 0, 1, 1, 2);
        Assert.Same(cfg, Dominators.Of(cfg, 0).Graph);
        var transposed = cfg.Transposed();
        Assert.Same(transposed, Dominators.Of(transposed, 2).Graph);
    }

    [Fact]
    public void AncestryOfDominatorsAnswersDominance()
    {
        var d = Dominators.Of(Bi(5, 0, 1, 1, 2, 2, 1, 2, 3, 0, 4, 4, 3), 0);
        var index = Ancestry.Of(d);
        Assert.True(index.IsAncestorOf(1, 2));
        Assert.True(index.IsAncestorOf(0, 3));
        Assert.False(index.IsAncestorOf(1, 3));
        Assert.Equal(0, index.LowestCommonAncestorOf(2, 4));
        Assert.Equal(2, index.DepthOf(2));
    }

    [Fact]
    public void NearestCommonDominatorInADag()
    {
        // Roots 0 and 5; 0 -> 1 -> {2, 3}, 5 -> 3. The deepest node on every path to 2 and 3 from
        // a root: none, because 3 is also reached from 5.
        var dag = Bi(6, 0, 1, 1, 2, 1, 3, 5, 3);
        var index = Ancestry.Of(Dominators.Of(dag, [0, 5]));
        Assert.Equal(-1, index.LowestCommonAncestorOf(2, 3));
        var single = Ancestry.Of(Dominators.Of(dag, 0));
        Assert.Equal(1, single.LowestCommonAncestorOf(2, 3));
    }

    [Fact]
    public void FanBehindAChainIsDominatedByTheEntry()
    {
        var graph = BiAdjacentGraph.Of(ControlFlowGraphs.FanBehindChain(1000, 1000));
        var d = Dominators.Of(graph, 0);
        for (int fan = 1001; fan <= 2000; fan++)
        {
            Assert.Equal(0, d.ImmediateDominatorOf(fan));
        }
        Assert.Equal(999, d.ImmediateDominatorOf(1000));
    }
}
