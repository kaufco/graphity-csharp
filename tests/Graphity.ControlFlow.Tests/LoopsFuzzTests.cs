namespace Graphity.ControlFlow.Tests;

/// <summary>
/// Cross-validates <see cref="Loops"/> against <see cref="NaiveLoops"/>, which builds the forest
/// literally from its definition by recursive decomposition into strongly connected components - and
/// so also checks that Havlak's algorithm yields that definition.
/// </summary>
public sealed class LoopsFuzzTests
{
    private const int Iterations = 150;

    [Fact]
    public void ForestMatchesTheComponentDefinition()
    {
        int loopsSeen = 0;
        int irreducibleSeen = 0;
        int nestedSeen = 0;
        for (long seed = 0; seed < Iterations; seed++)
        {
            Graph graph;
            int[] entries;
            string describe;
            if (seed % 3 != 2)
            {
                var cfg = ControlFlowGraphs.AnyControlFlowGraph(seed);
                graph = cfg.Graph;
                entries = [0];
                describe = cfg.Describe();
            }
            else
            {
                var labeled = RandomGraphs.AnyGraph(seed);
                graph = labeled.Graph;
                var rnd = new JavaRandom(seed);
                var list = new List<int>();
                for (int i = 0, c = 1 + rnd.NextInt(3); i < c; i++)
                {
                    list.Add(rnd.NextInt(graph.NodeCount));
                }
                entries = [.. list];
                describe = $"{labeled.Describe()}, entries=[{string.Join(", ", entries)}]";
            }
            int n = graph.NodeCount;
            var oracle = new NaiveLoops(graph, entries);
            var loops = Loops.Of(BiAdjacentGraph.Of(graph), entries);

            int expectedCount = 0;
            bool expectedReducible = true;
            var depth = new int[n];
            for (int h = 0; h < n; h++)
            {
                var loop = oracle.LoopOfHeader[h];
                if (loop is null)
                {
                    continue;
                }
                expectedCount++;
                int id = loops.InnermostLoopOf(h);
                Assert.True(id >= 0, $"loop of header {h}, {describe}");
                Assert.True(loops.HeaderOf(id) == h, $"header of loop {id}, {describe}");

                var body = new List<int>();
                for (int x = 0; x < n; x++)
                {
                    if (loop.Body[x])
                    {
                        body.Add(x);
                        depth[x]++;
                    }
                    Assert.True(loops.IsInLoop(x, id) == loop.Body[x], $"{x} in loop of {h}, {describe}");
                }
                int[] nodes = loops.BodyOf(id).ToArray();
                Assert.True(nodes[0] == h, $"body of {h} starts with its header, {describe}");
                Array.Sort(nodes);
                Assert.True(nodes.AsSpan().SequenceEqual(body.ToArray()), $"body of {h}, {describe}");

                var entryList = oracle.EntriesOf(loop);
                Assert.True(loops.EntriesOf(id).AsSpan().SequenceEqual(entryList.ToArray()), $"entries of {h}, {describe}");
                if (entryList.Count != 1)
                {
                    expectedReducible = false;
                }

                int parentHeader = loop.ParentHeader;
                int expectedParent = parentHeader < 0 ? -1 : loops.InnermostLoopOf(parentHeader);
                Assert.True(loops.ParentOf(id) == expectedParent, $"parent of {h}, {describe}");
                Assert.True(loops.ParentOf(id) < id, $"parent of loop {id} precedes it, {describe}");
                if (parentHeader >= 0)
                {
                    nestedSeen++;
                }
            }
            Assert.True(loops.LoopCount == expectedCount, $"loop count, {describe}");
            Assert.True(loops.IsReducible == expectedReducible, $"reducible, {describe}");
            for (int x = 0; x < n; x++)
            {
                Assert.True(loops.LoopDepthOf(x) == depth[x], $"depth of {x}, {describe}");
            }
            loopsSeen += expectedCount;
            if (!expectedReducible)
            {
                irreducibleSeen++;
            }
        }
        Assert.True(loopsSeen > 1000, $"loops compared: {loopsSeen}");
        Assert.True(irreducibleSeen > 20, $"irreducible graphs compared: {irreducibleSeen}");
        Assert.True(nestedSeen > 300, $"nested loops compared: {nestedSeen}");
    }

