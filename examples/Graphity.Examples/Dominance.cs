using Graphity;

namespace Examples;

/// <summary>
/// The immediate dominator of <c>next</c>, and whether <c>body</c> and <c>then</c>
/// dominate it.
/// </summary>
internal readonly record struct DominatorsOfNext(int Idom, bool BodyDominatesNext,
    bool ThenDominatesNext);

/// <summary>Dominators, and ancestor queries on the dominator tree.</summary>
internal static class Dominance
{
    /// <summary>
    /// Returns what the dominators of <c>load</c> say about its basic block <c>next</c>.
    /// </summary>
    internal static DominatorsOfNext DominatorsOfNext()
    {
        Graph cfg = SmallProgram.Cfg();
        string[] names = SmallProgram.BlockNames();
        int entry = SmallProgram.NodeOf(names, "entry");
        int body = SmallProgram.NodeOf(names, "body");
        int then = SmallProgram.NodeOf(names, "then");
        int next = SmallProgram.NodeOf(names, "next");
        // @start region="dominators"
        Dominators dominators = Dominators.Of(BiAdjacentGraph.Of(cfg), entry);
        // 2 - the block body
        int idom = dominators.ImmediateDominatorOf(next);
        Ancestry dominance = Ancestry.Of(dominators);
        bool bodyDominatesNext = dominance.IsAncestorOf(body, next);      // true
        bool thenDominatesNext = dominance.IsAncestorOf(then, next);      // false
        // @end
        return new DominatorsOfNext(idom, bodyDominatesNext, thenDominatesNext);
    }

    /// <summary>
    /// Returns the immediate post-dominator of <c>body</c> in the control flow of
    /// <c>load</c>.
    /// </summary>
    internal static int PostDominatorOfBody()
    {
        Graph cfg = SmallProgram.Cfg();
        string[] names = SmallProgram.BlockNames();
        int body = SmallProgram.NodeOf(names, "body");
        int exit = SmallProgram.NodeOf(names, "exit");
        // @start region="postDominators"
        Dominators postDominators =
            Dominators.Of(BiAdjacentGraph.Of(cfg).Transposed(), exit);
        int join = postDominators.ImmediateDominatorOf(body);   // 4 - the block next
        // @end
        return join;
    }

    /// <summary>
    /// Returns the basic blocks of <c>load</c> that <c>body</c> dominates.
    /// </summary>
    internal static int[] DominatedByBody()
    {
        Graph cfg = SmallProgram.Cfg();
        string[] names = SmallProgram.BlockNames();
        int entry = SmallProgram.NodeOf(names, "entry");
        int body = SmallProgram.NodeOf(names, "body");
        Dominators dominators = Dominators.Of(BiAdjacentGraph.Of(cfg), entry);
        // @start region="subtreeOf"
        PreorderAncestry dominance = PreorderAncestry.Of(Ancestry.Of(dominators));
        ReadOnlySpan<int> dominatedByBody = dominance.SubtreeOf(body);
        // dominatedByBody: [2, 3, 4] - body, then and next
        // @end
        return dominatedByBody.ToArray();
    }
}
