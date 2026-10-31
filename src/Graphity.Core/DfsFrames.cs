using System.Globalization;

namespace Graphity;

/// <summary>
/// Growth of the frame stacks of the iterative depth-first cores. A frame is two interleaved ints,
/// the node and its resume position in the adjacency array, so a stack of depth <c>d</c> needs
/// <c>2 x d</c> ints.
/// </summary>
/// <remarks>
/// Kept out of the cores because it runs once per doubling, not per edge; the cores themselves stay
/// duplicated, as in the Java library, so that each keeps its inner loop tight.
/// </remarks>
internal static class DfsFrames
{
    /// <summary>Most frames a stack holds: as many as fit into the largest array.</summary>
    private const int MaxDepth = ArrayLimits.MaxNodeCountTwoInts;

    /// <summary>The largest array, rounded down to whole frames.</summary>
    private const int MaxLength = 2 * MaxDepth;

    /// <summary>Initial capacity: one frame per node, but at least 16 and at most 1024 frames.</summary>
    internal static int[] Initial(int nodeCount) => new int[2 * Math.Max(16, Math.Min(nodeCount, 1024))];

    /// <summary>Doubles the stack, capped at one frame per node and at the array limit.</summary>
    /// <exception cref="ArgumentException">The search is deeper than an array can hold.</exception>
    internal static int[] Grow(int[] frames, int nodeCount)
    {
        long wanted = Math.Min(2L * frames.Length, 2L * nodeCount);
        if (wanted > MaxLength)
        {
            if (frames.Length >= MaxLength)
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                    $"a depth-first path is longer than {MaxDepth}"
                    + $" nodes, the most frames an array can hold"));
            }
            wanted = MaxLength;
        }
        var grown = new int[wanted];
        frames.CopyTo(grown, 0);
        return grown;
    }
}
