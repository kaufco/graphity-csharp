using Graphity;
using Graphity.ControlFlow;

namespace Examples;

/// <summary>The loop around a block, with what the example reads from it.</summary>
internal sealed record LoopOfBlock(Loops Loops, int Loop, int Header, int[] Blocks);

/// <summary>
/// The loops, the phi-nodes and the control dependence of the method <c>load</c>.
/// </summary>
internal static class AnalyzingControlFlow
{
    /// <summary>Returns the loop around the block <c>then</c> of <c>load</c>.</summary>
    internal static LoopOfBlock LoopOfThen()
    {
        Graph cfg = SmallProgram.Cfg();
        string[] names = SmallProgram.BlockNames();
        int entry = SmallProgram.NodeOf(names, "entry");
        int then = SmallProgram.NodeOf(names, "then");
        // @start region="loops"
        // The basic blocks of load are the nodes 0..5 of cfg: entry, header, body, then,
        // next, exit.

        // Find the loops of load, from its entry block.
        Loops loops = Loops.Of(BiAdjacentGraph.Of(cfg), entry);

        // Find the loop around the block then, with its header and its body.
        int loop = loops.InnermostLoopOf(then);         // 0 - the for loop, the only loop
        int header = loops.HeaderOf(loop);              // 1 - the block header
        // [1, 2, 3, 4] - header, body, then and next
        ReadOnlySpan<int> blocks = loops.BodyOf(loop);
        // @end
        return new LoopOfBlock(loops, loop, header, blocks.ToArray());
    }

    /// <summary>
    /// Returns the blocks of <c>load</c> where the variable <c>i</c> needs a phi-node.
    /// </summary>
    internal static SortedIntView PhiBlocksOfI()
    {
        Graph cfg = SmallProgram.Cfg();
        string[] names = SmallProgram.BlockNames();
        int entry = SmallProgram.NodeOf(names, "entry");
        int next = SmallProgram.NodeOf(names, "next");
        // @start region="frontierOf"
        Dominators dominators = Dominators.Of(BiAdjacentGraph.Of(cfg), entry);
        IteratedDominanceFrontier phis = IteratedDominanceFrontier.Of(dominators);
        // i is assigned in entry (i = 0) and in next (i++)
        SortedIntView phiBlocks = phis.FrontierOf([entry, next]);
        // phiBlocks: [1] - header
        // @end
        return phiBlocks;
    }

    /// <summary>
    /// Returns the control dependence between the blocks of <c>load</c>.
    /// </summary>
    internal static Graph ControlDependenceOfLoad()
    {
        Graph cfg = SmallProgram.Cfg();
        int exit = SmallProgram.NodeOf(SmallProgram.BlockNames(), "exit");
        // @start region="controlDependence"
        Dominators postDominators =
            Dominators.Of(BiAdjacentGraph.Of(cfg).Transposed(), exit);
        Graph controlDependence =
            BiAdjacentGraph.Of(ControlFlow.DominanceFrontiers(postDominators))
                .Transposed();
        // header -> [header, body, next]: the loop condition decides whether they run
        // body -> [then]: the if decides whether then runs
        // @end
        return controlDependence;
    }
}
