using Examples.Guide;

namespace Examples.Tests.Guide;

public sealed class CondensingCyclesTests
{
    [Fact]
    public void TheCondensedRulesAreAcyclicAndNamedInBuildOrder()
    {
        CondensingCycles.Condensed condensed = CondensingCycles.Condense();
        Assert.True(condensed.Acyclic);
        Assert.Equal(
            ["name", "number", "expression/term/factor", "assignment", "statement/block/ifStatement", "program"],
            condensed.GroupNames);
    }

    // The guide draws the condensed graph; its edges must be the ones drawn there.
    [Fact]
    public void TheCondensedGraphHasTheEdgesOfTheDiagram()
    {
        Assert.Equal("""
            0 -> []
            1 -> []
            2 -> [0, 1]
            3 -> [0, 2]
            4 -> [2, 3]
            5 -> [4]

            """, Graphs.AdjacencyListOf(CondensingCycles.Condense().Graph));
    }

    [Fact]
    public void TheStatementsUseTheExpressionsAndAssignment()
    {
        Assert.Equal("[2, 3]", CondensingCycles.Condense().UsedByStatements.ToString());
    }
}
