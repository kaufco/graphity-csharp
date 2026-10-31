using Graphity;

namespace Examples.Guide;

/// <summary>The packages without the edges that close their cycles.</summary>
internal static class BreakingCycles
{
    /// <summary>The edges removed and the graph without them.</summary>
    internal sealed record Broken(Edges BackEdges, Graph Acyclic, bool IsDag);

    /// <summary>Returns the packages without their back edges.</summary>
    internal static Broken WithoutBackEdges()
    {
        Graph dependencies = SmallProgram.Dependencies();
        // @start region="backEdges"
        Edges backEdges = BackEdges.Of(dependencies);   // storage -> model, edge 10

        // Build the graph again, without the back edges.
        bool[] isBack = new bool[dependencies.EdgeCount];
        foreach (int edge in backEdges.EdgeIndices)
        {
            isBack[edge] = true;
        }
        var builder = new GraphBuilder();
        builder.AddNodes(dependencies.NodeCount);
        dependencies.ForEachEdge((source, target, edge) =>
        {
            if (!isBack[edge])
            {
                builder.AddEdge(source, target);
            }
        });
        Graph acyclic = builder.Build();
        bool isDag = Graphs.IsDag(acyclic);   // true
        // @end
        return new Broken(backEdges, acyclic, isDag);
    }
}
