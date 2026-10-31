namespace Graphity.ControlFlow.Tests;

/// <summary>
/// Unit tests of <see cref="Loops"/>. Java's <c>viewsAreReadOnly</c> has no counterpart:
/// <see cref="Loops.BodyOf"/> returns a <see cref="ReadOnlySpan{T}"/>, which has no mutator, and
/// <see cref="Loops.EntriesOf"/> a <see cref="SortedIntView"/>, which has none either.
/// </summary>
public sealed class LoopsTests
{
    private static BiAdjacentGraph Bi(int n, params ReadOnlySpan<int> edges) => BiAdjacentGraph.Of(TestGraphs.Of(n, edges));

    [Fact]
    public void NaturalLoop()
    {
        // 0 -> 1 -> 2 -> 1, 2 -> 3
        var loops = Loops.Of(Bi(4, 0, 1, 1, 2, 2, 1, 2, 3), 0);
        Assert.Equal(1, loops.LoopCount);
        Assert.True(loops.IsReducible);
        Assert.Equal(1, loops.HeaderOf(0));
        Assert.Equal([1, 2], loops.BodyOf(0).ToArray());
        Assert.Equal([1], loops.EntriesOf(0).ToArray());
        Assert.Equal(-1, loops.ParentOf(0));
        Assert.Equal(-1, loops.InnermostLoopOf(0));
        Assert.Equal(0, loops.InnermostLoopOf(2));
        Assert.Equal(-1, loops.InnermostLoopOf(3));
        Assert.Equal(1, loops.LoopDepthOf(2));
        Assert.Equal(0, loops.LoopDepthOf(3));
    }

    [Fact]
    public void NestedLoops()
    {
        // Outer loop 1..4 (back edge 4 -> 1), inner loop 2..3 (back edge 3 -> 2), exit 5.
        var loops = Loops.Of(Bi(6, 0, 1, 1, 2, 2, 3, 3, 2, 3, 4, 4, 1, 4, 5), 0);
        Assert.Equal(2, loops.LoopCount);
        Assert.Equal(1, loops.HeaderOf(0));
        Assert.Equal(2, loops.HeaderOf(1));
        Assert.Equal(0, loops.ParentOf(1));
        Assert.Equal([1, 4, 2, 3], loops.BodyOf(0).ToArray());
        Assert.Equal([2, 3], loops.BodyOf(1).ToArray());
        Assert.True(loops.IsInLoop(3, 0));
        Assert.False(loops.IsInLoop(4, 1));
        Assert.Equal(2, loops.LoopDepthOf(3));
        Assert.Equal(1, loops.LoopDepthOf(4));
        Assert.True(loops.IsReducible);
    }

    [Fact]
    public void IrreducibleCycleIsOneLoopWithTwoEntries()
    {
        // 0 -> 1, 0 -> 2, 1 <-> 2. The search reaches 1 first, so 1 is the header.
        var loops = Loops.Of(Bi(3, 0, 1, 0, 2, 1, 2, 2, 1), 0);
        Assert.Equal(1, loops.LoopCount);
        Assert.Equal(1, loops.HeaderOf(0));
        Assert.Equal([1, 2], loops.EntriesOf(0).ToArray());
        Assert.False(loops.IsReducible);
        Assert.Equal(1, loops.LoopDepthOf(1));
        Assert.Equal(1, loops.LoopDepthOf(2));
    }

    [Fact]
    public void IrreducibleLoopNestedInAReducibleOne()
    {
        // Outer loop headed by 1 (back edge 4 -> 1) around the cycle 2 <-> 3, entered at 2 and 3.
        var loops = Loops.Of(Bi(6, 0, 1, 1, 2, 1, 3, 2, 3, 3, 2, 3, 4, 4, 1, 4, 5), 0);
        Assert.Equal(2, loops.LoopCount);
        Assert.Equal([1], loops.EntriesOf(0).ToArray());
        Assert.Equal([2, 3], loops.EntriesOf(1).ToArray());
        Assert.False(loops.IsReducible);
    }