    /// <summary>
    /// Checks the promises of the documentation against the classic definitions, with dominance taken
    /// from <see cref="NaiveDominators"/>: a graph is reducible if it becomes acyclic once its
    /// dominance back edges (<c>u -> v</c> with <c>v</c> dominating <c>u</c>) are removed, and then
    /// every loop is the natural loop of its header - the header plus every node that reaches the
    /// source of one of its back edges without passing through the header.
    /// </summary>
    [Fact]
    public void ReducibilityAndNaturalLoopsMatchTheClassicDefinitions()
    {
        int naturalLoopsCompared = 0;
        int irreducibleSeen = 0;
        for (long seed = 0; seed < Iterations; seed++)
        {
            Graph graph;
            int[] entries;
            if (seed % 3 != 2)
            {
                graph = ControlFlowGraphs.AnyControlFlowGraph(seed).Graph;
                entries = [0];
            }
            else
            {
                graph = RandomGraphs.AnyGraph(seed).Graph;
                var rnd = new JavaRandom(seed);
                int first = rnd.NextInt(graph.NodeCount);
                entries = [first, rnd.NextInt(graph.NodeCount)];
            }
            int n = graph.NodeCount;
            var dominance = new NaiveDominators(graph, entries);
            var loops = Loops.Of(BiAdjacentGraph.Of(graph), entries);

            var forwardOnly = new GraphBuilder();
            forwardOnly.AddNodes(n);
            for (int u = 0; u < n; u++)
            {
                if (!dominance.Reachable[u])
                {
                    continue;
                }
                foreach (int v in graph.OutNodes(u))
                {
                    if (!dominance.Dominates[v][u])
                    {
                        forwardOnly.AddEdge(u, v);
                    }
                }
            }
            bool reducible = Graphs.IsDag(forwardOnly.Build());
            Assert.True(loops.IsReducible == reducible, $"reducible, seed {seed}");
            if (!reducible)
            {
                irreducibleSeen++;
                continue;
            }

            // Every target of a dominance back edge heads a loop, and every loop header is one.
            var backEdgeTargets = new SortedSet<int>();
            for (int u = 0; u < n; u++)
            {
                if (!dominance.Reachable[u])
                {
                    continue;
                }
                foreach (int v in graph.OutNodes(u))
                {
                    if (dominance.Dominates[v][u])
                    {
                        backEdgeTargets.Add(v);
                    }
                }
            }
            var headers = new SortedSet<int>();
            for (int id = 0; id < loops.LoopCount; id++)
            {
                headers.Add(loops.HeaderOf(id));
            }
            Assert.True(headers.SetEquals(backEdgeTargets), $"loop headers, seed {seed}");

            for (int id = 0; id < loops.LoopCount; id++)
            {
                naturalLoopsCompared++;
                int header = loops.HeaderOf(id);
                var natural = new bool[n];
                natural[header] = true;
                var queue = new Stack<int>();
                for (int u = 0; u < n; u++)
                {
                    if (dominance.Reachable[u] && graph.HasOutNode(u, header)
                        && dominance.Dominates[header][u] && !natural[u])
                    {
                        natural[u] = true;
                        queue.Push(u);
                    }
                }
                while (queue.Count > 0)
                {
                    int x = queue.Pop();
                    for (int p = 0; p < n; p++)
                    {
                        if (dominance.Reachable[p] && !natural[p] && graph.HasOutNode(p, x))
                        {
                            natural[p] = true;
                            queue.Push(p);
                        }
                    }
                }
                var expected = new List<int>();
                for (int x = 0; x < n; x++)
                {
                    if (natural[x])
                    {
                        expected.Add(x);
                    }
                }
                int[] body = loops.BodyOf(id).ToArray();
                Array.Sort(body);
                Assert.True(body.AsSpan().SequenceEqual(expected.ToArray()), $"natural loop of {header}, seed {seed}");
            }
        }
        Assert.True(naturalLoopsCompared > 500, $"natural loops compared: {naturalLoopsCompared}");
        Assert.True(irreducibleSeen > 20, $"irreducible graphs compared: {irreducibleSeen}");
    }
}
