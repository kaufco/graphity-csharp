using System.Collections;

namespace Graphity.Core.Tests;

/// <summary>
/// The null contract of the core: a <see langword="null"/> argument where the type of a parameter
/// does not allow one throws <see cref="ArgumentNullException"/>, at every public entry point.
/// </summary>
/// <remarks>
/// Not in Java, whose library checks nothing at run time and leaves what a null argument does
/// unspecified.
/// </remarks>
public sealed class NullArgumentTests
{
    private static readonly BiAdjacentGraph Sample = BiAdjacentGraph.Of(TestGraphs.Of(3, 0, 1, 1, 2, 2, 0));

    private static object SampleOf(Type type)
    {
        if (type == typeof(Graph) || type == typeof(BiAdjacentGraph))
        {
            return Sample;
        }
        if (type == typeof(Dominators))
        {
            return Dominators.Of(Sample, 0);
        }
        if (type == typeof(NodeMapping) || type == typeof(IndexMapping))
        {
            return NodeMapping.Of([0, 1, 2]);
        }
        if (type == typeof(EdgeMapping))
        {
            return Graphs.TransposedEdgeMapping(Sample);
        }
        if (type == typeof(NodePartition) || type == typeof(IndexPartition))
        {
            return Components.WccsOf(Sample);
        }
        if (type == typeof(Sccs))
        {
            return Components.SccsOf(Sample);
        }
        if (type == typeof(EdgePartition))
        {
            return Components.BiconnectivityOf(Sample).Blocks;
        }
        if (type == typeof(Ancestry) || type == typeof(PreorderAncestry))
        {
            return PreorderAncestry.Of(Ancestry.Of(TestGraphs.Of(3, 0, 1, 0, 2)));
        }
        if (type == typeof(EdgeTypes))
        {
            return EdgeTypes.Of(Sample);
        }
        if (type == typeof(Edges))
        {
            return BackEdges.Of(Sample);
        }
        if (type == typeof(GraphBuilder))
        {
            return new GraphBuilder();
        }
        if (type == typeof(Stream))
        {
            return new MemoryStream();
        }
        if (type == typeof(BitArray))
        {
            return new BitArray(3);
        }
        if (type == typeof(int[]))
        {
            return new[] { 1, 2 };
        }
        if (type == typeof(EdgeConsumer))
        {
            return new EdgeConsumer((_, _, _) => { });
        }
        throw new InvalidOperationException($"no sample of {type}");
    }

    [Fact]
    public void EveryPublicEntryPointRejectsNull()
    {
        var result = NullArguments.Check(typeof(Graph).Assembly, SampleOf);
        Assert.Empty(result.Failures);
        Assert.True(result.Checked >= 54, $"only {result.Checked} parameters checked");
        // The members that take or return a span cannot be called by reflection; the test below
        // covers them.
        Assert.Equal(
            [
                "BfsDistances.Of(Graph, ReadOnlySpan`1)",
                "Csr.InAdjacency(BiAdjacentGraph)",
                "Csr.InAdjacencyStart(BiAdjacentGraph)",
                "Csr.OutAdjacency(Graph)",
                "Csr.OutAdjacencyStart(Graph)",
                "Dominators.Of(BiAdjacentGraph, ReadOnlySpan`1)",
                "Graphs.ReachableFrom(Graph, ReadOnlySpan`1)",
                "IndexPartition.Reduce(ReadOnlySpan`1, Span`1, Func`3)",
                "IndexPartition.Reduce(ReadOnlySpan`1, T, Func`3)",
                "NodePartition.Of(Graph, ReadOnlySpan`1, Int32)",
            ],
            result.NotCallable.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EntryPointsWithASpanRejectNull()
    {
        AssertNull("graph", () => Csr.OutAdjacencyStart(null!));
        AssertNull("graph", () => Csr.OutAdjacency(null!));
        AssertNull("graph", () => Csr.InAdjacencyStart(null!));
        AssertNull("graph", () => Csr.InAdjacency(null!));
        AssertNull("graph", () => BfsDistances.Of(null!, [0]));
        AssertNull("graph", () => Dominators.Of(null!, [0]));
        AssertNull("graph", () => Graphs.ReachableFrom(null!, [0]));
        AssertNull("graph", () => NodePartition.Of(null!, [0], 1));
        var partition = Components.WccsOf(Sample);
        AssertNull("combine", () => partition.Reduce<int>([1, 2, 3], 0, null!));
        AssertNull("combine", () => partition.Reduce<string>(["a", "b", "c"], new string[1], null!));
    }

    [Fact]
    public void TheConstructorsOfTheTraversersRejectNull()
    {
        AssertNull("graph", () => new RecordingDfsTraverser(null!));
        AssertNull("graph", () => new RecordingFrontierTraverser(null!));
        AssertNull("graph", () => new EdgeTraverser(null!));
    }

    private static void AssertNull(string parameter, Action call) =>
        Assert.Equal(parameter, Assert.Throws<ArgumentNullException>(call).ParamName);

    private sealed class EdgeTraverser(Graph graph) : DfsEdgeTraverser(graph);
}
