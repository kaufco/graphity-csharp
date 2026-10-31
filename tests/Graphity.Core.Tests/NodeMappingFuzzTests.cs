namespace Graphity.Core.Tests;

/// <summary>
/// Cross-validates <see cref="NodeMapping.Reindex"/> against a plain recursive depth-first search
/// (<see cref="RecursiveDfsOrder"/>) as an independent oracle for the documented DFS-preorder
/// semantics: the iterative reindexing must agree node for node, not merely produce some permutation.
/// </summary>
public sealed class NodeMappingFuzzTests
{
    private const int Iterations = 300;

    [Fact]
    public void ReindexOrderMatchesRecursivePreorder()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyGraph(seed);
            var g = labeled.Graph;

            var recursive = new RecursiveDfsOrder(g);
            recursive.Run();
            var nm = NodeMapping.Reindex(g);

            var actual = new int[nm.MappedCount];
            for (int newId = 0; newId < nm.MappedCount; newId++)
            {
                actual[newId] = nm.Get(newId);
            }
            Assert.True(recursive.PreOrder.SequenceEqual(actual),
                $"reindex must equal recursive DFS preorder for {labeled.Describe()}");
        }
    }
}
