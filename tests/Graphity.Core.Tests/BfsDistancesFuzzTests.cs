namespace Graphity.Core.Tests;

/// <summary>
/// Cross-validates <see cref="BfsDistances"/> and <see cref="Graphs.ReachableFrom(Graph, ReadOnlySpan{int})"/>
/// against a textbook breadth-first search over <see cref="Graph.OutNodes"/> on random graphs with up
/// to three random sources, duplicates and the empty list included. A returned path is checked edge
/// by edge, not compared with one particular shortest path.
/// </summary>
public sealed class BfsDistancesFuzzTests
{
    private const int Iterations = 150;

    [Fact]
    public void DistancesPathsAndReachabilityMatchTheOracle()
    {
        int pathsChecked = 0;
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyGraph(seed);
            var g = labeled.Graph;
            int n = g.NodeCount;
            var rnd = new JavaRandom((seed * 31) + 3);
            var sources = new List<int>();
            int count = rnd.NextInt(8) == 0 ? 0 : 1 + rnd.NextInt(3);
            for (int i = 0; i < count; i++)
            {
                sources.Add(rnd.NextInt(n));
            }
            string describe = $"{labeled.Describe()}, sources=[{string.Join(", ", sources)}]";

            int[] expected = NaiveDistances(g, sources);
            var d = BfsDistances.Of(g, sources.ToArray());
            int max = -1;
            var reachable = new List<int>();
            for (int v = 0; v < n; v++)
            {
                Assert.True(expected[v] == d.DistanceOf(v), $"distance of {v}, {describe}");
                max = Math.Max(max, expected[v]);
                if (expected[v] >= 0)
                {
                    reachable.Add(v);
                }
                var path = d.PathTo(v);
                if (expected[v] < 0)
                {
                    Assert.Empty(path);
                    continue;
                }
                Assert.Equal(expected[v] + 1, path.Length);
                Assert.Contains(path[0], sources);
                Assert.Equal(v, path[^1]);
                for (int i = 0; i + 1 < path.Length; i++)
                {
                    Assert.True(g.HasOutNode(path[i], path[i + 1]), $"path edge {i}, {describe}");
                }
                if (expected[v] > 1)
                {
                    pathsChecked++;
                }
            }
            Assert.True(max == d.MaxDistance, $"max distance, {describe}");
            Assert.True(Graphs.ReachableFrom(g, sources.ToArray()).AsSpan().SequenceEqual(reachable.ToArray()),
                $"reachable set, {describe}");
        }
        Assert.True(pathsChecked > 2000, $"paths with more than one edge: {pathsChecked}");
    }

    private static int[] NaiveDistances(Graph g, List<int> sources)
    {
        var dist = new int[g.NodeCount];
        Array.Fill(dist, -1);
        var queue = new Queue<int>();
        foreach (int s in sources)
        {
            if (dist[s] < 0)
            {
                dist[s] = 0;
                queue.Enqueue(s);
            }
        }
        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            foreach (int v in g.OutNodes(u))
            {
                if (dist[v] < 0)
                {
                    dist[v] = dist[u] + 1;
                    queue.Enqueue(v);
                }
            }
        }
        return dist;
    }
}
