using System.Globalization;

namespace Graphity;

/// <summary>
/// The largest array the library allocates, and the limits derived from it.
/// </summary>
/// <remarks>
/// One rule for all of them: the largest array .NET allocates, <see cref="Array.MaxLength"/>
/// (<c>0x7FFFFFC7</c>, 2^31 - 57). Java takes the JDK's soft maximum, 2^31 - 9, which .NET cannot
/// allocate; the derived limits are therefore a few elements lower than in Java.
/// <c>Graphity.ControlFlow</c> cannot see this class and repeats the numbers it needs, each with a
/// note that it follows this one.
/// </remarks>
internal static class ArrayLimits
{
    /// <summary>The largest array length .NET allocates, <see cref="Array.MaxLength"/>.</summary>
    internal const int MaxArrayLength = 0x7FFFFFC7;

    /// <summary>
    /// Largest node count of a graph, 2^31 - 58 (Java: 2^31 - 10): its <c>nodeCount + 1</c> node
    /// offsets fill the largest array. The node limit of <see cref="GraphBuilder"/>, the group
    /// limit of <see cref="NodePartition.Of"/> and the bound on the node count that
    /// <see cref="Graph.ReadFrom"/> accepts from a header.
    /// </summary>
    internal const int MaxNodeCount = MaxArrayLength - 1;

    /// <summary>
    /// Largest node count for an array of two ints per node, 2^30 - 29: the lanes of Tarjan's
    /// algorithm, the double cover of <see cref="Graphs.IsBipartite"/> and the frames of a
    /// depth-first path through every node.
    /// </summary>
    internal const int MaxNodeCountTwoInts = MaxArrayLength / 2;

    /// <summary>
    /// Rejects a node count above <paramref name="maxNodeCount"/>, the most nodes for which
    /// <paramref name="algorithm"/> fits its two ints per node into one array, counting a virtual
    /// root as a node if the algorithm adds one.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="nodeCount"/> is above the limit.</exception>
    internal static void CheckNodeCount(int nodeCount, int maxNodeCount, string algorithm)
    {
        if (nodeCount > maxNodeCount)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"{algorithm} keeps two ints per node in one array,"
                + $" counting a virtual root if it adds one, and so requires nodeCount <= "
                + $"{maxNodeCount}, but was {nodeCount}"));
        }
    }
}
