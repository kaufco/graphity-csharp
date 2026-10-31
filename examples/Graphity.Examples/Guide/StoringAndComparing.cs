using Graphity;

namespace Examples.Guide;

/// <summary>Graphs stored in a file, hashed and compared.</summary>
internal static class StoringAndComparing
{
    /// <summary>What the example reads after storing and loading.</summary>
    internal sealed record Stored(Graph Loaded, bool Same, long Size);

    /// <summary>Hashes compared, and the graph built a second time.</summary>
    internal sealed record HashesResult(Graph Rebuilt, bool SameForEqualGraphs,
        bool DifferentForNextVersion);

    /// <summary>
    /// Returns the packages after a round trip through <paramref name="file"/>.
    /// </summary>
    internal static Stored RoundTrip(string file)
    {
        Graph dependencies = SmallProgram.Dependencies();
        // @start region="writeRead"
        using (Stream output = File.Create(file))
        {
            dependencies.WriteTo(output);
        }
        Graph loaded;
        using (Stream input = File.OpenRead(file))
        {
            loaded = Graph.ReadFrom(input);
        }
        bool same = loaded.Equals(dependencies);   // true
        // 100 - bytes, for 8 nodes and 14 edges
        long size = new FileInfo(file).Length;
        // @end
        return new Stored(loaded, same, size);
    }

    /// <summary>
    /// The next version of the packages: storage no longer uses model, ui uses storage.
    /// </summary>
    internal static Graph NextVersion()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        int ui = SmallProgram.NodeOf(names, "ui");
        int model = SmallProgram.NodeOf(names, "model");
        int storage = SmallProgram.NodeOf(names, "storage");
        var builder = new GraphBuilder();
        builder.AddNodes(dependencies.NodeCount);
        dependencies.ForEachEdge((source, target, _) =>
        {
            if (source != storage || target != model)
            {
                builder.AddEdge(source, target);
            }
        });
        builder.AddEdge(ui, storage);
        return builder.Build();
    }

    /// <summary>Returns how the hashes of three graphs compare.</summary>
    internal static HashesResult Hashes()
    {
        Graph dependencies = SmallProgram.Dependencies();
        Graph nextVersion = NextVersion();
        // @start region="topologyHash"
        // The same graph built a second time, from its edges as pairs in another order,
        // with the pair [2, 4] given twice:
        int[][] uses =
        [
            [5, 7], [2, 4], [0, 3], [6, 7], [4, 5], [2, 7], [3, 5], [1, 7], [5, 4],
            [0, 1], [2, 3], [3, 4], [5, 6], [0, 2], [2, 4],
        ];
        var builder = new GraphBuilder();
        builder.AddNodes(8);
        foreach (int[] pair in uses)
        {
            builder.AddEdge(pair[0], pair[1]);
        }
        Graph rebuilt = builder.Build();

        // The hash of dependencies, compared with those of the other two graphs:
        long hash = Graphs.TopologyHash(dependencies);
        // true
        bool sameForEqualGraphs = hash == Graphs.TopologyHash(rebuilt);
        // true
        bool differentForNextVersion = hash != Graphs.TopologyHash(nextVersion);
        // @end
        return new HashesResult(rebuilt, sameForEqualGraphs, differentForNextVersion);
    }

    /// <summary>
    /// Returns the edges added and removed between two versions of the packages.
    /// </summary>
    internal static GraphDiff Diff()
    {
        Graph dependencies = SmallProgram.Dependencies();
        Graph nextVersion = NextVersion();
        // @start region="diff"
        GraphDiff diff = Graphs.Diff(dependencies, nextVersion);
        Edges added = diff.AddedEdges;       // ui -> storage, edge 6 of nextVersion
        Edges removed = diff.RemovedEdges;   // storage -> model, edge 10 of dependencies
        bool unchanged = diff.IsEmpty;       // false
        // @end
        return diff;
    }
}
