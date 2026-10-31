namespace Graphity.Core.Tests;

/// <summary>
/// Cross-validates the two ways to translate an edge index across a transpose:
/// <see cref="Graphs.TransposedEdgeMapping"/>, a single counting-sort pass, against
/// <see cref="BiAdjacentGraph.TransposedEdgeIndex"/>, two binary searches. Both are checked against a
/// structural oracle that follows neither: the translated edge has source and target swapped, and
/// translating twice returns the original index.
/// </summary>
public sealed class TransposedEdgeMappingFuzzTests
{
    private const int Iterations = 300;

    [Fact]
    public void TranslatedEdgeHasEndpointsSwapped()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyGraph(seed);
            var g = labeled.Graph;
            var transposed = BiAdjacentGraph.Of(g).Transposed();
            var mapping = Graphs.TransposedEdgeMapping(g);

            Assert.False(mapping.IsPartial, $"mapping is total for {labeled.Describe()}");
            Assert.Equal(g.EdgeCount, mapping.MappedCount);
            for (int q = 0; q < transposed.EdgeCount; q++)
            {
                int p = mapping.Get(q);
                Assert.True(g.EdgeSource(p) == transposed.EdgeTarget(q), $"source of edge {q} for {labeled.Describe()}");
                Assert.True(g.EdgeTarget(p) == transposed.EdgeSource(q), $"target of edge {q} for {labeled.Describe()}");
            }
        }
    }

    [Fact]
    public void CountingSortAndBinarySearchAgree()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyGraph(seed);
            var g = labeled.Graph;
            var biAdjacent = BiAdjacentGraph.Of(g);
            var mapping = Graphs.TransposedEdgeMapping(g);
            for (int p = 0; p < g.EdgeCount; p++)
            {
                int q = biAdjacent.TransposedEdgeIndex(p);
                Assert.True(mapping.Get(q) == p, $"edge {p} for {labeled.Describe()}");
            }
        }
    }

    [Fact]
    public void TranslatingTwiceReturnsTheOriginalIndex()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyGraph(seed);
            var biAdjacent = BiAdjacentGraph.Of(labeled.Graph);
            var transposed = biAdjacent.Transposed();
            for (int p = 0; p < biAdjacent.EdgeCount; p++)
            {
                Assert.True(transposed.TransposedEdgeIndex(biAdjacent.TransposedEdgeIndex(p)) == p,
                    $"edge {p} for {labeled.Describe()}");
            }
        }
    }

    [Fact]
    public void UniAndBiAdjacentInputYieldTheSameMapping()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyGraph(seed);
            var g = labeled.Graph;
            var fromUniAdjacent = Graphs.TransposedEdgeMapping(g);
            var fromBiAdjacent = Graphs.TransposedEdgeMapping(BiAdjacentGraph.Of(g));
            for (int q = 0; q < g.EdgeCount; q++)
            {
                Assert.True(fromBiAdjacent.Get(q) == fromUniAdjacent.Get(q), $"edge {q} for {labeled.Describe()}");
            }
        }
    }
}
