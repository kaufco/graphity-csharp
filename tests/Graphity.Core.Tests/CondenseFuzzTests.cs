namespace Graphity.Core.Tests;

/// <summary>
/// Cross-validates <see cref="NodePartition.Condense"/> against a naive reference built from
/// per-group sorted sets. Random graphs reach up to 500 groups, so several words of the internal
/// dedup bit set are in play - a stale mark leaking into the next group, or a reset that clears too
/// much, is invisible to the hand-written tests and to structural checks.
/// </summary>
public sealed class CondenseFuzzTests
{
    private const int Iterations = 100;
    private const long BaseSeed = 0xC0DEDA7AL;

    [Fact]
    public void CondenseAgreesWithNaiveReference()
    {
        int maxGroups = 0;
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + i);
            Graph g = labeled.Graph;
            Sccs sccs = Components.SccsOf(g);
            Graph condensed = sccs.Condense();
            maxGroups = Math.Max(maxGroups, sccs.GroupCount);

            Assert.Equal(sccs.GroupCount, condensed.NodeCount);
            var expected = NaiveCondensation(g, sccs);
            for (int group = 0; group < sccs.GroupCount; group++)
            {
                Assert.True(expected[group].SequenceEqual(condensed.OutNodes(group).ToArray()),
                    $"condensation adjacency of group {group} for {labeled.Describe()}");
            }
        }
        Assert.True(maxGroups > 64, "test would not exercise the multi-word dedup path");
    }

    /// <summary>
    /// Partitions chosen at random rather than by an algorithm: nodes in no group, empty groups and
    /// groups that are not connected at all, as <see cref="NodePartition.Of"/> allows them. Checks the
    /// members, the condensation and the edge provenance from <see cref="Graphs.EdgePartition"/>.
    /// </summary>
    [Fact]
    public void CallerPartitionsAgreeWithNaiveReference()
    {
        int partialSeen = 0;
        int crossEdgesSeen = 0;
        int maxGroups = 0;
        for (int i = 0; i < Iterations; i++)
        {
            var labeled = RandomGraphs.AnyGraph(BaseSeed + (7919L * i));
            Graph g = labeled.Graph;
            int n = g.NodeCount;
            var rnd = new JavaRandom(BaseSeed + i);
            int groupCount = rnd.NextInt(Math.Max(1, n / 2) + 2);
            maxGroups = Math.Max(maxGroups, groupCount);
            var groupOf = new int[n];
            for (int v = 0; v < n; v++)
            {
                groupOf[v] = groupCount == 0 || rnd.NextInt(5) == 0 ? -1 : rnd.NextInt(groupCount);
            }
            var partition = NodePartition.Of(g, groupOf, groupCount);
            string describe = labeled.Describe() + ", groups=" + groupCount;
            if (partition.IsPartial)
            {
                partialSeen++;
            }

            for (int group = 0; group < groupCount; group++)
            {
                var expectedMembers = new List<int>();
                for (int v = 0; v < n; v++)
                {
                    if (groupOf[v] == group)
                    {
                        expectedMembers.Add(v);
                    }
                }
                Assert.True(expectedMembers.SequenceEqual(partition.NodesOf(group).ToArray()),
                    $"members of group {group} for {describe}");
            }

            Graph condensed = partition.Condense();
            Assert.Equal(groupCount, condensed.NodeCount);
            var expected = NaiveCondensation(g, partition);
            for (int group = 0; group < groupCount; group++)
            {
                Assert.True(expected[group].SequenceEqual(condensed.OutNodes(group).ToArray()),
                    $"condensation adjacency of group {group} for {describe}");
            }

            var provenance = Graphs.EdgePartition(condensed, partition);
            for (int u = 0; u < n; u++)
            {
                for (int e = g.OutEdgeStart(u); e < g.OutEdgeEnd(u); e++)
                {
                    int su = groupOf[u];
                    int sv = groupOf[g.EdgeTarget(e)];
                    int mapped = provenance.Get(e);
                    if (su < 0 || sv < 0 || su == sv)
                    {
                        Assert.True(mapped == -1, $"edge {e} unmapped for {describe}");
                    }
                    else
                    {
                        crossEdgesSeen++;
                        Assert.Equal(su, condensed.EdgeSource(mapped));
                        Assert.Equal(sv, condensed.EdgeTarget(mapped));
                    }
                }
            }
        }
        Assert.True(partialSeen > 50, $"partitions with nodes in no group: {partialSeen}");
        Assert.True(crossEdgesSeen > 1000, $"edges between groups: {crossEdgesSeen}");
        Assert.True(maxGroups > 64, "test would not exercise the multi-word dedup path");
    }

    private static List<SortedSet<int>> NaiveCondensation(Graph graph, NodePartition partition)
    {
        var targets = new List<SortedSet<int>>(partition.GroupCount);
        for (int group = 0; group < partition.GroupCount; group++)
        {
            targets.Add([]);
        }
        for (int u = 0; u < graph.NodeCount; u++)
        {
            foreach (int v in graph.OutNodes(u))
            {
                int sourceGroup = partition.Get(u);
                int targetGroup = partition.Get(v);
                if (sourceGroup >= 0 && targetGroup >= 0 && sourceGroup != targetGroup)
                {
                    targets[sourceGroup].Add(targetGroup);
                }
            }
        }
        return targets;
    }
}
