using System.Runtime.InteropServices;

namespace Graphity.Core.Tests;

/// <summary>
/// Cross-validates the <see cref="GraphBuilder"/> convenience and batch methods against the
/// primitives they are sugar for: the oracle is always a second construction path that must yield an
/// equal graph. Java's <c>int[]</c> and <c>IntList</c> forms map to an array and a span over a list.
/// </summary>
public sealed class GraphBuilderFuzzTests
{
    private const int Iterations = 200;

    private sealed record EdgeList(int NodeCount, int[] Sources, int[] Targets)
    {
        public int Size => Sources.Length;

        /// <summary>Node count required when nodes are created on reference rather than up front.</summary>
        public int ReferencedNodeCount()
        {
            int max = -1;
            for (int i = 0; i < Size; i++)
            {
                max = Math.Max(max, Math.Max(Sources[i], Targets[i]));
            }
            return max + 1;
        }

        public string Describe() => $"n={NodeCount}, m={Size}";
    }

    [Fact]
    public void CapacityHintsDoNotChangeTheResult()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var edges = RandomEdges(seed);
            var expected = EdgeByEdge(edges);
            foreach (int hint in new[] { 0, edges.Size / 2, edges.Size, (2 * edges.Size) + 8 })
            {
                var b = new GraphBuilder();
                b.EnsureEdgeCapacity(hint);
                b.AddNodes(edges.NodeCount);
                for (int i = 0; i < edges.Size; i++)
                {
                    b.AddEdge(edges.Sources[i], edges.Targets[i]);
                }
                Assert.True(expected.Equals(b.Build()), $"capacity hint {hint} for {edges.Describe()}");
            }
        }
    }

    [Fact]
    public void CapacityHintAppliedMidConstructionKeepsAllEdges()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var edges = RandomEdges(seed);
            int half = edges.Size / 2;
            var b = new GraphBuilder();
            b.AddNodes(edges.NodeCount);
            for (int i = 0; i < half; i++)
            {
                b.AddEdge(edges.Sources[i], edges.Targets[i]);
            }
            b.EnsureEdgeCapacity(edges.Size);
            for (int i = half; i < edges.Size; i++)
            {
                b.AddEdge(edges.Sources[i], edges.Targets[i]);
            }
            Assert.True(EdgeByEdge(edges).Equals(b.Build()), $"mid-construction hint for {edges.Describe()}");
        }
    }

    [Fact]
    public void BatchFormsMatchEdgeByEdge()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var edges = RandomEdges(seed);
            var expected = EdgeByEdge(edges);
            var bySource = Group(edges, true, edges.NodeCount);
            var byTarget = Group(edges, false, edges.NodeCount);

            var fanOutArray = new GraphBuilder();
            fanOutArray.AddNodes(edges.NodeCount);
            var fanOutList = new GraphBuilder();
            fanOutList.AddNodes(edges.NodeCount);
            var fanInArray = new GraphBuilder();
            fanInArray.AddNodes(edges.NodeCount);
            var fanInList = new GraphBuilder();
            fanInList.AddNodes(edges.NodeCount);
            for (int node = 0; node < edges.NodeCount; node++)
            {
                fanOutArray.AddOutEdges(node, bySource[node].ToArray());
                fanOutList.AddOutEdges(node, CollectionsMarshal.AsSpan(bySource[node]));
                fanInArray.AddInEdges(node, byTarget[node].ToArray());
                fanInList.AddInEdges(node, CollectionsMarshal.AsSpan(byTarget[node]));
            }

            var pairs = new GraphBuilder();
            pairs.AddNodes(edges.NodeCount);
            pairs.AddEdgePairs(Interleave(edges));

            var edgeList = new GraphBuilder();
            edgeList.AddNodes(edges.NodeCount);
            edgeList.AddEdges(AsEdges(edges));

            Assert.True(expected.Equals(fanOutArray.Build()), $"fan-out from int[] for {edges.Describe()}");
            Assert.True(expected.Equals(fanOutList.Build()), $"fan-out from a list for {edges.Describe()}");
            Assert.True(expected.Equals(fanInArray.Build()), $"fan-in from int[] for {edges.Describe()}");
            Assert.True(expected.Equals(fanInList.Build()), $"fan-in from a list for {edges.Describe()}");
            Assert.True(expected.Equals(pairs.Build()), $"edge pairs for {edges.Describe()}");
            Assert.True(expected.Equals(edgeList.Build()), $"Edges for {edges.Describe()}");
        }
    }

    /// <summary>The random edges as an <see cref="Edges"/> list; the builder ignores the edge indices.</summary>
    private static Edges AsEdges(EdgeList edges) =>
        Edges.Wrap((int[])edges.Sources.Clone(), (int[])edges.Targets.Clone(), new int[edges.Size]);

    [Fact]
    public void AndNodesTwinsMatchPreallocatedConstruction()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var edges = RandomEdges(seed);
            int referenced = edges.ReferencedNodeCount();

            var expected = new GraphBuilder();
            expected.AddNodes(referenced);
            for (int i = 0; i < edges.Size; i++)
            {
                expected.AddEdge(edges.Sources[i], edges.Targets[i]);
            }
            var reference = expected.Build();

            var single = new GraphBuilder();
            for (int i = 0; i < edges.Size; i++)
            {
                single.AddEdgeAndNodes(edges.Sources[i], edges.Targets[i]);
            }

            var pairs = new GraphBuilder();
            pairs.AddEdgePairsAndNodes(Interleave(edges));

            var edgeList = new GraphBuilder();
            edgeList.AddEdgesAndNodes(AsEdges(edges));

            var bySource = Group(edges, true, referenced);
            var fanOutArray = new GraphBuilder();
            var fanOutList = new GraphBuilder();
            for (int node = 0; node < referenced; node++)
            {
                fanOutArray.AddOutEdgesAndNodes(node, bySource[node].ToArray());
                fanOutList.AddOutEdgesAndNodes(node, CollectionsMarshal.AsSpan(bySource[node]));
            }

            var byTarget = Group(edges, false, referenced);
            var fanInArray = new GraphBuilder();
            var fanInList = new GraphBuilder();
            for (int node = 0; node < referenced; node++)
            {
                fanInArray.AddInEdgesAndNodes(node, byTarget[node].ToArray());
                fanInList.AddInEdgesAndNodes(node, CollectionsMarshal.AsSpan(byTarget[node]));
            }

            Assert.True(reference.Equals(single.Build()), $"AddEdgeAndNodes for {edges.Describe()}");
            Assert.True(reference.Equals(pairs.Build()), $"AddEdgePairsAndNodes for {edges.Describe()}");
            Assert.True(reference.Equals(edgeList.Build()), $"AddEdgesAndNodes from Edges for {edges.Describe()}");
            Assert.True(reference.Equals(fanOutArray.Build()), $"AddOutEdgesAndNodes from int[] for {edges.Describe()}");
            Assert.True(reference.Equals(fanOutList.Build()), $"AddOutEdgesAndNodes from a list for {edges.Describe()}");
            Assert.True(reference.Equals(fanInArray.Build()), $"AddInEdgesAndNodes from int[] for {edges.Describe()}");
            Assert.True(reference.Equals(fanInList.Build()), $"AddInEdgesAndNodes from a list for {edges.Describe()}");
        }
    }

    [Fact]
    public void PathsMatchExplicitEdges()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var rnd = new JavaRandom((seed * 977) + 13);
            int n = 1 + rnd.NextInt(40);
            int length = rnd.NextInt(12);
            var nodes = new int[length];
            for (int i = 0; i < length; i++)
            {
                nodes[i] = rnd.NextInt(n);
            }

            var explicitEdges = new GraphBuilder();
            explicitEdges.AddNodes(n);
            for (int i = 1; i < length; i++)
            {
                explicitEdges.AddEdge(nodes[i - 1], nodes[i]);
            }
            var reference = explicitEdges.Build();

            var viaPath = new GraphBuilder();
            viaPath.AddNodes(n);
            viaPath.AddPath(nodes);

            var viaPathList = new GraphBuilder();
            viaPathList.AddNodes(n);
            viaPathList.AddPath(CollectionsMarshal.AsSpan(new List<int>(nodes)));

            Assert.True(reference.Equals(viaPath.Build()), $"AddPath, seed {seed}");
            Assert.True(reference.Equals(viaPathList.Build()), $"AddPath from a list, seed {seed}");

            int referenced = 0;
            foreach (int node in nodes)
            {
                referenced = Math.Max(referenced, node + 1);
            }
            var grownReference = new GraphBuilder();
            grownReference.AddNodes(referenced);
            grownReference.AddPath(nodes);

            var viaPathAndNodes = new GraphBuilder();
            viaPathAndNodes.AddPathAndNodes(nodes);

            var viaPathAndNodesList = new GraphBuilder();
            viaPathAndNodesList.AddPathAndNodes(CollectionsMarshal.AsSpan(new List<int>(nodes)));

            Assert.True(grownReference.Build().Equals(viaPathAndNodes.Build()), $"AddPathAndNodes, seed {seed}");
            Assert.True(grownReference.Build().Equals(viaPathAndNodesList.Build()), $"AddPathAndNodes from a list, seed {seed}");
        }
    }

    [Fact]
    public void ChildAndParentHelpersMatchManualConstruction()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var helpers = new GraphBuilder();
            var manual = new GraphBuilder();
            helpers.AddNode();
            manual.AddNode();

            var rnd = new JavaRandom((seed * 31) + 5);
            int rounds = 1 + rnd.NextInt(12);
            int nodeCount = 1;

            for (int round = 0; round < rounds; round++)
            {
                int anchor = rnd.NextInt(nodeCount);
                int count = rnd.NextInt(4);
                bool downwards = rnd.NextBoolean();

                int viaHelper;
                int viaManual;
                if (count == 1 && downwards)
                {
                    viaHelper = helpers.AddChild(anchor);
                    viaManual = manual.AddNode();
                    manual.AddEdge(anchor, viaManual);
                }
                else if (count == 1)
                {
                    viaHelper = helpers.AddParent(anchor);
                    viaManual = manual.AddNode();
                    manual.AddEdge(viaManual, anchor);
                }
                else if (downwards)
                {
                    viaHelper = helpers.AddChildren(anchor, count);
                    viaManual = manual.AddNodes(count);
                    for (int i = 0; i < count; i++)
                    {
                        manual.AddEdge(anchor, viaManual + i);
                    }
                }
                else
                {
                    viaHelper = helpers.AddParents(anchor, count);
                    viaManual = manual.AddNodes(count);
                    for (int i = 0; i < count; i++)
                    {
                        manual.AddEdge(viaManual + i, anchor);
                    }
                }

                Assert.True(viaHelper == viaManual, $"first returned ID, seed {seed} round {round}");
                nodeCount += count;
            }

            Assert.True(manual.Build().Equals(helpers.Build()), $"child/parent helpers, seed {seed}");
        }
    }

    /// <summary>
    /// Every added edge, duplicates included, is found by <see cref="Graph.EdgeIndexOf"/> at an index
    /// that holds exactly that edge, and distinct edges never share an index.
    /// </summary>
    [Fact]
    public void EdgeIndexOfTranslatesEveryInsertion()
    {
        int duplicatesSeen = 0;
        for (long seed = 0; seed < Iterations; seed++)
        {
            var edges = RandomEdges(seed);
            var g = EdgeByEdge(edges);
            var edgeOfInsertion = new int[edges.Size];
            for (int k = 0; k < edges.Size; k++)
            {
                int s = edges.Sources[k];
                int t = edges.Targets[k];
                int e = g.EdgeIndexOf(s, t);
                edgeOfInsertion[k] = e;
                Assert.True(e >= 0 && e < g.EdgeCount, $"insertion {k}, {edges.Describe()}");
                Assert.Equal(s, g.EdgeSource(e));
                Assert.Equal(t, g.EdgeTarget(e));
            }
            for (int a = 0; a < edges.Size; a++)
            {
                for (int b = a + 1; b < edges.Size; b++)
                {
                    bool same = edges.Sources[a] == edges.Sources[b] && edges.Targets[a] == edges.Targets[b];
                    if (same)
                    {
                        duplicatesSeen++;
                    }
                    Assert.True((edgeOfInsertion[a] == edgeOfInsertion[b]) == same,
                        $"insertions {a} and {b}, {edges.Describe()}");
                }
            }
        }
        Assert.True(duplicatesSeen > 50, "duplicate insertions compared");
    }

    private static EdgeList RandomEdges(long seed)
    {
        var rnd = new JavaRandom((seed * 1_000_003L) + 17);
        int n = 1 + rnd.NextInt(60);
        int m = rnd.NextInt(3 * n);
        var sources = new int[m];
        var targets = new int[m];
        for (int i = 0; i < m; i++)
        {
            sources[i] = rnd.NextInt(n);
            targets[i] = rnd.NextInt(n);
        }
        return new EdgeList(n, sources, targets);
    }

    private static Graph EdgeByEdge(EdgeList edges)
    {
        var b = new GraphBuilder();
        b.AddNodes(edges.NodeCount);
        for (int i = 0; i < edges.Size; i++)
        {
            b.AddEdge(edges.Sources[i], edges.Targets[i]);
        }
        return b.Build();
    }

    /// <summary>Buckets the edge list by source (or by target), one list per node.</summary>
    private static List<int>[] Group(EdgeList edges, bool bySource, int nodeCount)
    {
        var groups = new List<int>[nodeCount];
        for (int node = 0; node < nodeCount; node++)
        {
            groups[node] = [];
        }
        for (int i = 0; i < edges.Size; i++)
        {
            int key = bySource ? edges.Sources[i] : edges.Targets[i];
            int value = bySource ? edges.Targets[i] : edges.Sources[i];
            groups[key].Add(value);
        }
        return groups;
    }

    private static int[] Interleave(EdgeList edges)
    {
        var flat = new int[2 * edges.Size];
        for (int i = 0; i < edges.Size; i++)
        {
            flat[2 * i] = edges.Sources[i];
            flat[(2 * i) + 1] = edges.Targets[i];
        }
        return flat;
    }
}
