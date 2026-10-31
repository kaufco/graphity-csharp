namespace Graphity.Core.Tests;

public sealed class EdgeTypesTests
{
    [Fact]
    public void OfEmptyGraphHasNoEdgeTypes()
    {
        var et = EdgeTypes.Of(TestGraphs.Empty());
        foreach (EdgeType t in Enum.GetValues<EdgeType>())
        {
            Assert.Equal(0, Edges.Of(et, t).Count);
        }
    }

    [Fact]
    public void OfPathClassifiesAllEdgesAsTree()
    {
        var g = TestGraphs.Path(5);
        var et = EdgeTypes.Of(g);
        for (int i = 0; i < g.EdgeCount; i++)
        {
            Assert.Equal(EdgeType.Tree, et.TypeOf(i));
        }
        Assert.Equal(4, Edges.Of(et, EdgeType.Tree).Count);
    }

    [Fact]
    public void OfDagWithForwardEdge()
    {
        // 0 -> 1, 0 -> 2, 1 -> 2: tree 0->1, tree 1->2, then 0->2 is forward (2 already finished).
        var et = EdgeTypes.Of(TestGraphs.Of(3, 0, 1, 0, 2, 1, 2));
        var forward = Edges.Of(et, EdgeType.Forward);
        Assert.Equal([0], forward.SourceNodes.ToArray());
        Assert.Equal([2], forward.TargetNodes.ToArray());
    }

    [Fact]
    public void OfDagWithCrossEdge()
    {
        // 0 -> 1, 0 -> 2, 2 -> 1: 1 is finished and off the stack when 2 -> 1 is seen, disc[2] > disc[1].
        var et = EdgeTypes.Of(TestGraphs.Of(3, 0, 1, 0, 2, 2, 1));
        var cross = Edges.Of(et, EdgeType.Cross);
        Assert.Equal([2], cross.SourceNodes.ToArray());
        Assert.Equal([1], cross.TargetNodes.ToArray());
    }

    [Fact]
    public void OfCycleHasOneBackEdge()
    {
        var et = EdgeTypes.Of(TestGraphs.Cycle(4));
        var back = Edges.Of(et, EdgeType.Back);
        Assert.Equal(1, back.Count);
        Assert.Equal(3, back.SourceNodes[0]);
        Assert.Equal(0, back.TargetNodes[0]);
        Assert.Equal(3, Edges.Of(et, EdgeType.Tree).Count);
    }

    [Fact]
    public void OfSelfLoopIsBackEdge() => Assert.Equal(EdgeType.Back, EdgeTypes.Of(TestGraphs.Of(1, 0, 0)).TypeOf(0));

    [Fact]
    public void EdgeTypesPartitionAllEdges()
    {
        // All four types: 0 -> 1, 0 -> 2, 0 -> 3, 1 -> 3, 2 -> 3, 3 -> 0.
        var g = TestGraphs.Of(4,
            0, 1, 0, 2, 0, 3,
            1, 3,
            2, 3,
            3, 0);
        var et = EdgeTypes.Of(g);
        int total = 0;
        foreach (EdgeType t in Enum.GetValues<EdgeType>())
        {
            total += Edges.Of(et, t).Count;
        }
        Assert.Equal(g.EdgeCount, total);
        for (int i = 0; i < g.EdgeCount; i++)
        {
            Assert.True(Enum.IsDefined(et.TypeOf(i)));
        }
    }

    [Fact]
    public void TypeOfAgreesWithEdgesOfType()
    {
        var g = TestGraphs.Of(4, 0, 1, 0, 2, 1, 2, 1, 3, 2, 3, 3, 0);
        var et = EdgeTypes.Of(g);
        foreach (EdgeType type in Enum.GetValues<EdgeType>())
        {
            var edges = Edges.Of(et, type);
            for (int i = 0; i < edges.Count; i++)
            {
                int src = edges.SourceNodes[i];
                int tgt = edges.TargetNodes[i];
                int foundIndex = -1;
                for (int j = g.OutEdgeStart(src); j < g.OutEdgeEnd(src); j++)
                {
                    if (g.EdgeTarget(j) == tgt)
                    {
                        foundIndex = j;
                        break;
                    }
                }
                Assert.NotEqual(-1, foundIndex);
                Assert.Equal(type, et.TypeOf(foundIndex));
            }
        }
    }

    [Fact]
    public void BackEdgesAgreeWithBackEdgesClass()
    {
        var g = TestGraphs.Of(5,
            0, 1, 1, 2, 2, 0,
            2, 3,
            3, 4, 4, 3);
        var viaEt = Edges.Of(EdgeTypes.Of(g), EdgeType.Back);
        var viaBe = BackEdges.Of(g);
        Assert.Equal(viaBe.Count, viaEt.Count);
        Assert.True(PairsOf(viaEt).SetEquals(PairsOf(viaBe)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(1000)]
    public void TypeOfOutOfRangeThrows(int edgeIndex)
    {
        var et = EdgeTypes.Of(TestGraphs.Path(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => et.TypeOf(edgeIndex));
    }

    [Fact]
    public void EdgeTypeOrdinalsAreTheSharedByteLayout()
    {
        Assert.Equal(0, (byte)EdgeType.Tree);
        Assert.Equal(1, (byte)EdgeType.Back);
        Assert.Equal(2, (byte)EdgeType.Forward);
        Assert.Equal(3, (byte)EdgeType.Cross);
    }

    private static HashSet<long> PairsOf(Edges edges)
    {
        var set = new HashSet<long>();
        for (int i = 0; i < edges.Count; i++)
        {
            set.Add(((long)edges.SourceNodes[i] << 32) | (uint)edges.TargetNodes[i]);
        }
        return set;
    }
}
