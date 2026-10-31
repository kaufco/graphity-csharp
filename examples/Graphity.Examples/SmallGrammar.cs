using Graphity;

namespace Examples;

/// <summary>
/// The second example domain: the grammar of a small programming language, seen as a
/// graph.
/// </summary>
/// <remarks>
/// <para>
/// The grammar has a rule for each construct of the language, written in EBNF (extended
/// Backus-Naur form):
/// </para>
/// <code>
/// program     = { statement } ;
/// statement   = block | ifStatement | assignment ;
/// block       = "{" { statement } "}" ;
/// ifStatement = "if" "(" expression ")" statement ;
/// assignment  = name "=" expression ";" ;
/// expression  = term { "+" term } ;
/// term        = factor { "*" factor } ;
/// factor      = number | name | "(" expression ")" ;
/// </code>
/// <para>
/// <c>name</c> and <c>number</c> are rules for single tokens and use no other rule.
/// <see cref="Rules"/> gives an edge from each rule to each rule it uses, with the names
/// in <see cref="RuleNames"/>: statements nest in statements, expressions in expressions.
/// </para>
/// <para>
/// Node IDs follow the order of the names. The graph never changes: the comments of the
/// examples, and the documentation that shows them, depend on every node and edge.
/// </para>
/// </remarks>
public static class SmallGrammar
{
    private static readonly string[] RuleNameList =
    [
        "program", "statement", "block", "ifStatement", "assignment",
        "expression", "term", "factor", "name", "number",
    ];

    // A rule, then the rules it uses, in the order they appear in the rule.
    private static readonly string[][] Uses =
    [
        ["program", "statement"],
        ["statement", "block", "ifStatement", "assignment"],
        ["block", "statement"],
        ["ifStatement", "expression", "statement"],
        ["assignment", "name", "expression"],
        ["expression", "term"],
        ["term", "factor"],
        ["factor", "number", "name", "expression"],
    ];

    /// <summary>
    /// Returns the rules and the rules they use: an edge from each rule to each rule it
    /// uses.
    /// </summary>
    /// <returns>10 nodes, 14 edges.</returns>
    public static Graph Rules()
    {
        var builder = new GraphBuilder();
        builder.AddNodes(RuleNameList.Length);
        foreach (string[] row in Uses)
        {
            int rule = RuleOf(row[0]);
            for (int i = 1; i < row.Length; i++)
            {
                builder.AddEdge(rule, RuleOf(row[i]));
            }
        }
        return builder.Build();
    }

    /// <summary>
    /// Returns the names of the rules, by node ID of <see cref="Rules"/>.
    /// </summary>
    /// <returns>
    /// program, statement, block, ifStatement, assignment, expression, term, factor,
    /// name, number.
    /// </returns>
    public static string[] RuleNames() => [.. RuleNameList];

    /// <summary>Returns the node ID of the rule named <paramref name="name"/>.</summary>
    /// <param name="name">The name of the rule.</param>
    /// <returns>The node ID.</returns>
    /// <exception cref="ArgumentException">If no rule has this name.</exception>
    public static int RuleOf(string name) => SmallProgram.NodeOf(RuleNameList, name);
}
