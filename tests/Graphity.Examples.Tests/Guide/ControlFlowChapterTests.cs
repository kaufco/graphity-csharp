using Examples.Guide;

namespace Examples.Tests.Guide;

/// <summary>The examples of the chapter "Control-flow analysis" added after the pilot.</summary>
public sealed class ControlFlowChapterTests
{
    [Fact]
    public void TheLoopOfParseArgsHasTwoEntries()
    {
        AnalyzingControlFlowFurther.IrreducibleResult irreducible = AnalyzingControlFlowFurther.Irreducible();
        Assert.False(irreducible.Reducible);
        Assert.Equal(1, irreducible.LoopCount);
        Assert.Equal("[1, 2]", irreducible.Entries.ToString());
        Assert.Equal(1, irreducible.Header);
    }

    [Fact]
    public void DominanceFrontiersOfLoad()
    {
        Assert.Equal("""
            0 -> []
            1 -> [1]
            2 -> [1]
            3 -> [4]
            4 -> [1]
            5 -> []

            """, Graphs.AdjacencyListOf(AnalyzingControlFlowFurther.DominanceFrontiers()));
    }

    [Fact]
    public void PhiBlocksOfCount()
    {
        AnalyzingControlFlowFurther.Phis phis = AnalyzingControlFlowFurther.PhiBlocks();
        Assert.Equal("[1]", phis.ForI.ToString());
        Assert.Equal("[1, 4]", phis.ForLast.ToString());
    }

    [Fact]
    public void ControlDependenceOfLoad()
    {
        Assert.Equal("""
            0 -> []
            1 -> [1, 2, 4]
            2 -> [3]
            3 -> []
            4 -> []
            5 -> []

            """, Graphs.AdjacencyListOf(AnalyzingControlFlowFurther.ControlDependence()));
    }
}
