using System.Globalization;

namespace Graphity.Dot;

/// <summary>
/// The graph, the node filter, the undirected reading and the clusters of a <see cref="DotWriter"/>,
/// and the order in which edges are written. <c>Graphity.Mermaid</c> keeps its own copy for
/// <c>MermaidWriter</c>: a package has no access to the internal types of another.
/// </summary>
internal sealed class Selection
{
    internal readonly Graph Graph;
    internal Func<int, bool>? NodeFilter;
    internal bool Undirected;
    internal Clusters? Clusters;

    internal Selection(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        Graph = graph;
    }

    /// <summary>Sets the groups drawn as clusters.</summary>
    /// <exception cref="ArgumentException">The partition covers a different number of nodes.</exception>
    internal void SetClusters(NodePartition partition, Func<int, string?> label)
    {
        ArgumentNullException.ThrowIfNull(partition);
        ArgumentNullException.ThrowIfNull(label);
        if (partition.NodeCount != Graph.NodeCount)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture,
                    $"the partition covers {partition.NodeCount} nodes, the graph has {Graph.NodeCount}"),
                nameof(partition));
        }
        Clusters = new Clusters(partition, label);
    }

    internal bool IsIncluded(int node) => NodeFilter is null || NodeFilter(node);

    /// <summary>The cluster of a node, -1 for none.</summary>
    internal int ClusterOf(int node) => Clusters is null ? -1 : Clusters.Partition.Get(node);

    /// <summary>
    /// Whether the edge <c>source -> target</c> of an included source is written: its target
    /// included, and in the undirected reading only one edge per reciprocal pair, the one from the
    /// lower node ID.
    /// </summary>
    private bool IsWritten(int source, int target)
    {
        if (!IsIncluded(target))
        {
            return false;
        }
        return !Undirected || source <= target || !Graph.HasOutNode(target, source);
    }

    /// <summary>Calls <paramref name="action"/> for every written edge, in edge index order.</summary>
    internal void ForEachWrittenEdge(EdgeConsumer action)
    {
        ReadOnlySpan<int> outAdjacencyStart = Csr.OutAdjacencyStart(Graph);
        ReadOnlySpan<int> outAdjacency = Csr.OutAdjacency(Graph);
        int n = Graph.NodeCount;
        for (int source = 0; source < n; source++)
        {
            if (!IsIncluded(source))
            {
                continue;
            }
            for (int e = outAdjacencyStart[source], end = outAdjacencyStart[source + 1]; e < end; e++)
            {
                int target = outAdjacency[e];
                if (IsWritten(source, target))
                {
                    action(source, target, e);
                }
            }
        }
    }

    /// <summary>Whether <paramref name="name"/> is a plain identifier: a letter or underscore, then also digits.</summary>
    internal static bool IsIdentifier(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            bool letter = c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_';
            bool digit = c is >= '0' and <= '9';
            if (!(letter || (i > 0 && digit)))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Writes <paramref name="value"/> in decimal, as Java's <c>Integer.toString</c>.</summary>
    internal static void WriteInt(TextWriter output, int value)
    {
        Span<char> digits = stackalloc char[11];
        value.TryFormat(digits, out int length, default, CultureInfo.InvariantCulture);
        output.Write(digits[..length]);
    }
}

/// <summary>The groups drawn as clusters, with the label of each; set together, so held together.</summary>
internal sealed record Clusters(NodePartition Partition, Func<int, string?> Label);
