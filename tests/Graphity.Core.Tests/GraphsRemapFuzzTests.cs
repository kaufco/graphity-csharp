using System.Collections;

namespace Graphity.Core.Tests;

/// <summary>
/// Cross-validates <see cref="Graphs.Remapped"/> against a set-based oracle: the relabeled edge set
/// must equal the original edge set translated through the mapping. Covers the total-mapping path
/// (permutations) and the filtering path (subgraphs) separately, since they are distinct code paths.
/// </summary>
public sealed class GraphsRemapFuzzTests
{
    private const int Iterations = 300;

    [Fact]
    public void PermutationPreservesTranslatedEdgeSet()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyGraph(seed);
            var g = labeled.Graph;
            int n = g.NodeCount;

            int[] newToOld = RandomPermutation(n, seed);
            var remapped = Graphs.Remapped(g, NodeMapping.ForPermutation(newToOld));

            Assert.True(remapped.NodeCount == n, $"node count for {labeled.Describe()}");
            Assert.True(remapped.EdgeCount == g.EdgeCount, $"permutations drop no edge for {labeled.Describe()}");
            Assert.True(TranslatedEdges(remapped, newToOld).SetEquals(Edges(g)), $"translated edge set for {labeled.Describe()}");
            AssertSortedAdjacency(remapped, labeled.Describe());
        }
    }

    [Fact]
    public void SubgraphKeepsExactlyTheInducedEdges()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyGraph(seed);
            var g = labeled.Graph;
            int n = g.NodeCount;

            var rnd = new JavaRandom((seed * 31) + 7);
            var selected = new BitArray(n);
            for (int node = 0; node < n; node++)
            {
                if (rnd.NextBoolean())
                {
                    selected[node] = true;
                }
            }

            var nm = NodeMapping.ForSubgraph(selected);
            var sub = Graphs.Remapped(g, nm);

            var newToOld = new int[nm.MappedCount];
            for (int newId = 0; newId < nm.MappedCount; newId++)
            {
                newToOld[newId] = nm.Get(newId);
            }

            Assert.True(TranslatedEdges(sub, newToOld).SetEquals(InducedEdges(g, selected)), $"induced edge set for {labeled.Describe()}");
            AssertSortedAdjacency(sub, labeled.Describe());
        }
    }

    private static int[] RandomPermutation(int n, long seed)
    {
        var perm = new int[n];
        for (int i = 0; i < n; i++)
        {
            perm[i] = i;
        }
        var rnd = new JavaRandom(seed);
        for (int i = n - 1; i > 0; i--)
        {
            int j = rnd.NextInt(i + 1);
            (perm[i], perm[j]) = (perm[j], perm[i]);
        }
        return perm;
    }

    private static HashSet<long> Edges(Graph g)
    {
        var set = new HashSet<long>();
        for (int source = 0; source < g.NodeCount; source++)
        {
            foreach (int target in g.OutNodes(source))
            {
                set.Add(((long)source << 32) | (uint)target);
            }
        }
        return set;
    }

    private static HashSet<long> InducedEdges(Graph g, BitArray selected)
    {
        var set = new HashSet<long>();
        for (int source = 0; source < g.NodeCount; source++)
        {
            if (!selected[source])
            {
                continue;
            }
            foreach (int target in g.OutNodes(source))
            {
                if (selected[target])
                {
                    set.Add(((long)source << 32) | (uint)target);
                }
            }
        }
        return set;
    }

    /// <summary>Edge set of <paramref name="g"/>, both endpoints translated back into the original ID space.</summary>
    private static HashSet<long> TranslatedEdges(Graph g, int[] newToOld)
    {
        var set = new HashSet<long>();
        for (int source = 0; source < g.NodeCount; source++)
        {
            foreach (int target in g.OutNodes(source))
            {
                set.Add(((long)newToOld[source] << 32) | (uint)newToOld[target]);
            }
        }
        return set;
    }

    private static void AssertSortedAdjacency(Graph g, string description)
    {
        for (int source = 0; source < g.NodeCount; source++)
        {
            var targets = g.OutNodes(source);
            for (int i = 1; i < targets.Count; i++)
            {
                Assert.True(targets[i] > targets[i - 1], $"adjacency of node {source} must stay sorted for {description}");
            }
        }
    }
}
