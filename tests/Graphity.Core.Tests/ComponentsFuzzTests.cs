namespace Graphity.Core.Tests;

/// <summary>
/// Cross-validates <see cref="Components.SccsOf"/> and <see cref="Components.WccsOf"/> against the
/// naive oracles (in Java: JGraphT's Kosaraju and connectivity inspectors). For every random graph,
/// both must produce the same partition.
/// </summary>
public sealed class ComponentsFuzzTests
{
    private const int Iterations = 100;
    private const long BaseSeed = 0xC0FFEEL;

    [Fact]
    public void SccsAgreeWithNaiveOracle()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;

            var ourSccs = Components.SccsOf(g);
            var ourPartition = CollectPartition(ourSccs, g.NodeCount);
            var theirPartition = NaiveOracles.StronglyConnectedSets(g);

            Assert.True(theirPartition.SetEquals(ourPartition), $"SCC partition mismatch for {labeled.Describe()}");
            AssertReverseTopoInvariant(labeled, g, ourSccs);
        }
    }

    [Fact]
    public void WccsAgreeWithNaiveOracle()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            var g = labeled.Graph;

            NodePartition ourWccs = Components.WccsOf(g);
            var ourPartition = CollectPartition(ourWccs, g.NodeCount);
            var theirPartition = NaiveOracles.WeaklyConnectedSets(g);

            Assert.True(theirPartition.SetEquals(ourPartition), $"WCC partition mismatch for {labeled.Describe()}");
        }
    }

    private static HashSet<string> CollectPartition(NodePartition partition, int nodeCount)
    {
        var result = new HashSet<string>();
        int total = 0;
        for (int group = 0; group < partition.GroupCount; group++)
        {
            var nodes = partition.NodesOf(group);
            total += nodes.Count;
            result.Add(NaiveOracles.Key(nodes));
        }
        Assert.Equal(nodeCount, total);
        return result;
    }

    private static void AssertReverseTopoInvariant(LabeledGraph labeled, Graph graph, Sccs sccs)
    {
        for (int u = 0; u < graph.NodeCount; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                int su = sccs.Get(u);
                int sv = sccs.Get(v);
                if (su != sv)
                {
                    Assert.True(su > sv,
                        $"reverse-topo invariant violated for {labeled.Describe()}: edge {u} -> {v}, sccId({u})={su}, sccId({v})={sv}");
                }
            }
        }
    }
}
