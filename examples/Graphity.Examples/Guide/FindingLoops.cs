using Graphity;
using Graphity.ControlFlow;

namespace Examples.Guide;

/// <summary>The loops of the method <c>show</c> and how they nest.</summary>
internal static class FindingLoops
{
    /// <summary>
    /// What the example reads from the loops of <c>show</c>; the body copied.
    /// </summary>
    internal sealed record Nesting(int LoopCount, int NotesLoop, int LinesLoop,
        int Header, int[] Body, int Parent, int Depth);

    /// <summary>Returns the loops of <c>show</c> and how they nest.</summary>
    internal static Nesting LoopsOfShow()
    {
        Graph cfg = SmallProgram.CfgOfShow();
        string[] names = SmallProgram.BlockNamesOfShow();
        int entry = SmallProgram.NodeOf(names, "entry");
        int format = SmallProgram.NodeOf(names, "format");
        int print = SmallProgram.NodeOf(names, "print");
        // @start region="loops"
        // Find the loops of Show, from its entry block.
        Loops loops = Loops.Of(BiAdjacentGraph.Of(cfg), entry);
        int loopCount = loops.LoopCount;                    // 2

        // The innermost loop of format is the loop over the notes; that of print, over
        // the lines.
        int notesLoop = loops.InnermostLoopOf(format);      // 0
        int linesLoop = loops.InnermostLoopOf(print);       // 1
        int header = loops.HeaderOf(linesLoop);             // 3 - nextLine
        // [1, 2, 3, 4] - all but entry and exit
        ReadOnlySpan<int> body = loops.BodyOf(notesLoop);

        // How the loops nest.
        int parent = loops.ParentOf(linesLoop);             // 0 - the loop over the notes
        // 2 - print runs in both loops
        int depth = loops.LoopDepthOf(print);
        // @end
        return new Nesting(loopCount, notesLoop, linesLoop, header, body.ToArray(),
            parent, depth);
    }
}