    [Fact]
    public void SelfLoopIsALoopOfItsOwn()
    {
        var loops = Loops.Of(Bi(3, 0, 1, 1, 1, 1, 2), 0);
        Assert.Equal(1, loops.LoopCount);
        Assert.Equal(1, loops.HeaderOf(0));
        Assert.Equal([1], loops.BodyOf(0).ToArray());
        Assert.Equal([1], loops.EntriesOf(0).ToArray());
    }

    [Fact]
    public void AHeaderWithASelfLoopHeadsNoLoopOfItsOwn()
    {
        // 0 -> 1, 1 -> 1, 1 <-> 2: the self-loop leads into the header 1 from inside its loop.
        var loops = Loops.Of(Bi(3, 0, 1, 1, 1, 1, 2, 2, 1), 0);
        Assert.Equal(1, loops.LoopCount);
        Assert.Equal(1, loops.HeaderOf(0));
        Assert.Equal([1, 2], loops.BodyOf(0).ToArray());
        // On a node that heads no larger loop, the self-loop makes a nested loop.
        var nested = Loops.Of(Bi(3, 0, 1, 1, 2, 2, 2, 2, 1), 0);
        Assert.Equal(2, nested.LoopCount);
        Assert.Equal(2, nested.HeaderOf(1));
        Assert.Equal([2], nested.BodyOf(1).ToArray());
    }

    [Fact]
    public void GraphEntriesCountAsLoopEntries()
    {
        // Entries 1 and 2 on the cycle 1 <-> 2: two entries, irreducible.
        var loops = Loops.Of(Bi(3, 1, 2, 2, 1), [1, 2]);
        Assert.Equal(1, loops.LoopCount);
        Assert.Equal(1, loops.HeaderOf(0));
        Assert.Equal([1, 2], loops.EntriesOf(0).ToArray());
        Assert.False(loops.IsReducible);
        // With 1 as the only entry, the same cycle is a natural loop.
        Assert.True(Loops.Of(Bi(3, 1, 2, 2, 1), 1).IsReducible);
    }

    [Fact]
    public void EntryOrderDecidesTheHeaderOfAnIrreducibleLoop()
    {
        var graph = Bi(3, 1, 2, 2, 1);
        Assert.Equal(2, Loops.Of(graph, [2, 1]).HeaderOf(0));
    }

    [Fact]
    public void UnreachableCyclesAreNoLoops()
    {
        var loops = Loops.Of(Bi(4, 0, 1, 2, 3, 3, 2), 0);
        Assert.Equal(0, loops.LoopCount);
        Assert.True(loops.IsReducible);
        Assert.Equal(-1, loops.InnermostLoopOf(2));
    }

    [Fact]
    public void EdgesFromUnreachableNodesEnterNoLoop()
    {
        // 3 -> 2 comes from a node no entry reaches and must not make 2 a second entry.
        var loops = Loops.Of(Bi(4, 0, 1, 1, 2, 2, 1, 3, 2), 0);
        Assert.Equal(1, loops.LoopCount);
        Assert.Equal([1], loops.EntriesOf(0).ToArray());
        Assert.True(loops.IsReducible);
    }

    [Fact]
    public void EmptyEntryListAndEmptyGraph()
    {
        Assert.Equal(0, Loops.Of(Bi(2, 0, 1, 1, 0), []).LoopCount);
        Assert.Equal(0, Loops.Of(BiAdjacentGraph.Of(TestGraphs.Empty()), []).LoopCount);
    }

    [Fact]
    public void SiblingLoopsShareAParent()
    {
        // Outer loop 1..5 (back edge 5 -> 1) around two sibling loops, 2 <-> 3 and 4 <-> 5:
        // 1 -> 2 -> 3 -> 2, 3 -> 4 -> 5 -> 4, 5 -> 1, 5 -> 6.
        var loops = Loops.Of(Bi(7, 0, 1, 1, 2, 2, 3, 3, 2, 3, 4, 4, 5, 5, 4, 5, 1, 5, 6), 0);
        Assert.Equal(3, loops.LoopCount);
        Assert.Equal(1, loops.HeaderOf(0));
        Assert.Equal(0, loops.ParentOf(1));
        Assert.Equal(0, loops.ParentOf(2));
        Assert.Equal([1, 2, 3, 4, 5], loops.BodyOf(0).ToArray());
    }

