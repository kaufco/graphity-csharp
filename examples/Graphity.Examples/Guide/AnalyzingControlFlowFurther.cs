using Graphity;
using Graphity.ControlFlow;

namespace Examples.Guide;

/// <summary>
/// Irreducible loops, dominance frontiers, phi-nodes and control dependence.
/// </summary>
internal static class AnalyzingControlFlowFurther
{
    /// <summary>What the example reads from the loops of parseArgs.</summary>
    internal sealed record IrreducibleResult(bool Reducible, int LoopCount,
        SortedIntView Entries, int Header);

    /// <summary>The blocks that need a phi-node for each of two variables.</summary>
    internal sealed record Phis(SortedIntView ForI, SortedIntView ForLast);

    /// <summary>
    /// Returns the loop of parseArgs, which can be entered in two places.
    /// </summary>
    internal static IrreducibleResult Irreducible()
    {
        Graph cfg = SmallProgram.CfgOfParseArgs();
        int entry = SmallProgram.NodeOf(SmallProgram.BlockNamesOfParseArgs(), "entry");
        // @start region="irreducible"
        Loops loops = Loops.Of(BiAdjacentGraph.Of(cfg), entry);
        bool reducible = loops.IsReducible;            // false
        // 1 - option and value, entered at both
        int loopCount = loops.LoopCount;
        SortedIntView entries = loops.EntriesOf(0);    // [1, 2] - option and value
        // 1 - option, reached first by the search
        int header = loops.HeaderOf(0);
        // @end
        return new IrreducibleResult(reducible, loopCount, entries, header);
    }

    /// <summary>Returns the dominance frontiers of the control flow of load.</summary>
    internal static Graph DominanceFrontiers()
    {
        Graph cfg = SmallProgram.Cfg();
        int entry = SmallProgram.NodeOf(SmallProgram.BlockNames(), "entry");
        // @start region="dominanceFrontiers"
        Dominators dominators = Dominators.Of(BiAdjacentGraph.Of(cfg), entry);
        Graph frontiers = ControlFlow.DominanceFrontiers(dominators);
        // frontiers: header -> header, body -> header, then -> next, next -> header
        // @end
        return frontiers;
    }

    /// <summary>
    /// Returns the blocks of load that need a phi-node for i and for last.
    /// </summary>
    internal static Phis PhiBlocks()
    {
        Graph cfg = SmallProgram.Cfg();
        string[] blocks = SmallProgram.BlockNames();
        int entry = SmallProgram.NodeOf(blocks, "entry");
        int then = SmallProgram.NodeOf(blocks, "then");
        int next = SmallProgram.NodeOf(blocks, "next");
        // @start region="phi"
        // The loop counter i is assigned in entry (i = 0) and in next (i++), the variable
        // last in entry (last = null) and in then (last = Parse(line)).
        Dominators dominators = Dominators.Of(BiAdjacentGraph.Of(cfg), entry);
        IteratedDominanceFrontier frontier = IteratedDominanceFrontier.Of(dominators);
        SortedIntView forI = frontier.FrontierOf([entry, next]);   // [1] - header
        SortedIntView forLast = frontier.FrontierOf([entry, then]);
        // forLast: [1, 4] - header and next
        // @end
        return new Phis(forI, forLast);
    }

    /// <summary>Returns the control dependence between the blocks of load.</summary>
    internal static Graph ControlDependence()
    {
        Graph cfg = SmallProgram.Cfg();
        int exit = SmallProgram.NodeOf(SmallProgram.BlockNames(), "exit");
        // @start region="controlDependence"
        // Control dependence is the transposed graph of the dominance frontiers of the
        // post-dominators.
        Dominators postDominators =
            Dominators.Of(BiAdjacentGraph.Of(cfg).Transposed(), exit);
        Graph dependence =
            BiAdjacentGraph.Of(ControlFlow.DominanceFrontiers(postDominators))
                .Transposed();
        // dependence: header -> header, header -> body, header -> next, body -> then
        // @end
        return dependence;
    }
}
