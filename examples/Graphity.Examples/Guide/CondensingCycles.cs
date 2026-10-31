using Graphity;

namespace Examples.Guide;

/// <summary>
/// The rules of a grammar, each group of rules that use each other condensed into one
/// node.
/// </summary>
internal static class CondensingCycles
{
    /// <summary>
    /// The condensed graph of the rules, a name for each of its nodes, and what statement
    /// uses.
    /// </summary>
    internal sealed record Condensed(Graph Graph, bool Acyclic, string[] GroupNames,
        SortedIntView UsedByStatements);

    /// <summary>
    /// Returns the condensed graph of the rules and the names of its nodes.
    /// </summary>
    internal static Condensed Condense()
    {
        Graph rules = SmallGrammar.Rules();
        string[] names = SmallGrammar.RuleNames();
        int statement = SmallGrammar.RuleOf("statement");
        // @start region="condense"
        // Condense each group of rules that use each other into one node.
        Sccs sccs = Components.SccsOf(rules);
        Graph condensed = sccs.Condense();
        bool acyclic = Graphs.IsDag(condensed);   // true
        // condensed: 0 -> [], 1 -> [], 2 -> [0, 1], 3 -> [0, 2], 4 -> [2, 3], 5 -> [4]

        // Name each node of the condensed graph after the rules in it.
        string[] groupNames = new string[sccs.SccCount];
        for (int scc = 0; scc < sccs.SccCount; scc++)
        {
            groupNames[scc] =
                string.Join("/", sccs.NodesOf(scc).Select(rule => names[rule]));
        }
        // groupNames: [name, number, expression/term/factor, assignment,
        //              statement/block/ifStatement, program]

        // Which groups does the group of statement use? The out-nodes of its node.
        SortedIntView usedByStatements = condensed.OutNodes(sccs.SccOf(statement));
        // usedByStatements: [2, 3] - expression/term/factor and assignment
        // @end
        return new Condensed(condensed, acyclic, groupNames, usedByStatements);
    }
}
