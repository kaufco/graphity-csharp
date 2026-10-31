using System.Globalization;
using System.Runtime.CompilerServices;

namespace Graphity;

/// <summary>
/// Argument checks shared by several types: the counterpart of Java's package-private
/// <c>Checks</c> and of <c>Objects.checkIndex</c>.
/// </summary>
internal static class Checks
{
    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="index"/> is in <c>0..length-1</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Index(int index, int length, [CallerArgumentExpression(nameof(index))] string? name = null)
    {
        if ((uint)index >= (uint)length)
        {
            ThrowIndex(index, length, name);
        }
    }

    /// <summary>
    /// Checks every node of a list of start nodes, sources or entries before any work begins: the
    /// check of each list form of a traversal or search. Throws
    /// <see cref="ArgumentOutOfRangeException"/> for a node outside <c>0..nodeCount-1</c>.
    /// </summary>
    internal static void Nodes(ReadOnlySpan<int> nodes, int nodeCount,
        [CallerArgumentExpression(nameof(nodes))] string? name = null)
    {
        foreach (int node in nodes)
        {
            Index(node, nodeCount, name);
        }
    }

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> unless <c>0 &lt;= from &lt;= to &lt;= length</c>.</summary>
    internal static void FromToIndex(int fromIndex, int toIndex, int length)
    {
        if (fromIndex < 0 || fromIndex > toIndex || toIndex > length)
        {
            throw new ArgumentOutOfRangeException(nameof(fromIndex), fromIndex,
                string.Create(CultureInfo.InvariantCulture,
                    $"Range [{fromIndex}, {toIndex}) out of bounds for length {length}"));
        }
    }

    /// <summary>Throws <see cref="ArgumentException"/> unless <paramref name="length"/> is <paramref name="expected"/>.</summary>
    internal static void Length(int length, int expected, string name)
    {
        if (length != expected)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"{name} has length {length}, expected {expected}"), name);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIndex(int index, int length, string? name) =>
        throw new ArgumentOutOfRangeException(name, index, string.Create(CultureInfo.InvariantCulture,
            $"Index {index} out of bounds for length {length}"));
}
