namespace Graphity.Core.Tests;

/// <summary>
/// Cross-validates the shape predicates of <see cref="Graphs"/> against independent references.
/// </summary>
/// <remarks>
/// The directed predicates: <c>IsDag</c> against <see cref="NaiveOracles.HasCycle"/> (Kahn's
/// removal, in place of JGraphT's cycle detector); <c>IsForest</c>, <c>IsTree</c>, <c>IsPath</c>
/// against their structural definitions from plain arrays. The undirected predicates against
/// <see cref="NaiveOracles"/> (breadth-first two-coloring, edge and component counts, in place of
/// JGraphT's <c>GraphTests</c> and connectivity inspector), which read the graph as a simple
/// undirected graph: reciprocal pairs collapse, self-loops are dropped. Since that reading drops
/// self-loops, graphs holding one are excluded from the bipartiteness comparison, as in Java.
/// </remarks>
public sealed class GraphsFuzzTests
{
    private const int Iterations = 100;
    private const long BaseSeed = 0x15DA6EDL;

    [Fact]
    public void IsDagAgreesWithTheCycleOracle()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            Assert.True(Graphs.IsDag(g) == !NaiveOracles.HasCycle(g), $"isDag mismatch for {labeled.Describe()}");
        }
    }

    [Fact]
    public void IsForestAgreesWithDefinition()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            Assert.True(Graphs.IsForest(g) == IsForestReference(g), $"isForest mismatch for {labeled.Describe()}");
        }
    }

    [Fact]
    public void IsTreeAgreesWithDefinition()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            Assert.True(Graphs.IsTree(g) == IsTreeReference(g), $"isTree mismatch for {labeled.Describe()}");
        }
    }

    [Fact]
    public void IsPathAgreesWithDefinition()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            Assert.True(Graphs.IsPath(g) == IsPathReference(g), $"isPath mismatch for {labeled.Describe()}");
        }
    }

    [Fact]
    public void ShapeHierarchyHolds()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            if (Graphs.IsPath(g))
            {
                Assert.True(Graphs.IsTree(g), $"isPath => isTree violated for {labeled.Describe()}");
            }
            if (Graphs.IsTree(g))
            {
                Assert.True(Graphs.IsForest(g), $"isTree => isForest violated for {labeled.Describe()}");
            }
            if (Graphs.IsForest(g))
            {
                Assert.True(Graphs.IsDag(g), $"isForest => isDag violated for {labeled.Describe()}");
            }
        }
    }

    // ---- Undirected predicates ----

    [Fact]
    public void IsBipartiteAgreesWithTheOracle()
    {
        int bipartite = 0;
        int notBipartite = 0;
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyUndirectedShape(BaseSeed + i);
            var g = labeled.Graph;
            if (ContainsSelfLoop(g))
            {
                continue; // the oracle reads self-loops away, see the class comment
            }
            bool ours = Graphs.IsBipartite(g);
            Assert.True(ours == NaiveOracles.IsBipartite(g), $"isBipartite mismatch for {labeled.Describe()}");
            if (ours)
            {
                bipartite++;
            }
            else
            {
                notBipartite++;
            }
        }
        // Without both outcomes the comparison above would pass on agreement about nothing.
        Assert.True(bipartite > 0, "no bipartite graph was generated");
        Assert.True(notBipartite > 0, "no non-bipartite graph was generated");
    }

    [Fact]
    public void IsUndirectedTreeAgreesWithTheOracle()
    {
        int trees = 0;
        int nonTrees = 0;
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyUndirectedShape(BaseSeed + i);
            var g = labeled.Graph;
            bool ours = Graphs.IsUndirectedTree(g);
            Assert.True(ours == NaiveOracles.IsUndirectedTree(g), $"isUndirectedTree mismatch for {labeled.Describe()}");
            if (ours)
            {
                trees++;
            }
            else
            {
                nonTrees++;
            }
        }
        Assert.True(trees > 0, "no undirected tree was generated");
        Assert.True(nonTrees > 0, "no non-tree was generated");
    }

    [Fact]
    public void IsUndirectedForestAgreesWithTheOracle()
    {
        int forests = 0;
        int nonForests = 0;
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyUndirectedShape(BaseSeed + i);
            var g = labeled.Graph;
            bool ours = Graphs.IsUndirectedForest(g);
            Assert.True(ours == NaiveOracles.IsUndirectedForest(g), $"isUndirectedForest mismatch for {labeled.Describe()}");
            if (ours)
            {
                forests++;
            }
            else
            {
                nonForests++;
            }
        }
        Assert.True(forests > 0, "no undirected forest was generated");
        Assert.True(nonForests > 0, "no graph with an undirected cycle was generated");
    }

    [Fact]
    public void IsStronglyConnectedAgreesWithSccs()
    {
        int connected = 0;
        int notConnected = 0;
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            bool ours = Graphs.IsStronglyConnected(g);
            Assert.True(ours == Components.SccsOf(g).IsStronglyConnected,
                $"isStronglyConnected mismatch vs SccsOf for {labeled.Describe()}");
            var symmetric = RandomGraphs.Symmetrized(g);
            Assert.True(Graphs.IsStronglyConnected(symmetric) == Graphs.IsWeaklyConnected(g),
                $"symmetrized {labeled.Describe()}: strongly connected exactly if weakly");
            if (ours)
            {
                connected++;
            }
            else
            {
                notConnected++;
            }
        }
        Assert.True(notConnected > 0, "no graph with several components was generated");
        var random = new JavaRandom(BaseSeed);
        for (int i = 0; i < Iterations; i++)
        {
            var g = RandomGraphs.Gnp(2 + random.NextInt(10), 0.3 + (random.NextDouble() * 0.5), random.NextLong());
            bool ours = Graphs.IsStronglyConnected(g);
            Assert.True(ours == Components.SccsOf(g).IsStronglyConnected, $"dense gnp {i}");
            if (ours)
            {
                connected++;
            }
        }
        Assert.True(connected > 0, "no strongly connected graph was generated");
    }

    [Fact]
    public void IsWeaklyConnectedAgreesWithTheOracle()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyUndirectedShape(BaseSeed + i);
            var g = labeled.Graph;
            Assert.True(Graphs.IsWeaklyConnected(g) == NaiveOracles.IsConnected(g),
                $"isWeaklyConnected mismatch for {labeled.Describe()}");
        }
    }

    [Fact]
    public void IsWeaklyConnectedAgreesWithWeaklyConnectedComponents()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;
            Assert.True(Graphs.IsWeaklyConnected(g) == (Components.WccsOf(g).GroupCount <= 1),
                $"isWeaklyConnected disagrees with WccsOf for {labeled.Describe()}");
        }
    }

    [Fact]
    public void UndirectedShapeHierarchyHolds()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyUndirectedShape(BaseSeed + i);
            var g = labeled.Graph;
            if (Graphs.IsTree(g))
            {
                Assert.True(Graphs.IsUndirectedTree(g), $"isTree => isUndirectedTree violated for {labeled.Describe()}");
            }
            if (Graphs.IsForest(g))
            {
                Assert.True(Graphs.IsUndirectedForest(g), $"isForest => isUndirectedForest violated for {labeled.Describe()}");
            }
            if (Graphs.IsUndirectedTree(g))
            {
                Assert.True(Graphs.IsUndirectedForest(g), $"isUndirectedTree => isUndirectedForest violated for {labeled.Describe()}");
                Assert.True(Graphs.IsWeaklyConnected(g), $"isUndirectedTree => isWeaklyConnected violated for {labeled.Describe()}");
                if (!ContainsSelfLoop(g))
                {
                    // Holds only without self-loops: the tree ignores them, IsBipartite does not.
                    Assert.True(Graphs.IsBipartite(g), $"isUndirectedTree => isBipartite violated for {labeled.Describe()}");
                }
            }
        }
    }

    /// <summary>A forest: a DAG where every node has in-degree &lt;= 1.</summary>
    private static bool IsForestReference(Graph graph)
    {
        if (!Graphs.IsDag(graph))
        {
            return false;
        }
        return InDegrees(graph).All(d => d <= 1);
    }

    /// <summary>A tree: a forest with exactly one root, or an empty graph.</summary>
    private static bool IsTreeReference(Graph graph)
    {
        if (!IsForestReference(graph))
        {
            return false;
        }
        if (graph.NodeCount == 0)
        {
            return true;
        }
        return InDegrees(graph).Count(d => d == 0) == 1;
    }

    /// <summary>A path: a tree where every node has out-degree &lt;= 1.</summary>
    private static bool IsPathReference(Graph graph)
    {
        if (!IsTreeReference(graph))
        {
            return false;
        }
        for (int v = 0; v < graph.NodeCount; v++)
        {
            if (graph.OutEdgeEnd(v) - graph.OutEdgeStart(v) > 1)
            {
                return false;
            }
        }
        return true;
    }

    private static bool ContainsSelfLoop(Graph graph)
    {
        for (int v = 0; v < graph.NodeCount; v++)
        {
            if (graph.ContainsSelfLoop(v))
            {
                return true;
            }
        }
        return false;
    }

    private static int[] InDegrees(Graph graph)
    {
        var inDeg = new int[graph.NodeCount];
        for (int u = 0; u < graph.NodeCount; u++)
        {
            for (int ei = graph.OutEdgeStart(u); ei < graph.OutEdgeEnd(u); ei++)
            {
                inDeg[graph.EdgeTarget(ei)]++;
            }
        }
        return inDeg;
    }
}