    [Fact]
    public void BodiesAreSlicesOfOneArrayInLoopIdOrder()
    {
        // The sibling loops above: the body of the outer loop is its own nodes, then the bodies of
        // loop 1 and loop 2, each a slice of the same array.
        var loops = Loops.Of(Bi(7, 0, 1, 1, 2, 2, 3, 3, 2, 3, 4, 4, 5, 5, 4, 5, 1, 5, 6), 0);
        Assert.Equal([2, 3], loops.BodyOf(1).ToArray());
        Assert.Equal([4, 5], loops.BodyOf(2).ToArray());
        var outer = loops.BodyOf(0);
        Assert.True(outer.Overlaps(loops.BodyOf(1), out int offsetOfLoop1));
        Assert.Equal(1, offsetOfLoop1);
        Assert.True(outer.Overlaps(loops.BodyOf(2), out int offsetOfLoop2));
        Assert.Equal(3, offsetOfLoop2);
    }

    [Fact]
    public void OutOfRangeArgumentsAreRejected()
    {
        var loops = Loops.Of(Bi(3, 0, 1, 1, 2, 2, 1), 0);
        Assert.Equal("entry", Assert.Throws<ArgumentOutOfRangeException>(() => Loops.Of(Bi(2, 0, 1), 2)).ParamName);
        Assert.Equal("entries", Assert.Throws<ArgumentOutOfRangeException>(() => Loops.Of(Bi(2, 0, 1), [0, -1])).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => loops.InnermostLoopOf(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => loops.LoopDepthOf(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => loops.IsInLoop(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => loops.IsInLoop(3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => loops.HeaderOf(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => loops.EntriesOf(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => loops.ParentOf(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => loops.BodyOf(1).ToArray());
    }

    [Fact]
    public void MessagesWriteNegativeValuesAsJavaDoesInEveryCulture()
    {
        using var culture = new ForeignCulture();
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => Loops.Of(Bi(2, 0, 1), -1));
        Assert.StartsWith("Index -1 out of bounds for length 2", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullGraphIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Loops.Of(null!, 0));
        Assert.Throws<ArgumentNullException>(() => Loops.Of(null!, []));
    }

    [Fact]
    public void TheNodeLimitIsTheOneOfTheCore()
    {
        // Loops.Of documents NodeCount > 2^30 - 30 as rejected, the same limit as Dominators.Of: two
        // ints per preorder number, the virtual root included, within Array.MaxLength (Java: 2^30 - 6
        // within Integer.MAX_VALUE - 8).
        Assert.Equal((1 << 30) - 30, DfsPreorder.MaxNodeCount);
        Assert.True(2L * (DfsPreorder.MaxNodeCount + 1) <= Array.MaxLength);
        Assert.True(2L * (DfsPreorder.MaxNodeCount + 2) > Array.MaxLength);
        DfsPreorder.CheckNodeCount(DfsPreorder.MaxNodeCount, "Loops");
        var e = Assert.Throws<ArgumentException>(() => DfsPreorder.CheckNodeCount(DfsPreorder.MaxNodeCount + 1, "Loops"));
        Assert.Equal("Loops takes the node limit of Dominators, which keeps two ints per node in one"
            + " array, counting its virtual root: nodeCount must be <= 1073741794, but was"
            + " 1073741795", e.Message);
    }

    [Fact]
    public void DeepNestingDoesNotOverflowStack()
    {
        // deep / 2 loops nested inside each other: i -> i + 1 down the chain, and a back edge from
        // deep - 1 - i to i, so loop i encloses loop i + 1.
        const int deep = 1_000_000;
        var builder = new GraphBuilder();
        builder.AddNodes(deep);
        for (int i = 0; i + 1 < deep; i++)
        {
            builder.AddEdge(i, i + 1);
        }
        for (int i = 0; i < deep / 2; i++)
        {
            builder.AddEdge(deep - 1 - i, i);
        }
        var loops = Loops.Of(BiAdjacentGraph.Of(builder.Build()), 0);
        Assert.Equal(deep / 2, loops.LoopCount);
        Assert.Equal(deep / 2, loops.LoopDepthOf(deep / 2));
        Assert.True(loops.IsReducible);
    }
}
