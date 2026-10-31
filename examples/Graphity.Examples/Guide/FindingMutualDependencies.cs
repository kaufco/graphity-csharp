using Graphity;

namespace Examples.Guide;

/// <summary>
/// The rules of a grammar that use each other, found as strongly connected components.
/// </summary>
internal static class FindingMutualDependencies
{
    /// <summary>
    /// The groups of rules that use each other, and the number of SCCs.
    /// </summary>
    internal sealed record Groups(int SccCount, List<List<string>> Recursive);

    /// <summary>Whether two rules are in the SCC of the expressions.</summary>
    internal sealed record SameGroupResult(int Expressions, bool FactorInIt,
        bool AssignmentInIt);

    /// <summary>
    /// Returns the groups of rules that use each other, by their names.
    /// </summary>
    internal static Groups RecursiveGroups()
    {
        Graph rules = SmallGrammar.Rules();
        string[] names = SmallGrammar.RuleNames();
        // @start region="groups"
        // Group the rules into SCCs: rules in one SCC use each other, directly or through
        // others.
        Sccs sccs = Components.SccsOf(rules);
        int sccCount = sccs.SccCount;   // 6

        // Keep the SCCs with a cycle: those are the rules that use themselves, through
        // each other.
        List<List<string>> recursive = [];
        for (int scc = 0; scc < sccs.SccCount; scc++)
        {
            if (sccs.ContainsCycle(scc))
            {
                recursive.Add([.. sccs.NodesOf(scc).Select(rule => names[rule])]);
            }
        }
        // recursive: [[expression, term, factor], [statement, block, ifStatement]]
        // @end
        return new Groups(sccCount, recursive);
    }

    /// <summary>Returns whether two pairs of rules are in the same SCC.</summary>
    internal static SameGroupResult SameGroup()
    {
        Sccs sccs = Components.SccsOf(SmallGrammar.Rules());
        int factor = SmallGrammar.RuleOf("factor");
        int expression = SmallGrammar.RuleOf("expression");
        int assignment = SmallGrammar.RuleOf("assignment");
        // @start region="sameGroup"
        // Two rules reach each other if they are in the same SCC.
        int expressions = sccs.SccOf(expression);                      // 2
        bool factorInIt = sccs.SccOf(factor) == expressions;           // true
        bool assignmentInIt = sccs.SccOf(assignment) == expressions;   // false
        // assignment uses expression, but expression does not use assignment.
        // @end
        return new SameGroupResult(expressions, factorInIt, assignmentInIt);
    }
}
