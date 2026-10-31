using Graphity;

namespace Examples.Guide;

/// <summary>
/// Dominators, the dominator tree, post-dominators and dominators from several entries.
/// </summary>
internal static class FindingDominators
{
    /// <summary>
    /// Immediate dominators in the control flow of load.
    /// </summary>
    internal sealed record ImmediateResult(int ImmediateDominatorOfNext,
        int ImmediateDominatorOfExit, bool ExitReachable, List<int> DominatorsOfNext);

    /// <summary>The dominator tree and questions answered by it.</summary>
    internal sealed record TreeResult(Graph Tree, bool BodyDominatesNext,
        bool ThenDominatesNext, int LowestCommonDominator);

    /// <summary>Immediate post-dominators and the post-dominator tree.</summary>
    internal sealed record PostResult(int JoinOfBody, int JoinOfHeader, Graph Tree);

    /// <summary>Dominators from two entries, and their dominator forest.</summary>
    internal sealed record TwoEntriesResult(int ImmediateDominatorOfLoad,
        int ImmediateDominatorOfParse, int ImmediateDominatorOfFormat, Graph Forest);

    /// <summary>
    /// Returns immediate dominators of the control flow of load and of the calls.
    /// </summary>
    internal static ImmediateResult Immediate()
    {
        Graph cfg = SmallProgram.Cfg();
        string[] blocks = SmallProgram.BlockNames();
        int entry = SmallProgram.NodeOf(blocks, "entry");
        int next = SmallProgram.NodeOf(blocks, "next");
        int exit = SmallProgram.NodeOf(blocks, "exit");
        // @start region="dominators"
        // The analysis follows edges against their direction too, so it takes a
        // BiAdjacentGraph.
        Dominators dominators = Dominators.Of(BiAdjacentGraph.Of(cfg), entry);
        // 2 - body
        int immediateDominatorOfNext = dominators.ImmediateDominatorOf(next);
        // 1 - header
        int immediateDominatorOfExit = dominators.ImmediateDominatorOf(exit);
        bool exitReachable = dominators.IsReachable(exit);                      // true

        // All blocks that dominate next: next, its immediate dominator, that one's, and
        // so on.
        List<int> dominatorsOfNext = [];
        for (int d = next; d != -1; d = dominators.ImmediateDominatorOf(d))
        {
            dominatorsOfNext.Add(d);
        }
        // dominatorsOfNext: [4, 2, 1, 0] - next, body, header, entry
        // @end
        return new ImmediateResult(immediateDominatorOfNext, immediateDominatorOfExit,
            exitReachable, dominatorsOfNext);
    }

    /// <summary>
    /// Returns the dominator tree of the control flow of load and what it answers.
    /// </summary>
    internal static TreeResult Tree()
    {
        Graph cfg = SmallProgram.Cfg();
        string[] blocks = SmallProgram.BlockNames();
        int entry = SmallProgram.NodeOf(blocks, "entry");
        int body = SmallProgram.NodeOf(blocks, "body");
        int then = SmallProgram.NodeOf(blocks, "then");
        int next = SmallProgram.NodeOf(blocks, "next");
        // @start region="dominatorTree"
        // Dominators takes a BiAdjacentGraph, which also holds the in-nodes.
        Dominators dominators = Dominators.Of(BiAdjacentGraph.Of(cfg), entry);
        Graph tree = Graphs.DominatorTree(dominators);
        // tree: entry -> header, header -> body, header -> exit, body -> then,
        //       body -> next

        // Does one block dominate another? An ancestor question on the dominator tree.
        Ancestry dominance = Ancestry.Of(dominators);
        bool bodyDominatesNext = dominance.IsAncestorOf(body, next);   // true
        bool thenDominatesNext = dominance.IsAncestorOf(then, next);   // false
        // 2 - body
        int lowestCommonDominator = dominance.LowestCommonAncestorOf(then, next);
        // @end
        return new TreeResult(tree, bodyDominatesNext, thenDominatesNext,
            lowestCommonDominator);
    }

    /// <summary>
    /// Returns immediate post-dominators of the control flow of load, and its
    /// post-dominator tree.
    /// </summary>
    internal static PostResult Post()
    {
        Graph cfg = SmallProgram.Cfg();
        string[] blocks = SmallProgram.BlockNames();
        int body = SmallProgram.NodeOf(blocks, "body");
        int header = SmallProgram.NodeOf(blocks, "header");
        int exit = SmallProgram.NodeOf(blocks, "exit");
        // @start region="postDominators"
        // The post-dominators are the dominators of the transposed graph, from the exit.
        // Dominators takes a BiAdjacentGraph, which also holds the in-nodes.
        Dominators postDominators =
            Dominators.Of(BiAdjacentGraph.Of(cfg).Transposed(), exit);
        int joinOfBody = postDominators.ImmediateDominatorOf(body);       // 4 - next
        int joinOfHeader = postDominators.ImmediateDominatorOf(header);   // 5 - exit

        // The post-dominator tree: an edge from each block to the blocks it immediately
        // post-dominates.
        Graph tree = Graphs.DominatorTree(postDominators);
        // tree: header -> entry, header -> next, next -> body, next -> then,
        //       exit -> header
        // @end
        return new PostResult(joinOfBody, joinOfHeader, tree);
    }

    /// <summary>
    /// Returns dominators of the calls from main and from load, and their dominator
    /// forest.
    /// </summary>
    internal static TwoEntriesResult TwoEntries()
    {
        Graph calls = SmallProgram.Calls();
        string[] methods = SmallProgram.MethodNames();
        int main = SmallProgram.NodeOf(methods, "main");
        int load = SmallProgram.NodeOf(methods, "load");
        int parse = SmallProgram.NodeOf(methods, "parse");
        int format = SmallProgram.NodeOf(methods, "format");
        // @start region="entries"
        // Besides main, a background job calls load directly.
        Dominators dominators = Dominators.Of(BiAdjacentGraph.Of(calls), [main, load]);
        // -1 - an entry
        int immediateDominatorOfLoad = dominators.ImmediateDominatorOf(load);
        // 3 - load
        int immediateDominatorOfParse = dominators.ImmediateDominatorOf(parse);
        // 2 - run
        int immediateDominatorOfFormat = dominators.ImmediateDominatorOf(format);

        // The dominator forest: here a tree from main and one from load.
        Graph forest = Graphs.DominatorTree(dominators);
        // forest: main -> parseArgs, main -> run, run -> save, run -> show,
        //         run -> format, load -> parse, load -> read, save -> write
        // @end
        return new TwoEntriesResult(immediateDominatorOfLoad, immediateDominatorOfParse,
            immediateDominatorOfFormat, forest);
    }
}
