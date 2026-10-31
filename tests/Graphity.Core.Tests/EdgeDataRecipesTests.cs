using System.Collections;

namespace Graphity.Core.Tests;

/// <summary>Recipes for edge data - visiting, summing, collecting and carrying it along - checked independently.</summary>
public sealed class EdgeDataRecipesTests
{
    private static double[] WeightsOf(Graph graph)
    {
        var weights = new double[graph.EdgeCount];
        for (int e = 0; e < weights.Length; e++)
        {
            weights[e] = 1 + (e % 7);
        }
        return weights;
    }

    [Fact]
    public void VisitEveryEdgeWithItsData()
    {
        Graph graph = RandomGraphs.Gnp(40, 0.1, 1);
        double[] weights = WeightsOf(graph);

        double total = 0;
        graph.ForEachEdge((source, target, edgeIndex) => total += weights[edgeIndex]);

        Assert.Equal(weights.Sum(), total);
    }

    [Fact]
    public void SumTheWeightsBehindEachContractedEdgeAndCollectTheEdgesBetweenTwoGroups()
    {
        Graph graph = RandomGraphs.Gnp(60, 0.03, 2);
        double[] weights = WeightsOf(graph);
        Sccs partition = Components.SccsOf(graph);

        Graph condensed = partition.Condense();
        EdgePartition provenance = Graphs.EdgePartition(condensed, partition);
        double[] weightPerCondensedEdge = provenance.Reduce<double>(weights, 0.0, (a, b) => a + b);

        Assert.True(partition.SccCount > 10);
        Assert.True(condensed.EdgeCount > 10);

        for (int g = 0; g < partition.SccCount; g++)
        {
            for (int h = 0; h < partition.SccCount; h++)
            {
                int condensedEdge = condensed.EdgeIndexOf(g, h); // -1 if no edge runs from g to h
                SortedIntView between = condensedEdge < 0 ? SortedIntView.Empty : provenance.EdgesOf(condensedEdge);
                double betweenWeight = 0;
                foreach (int edgeIndex in between)
                {
                    betweenWeight += weights[edgeIndex]; // between holds edge indices of graph
                }

                double sum = 0;
                int count = 0;
                for (int e = 0; e < graph.EdgeCount; e++)
                {
                    if (g != h && partition.SccOf(graph.EdgeSource(e)) == g && partition.SccOf(graph.EdgeTarget(e)) == h)
                    {
                        sum += weights[e];
                        count++;
                    }
                }
                Assert.True(between.Count == count, $"{g} -> {h}");
                Assert.Equal(sum, betweenWeight);
                if (condensedEdge >= 0)
                {
                    Assert.Equal(sum, weightPerCondensedEdge[condensedEdge]);
                }
                foreach (int edgeIndex in between)
                {
                    Assert.Equal(g, partition.SccOf(graph.EdgeSource(edgeIndex)));
                    Assert.Equal(h, partition.SccOf(graph.EdgeTarget(edgeIndex)));
                }
            }
        }
        // Edges inside one group are in no condensed edge.
        for (int e = 0; e < graph.EdgeCount; e++)
        {
            bool inside = partition.SccOf(graph.EdgeSource(e)) == partition.SccOf(graph.EdgeTarget(e));
            Assert.Equal(inside, provenance.Get(e) < 0);
        }
    }

    [Fact]
    public void CarryEdgeDataAcrossASubgraph()
    {
        Graph graph = RandomGraphs.Gnp(50, 0.1, 3);
        double[] weights = WeightsOf(graph);
        var names = new string?[graph.NodeCount];
        for (int v = 0; v < names.Length; v++)
        {
            names[v] = "v" + v;
        }
        var selected = new BitArray(graph.NodeCount);
        for (int v = 0; v < graph.NodeCount; v += 2)
        {
            selected[v] = true;
        }

        NodeMapping keep = NodeMapping.ForSubgraph(selected);
        Graph sub = Graphs.Remapped(graph, keep);
        EdgeMapping subToGraph = Graphs.EdgeMapping(sub, graph, keep);
        double[] subWeights = subToGraph.Gather<double>(weights);
        var subNames = new string?[sub.NodeCount];
        keep.Gather<string?>(names, subNames);

        sub.ForEachEdge((source, target, edgeIndex) =>
        {
            int oldEdge = graph.EdgeIndexOf(keep.Get(source), keep.Get(target));
            Assert.Equal(weights[oldEdge], subWeights[edgeIndex]);
        });
        for (int v = 0; v < sub.NodeCount; v++)
        {
            Assert.Equal("v" + keep.Get(v), subNames[v]);
        }
    }

    [Fact]
    public void CarryEdgeDataAcrossATranspose()
    {
        Graph graph = RandomGraphs.Gnp(30, 0.1, 4);
        double[] weights = WeightsOf(graph);

        Graph transpose = BiAdjacentGraph.Of(graph).Transposed();
        EdgeMapping newToOld = Graphs.TransposedEdgeMapping(graph);
        double[] transposedWeights = newToOld.Gather<double>(weights);

        transpose.ForEachEdge((source, target, edgeIndex) =>
            Assert.Equal(weights[graph.EdgeIndexOf(target, source)], transposedWeights[edgeIndex]));
    }
}
