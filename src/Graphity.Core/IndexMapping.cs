using System.Globalization;

namespace Graphity;

/// <summary>
/// A mapping from the indices <c>0..IndexCount-1</c> to the indices of another numbering. It is the
/// common base of the node and edge mappings and of the partitions.
/// </summary>
/// <remarks>
/// <para>
/// The indices are node IDs or edge indices. <c>Get(i)</c> is the index that <c>i</c> maps to, or
/// -1 if <c>i</c> is not mapped; a mapping with unmapped indices is partial. A
/// <see cref="NodeMapping"/> maps the node IDs of one graph to those of another, and an
/// <see cref="EdgeMapping"/> does the same for edge indices. Neither maps two indices to the same
/// index, so both can be inverted. An <see cref="IndexPartition"/> maps indices to groups and lists
/// the indices of each group. Several indices can map to the same group.
/// </para>
/// <para>
/// That a node or edge mapping maps no two indices to the same index is a contract, not a check:
/// <see cref="NodeMapping.Of"/> accepts an array that breaks it, and only <c>Inverted</c> and
/// <see cref="Graphs.Remapped"/> reject such a mapping.
/// </para>
/// <para>
/// Instances are immutable and safe to share between threads. No other assembly can derive from
/// this class.
/// </para>
/// <para><b>Memory:</b> 4 x IndexCount bytes; a partition holds more.</para>
/// </remarks>
public abstract class IndexMapping
{
    // Mapping[i] is the index that i maps to, or -1. Graphs and Biconnectivity read it directly.
    internal readonly int[] Mapping;

    // The number of entries other than -1, counted once so that IsPartial and MappedCount are O(1).
    private readonly int _validCount;

    // Takes the array as it is, not copied: the caller hands over an array that nobody else holds,
    // and validCount matches it.
    private protected IndexMapping(int[] mapping, int validCount)
    {
        Mapping = mapping;
        _validCount = validCount;
    }

    /// <summary>
    /// Returns the index that <paramref name="id"/> maps to, or -1 if <paramref name="id"/> is not
    /// mapped.
    /// </summary>
    /// <remarks>
    /// <para>In a partition, it is the group of <paramref name="id"/>.</para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="id">Index in <c>0..IndexCount-1</c>.</param>
    /// <returns>The index that <paramref name="id"/> maps to, or -1.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is out of range.</exception>
    public int Get(int id)
    {
        Checks.Index(id, Mapping.Length);
        return Mapping[id];
    }

    /// <summary>
    /// Returns whether <paramref name="id"/> is mapped. Equivalent to <c>Get(id) != -1</c>.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="id">Index in <c>0..IndexCount-1</c>.</param>
    /// <returns><see langword="true"/> if <paramref name="id"/> is mapped.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is out of range.</exception>
    public bool IsMapped(int id) => Get(id) >= 0;

    /// <summary>
    /// Gets whether some index is not mapped. Equivalent to <c>MappedCount &lt; IndexCount</c>.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public bool IsPartial => _validCount < Mapping.Length;

    /// <summary>
    /// Gets the number of indices that are mapped; in a partition, the number of indices that belong
    /// to a group.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public int MappedCount => _validCount;

    /// <summary>Gets the number of indices this mapping maps from, mapped or not.</summary>
    /// <remarks>
    /// <para>It is the length of the arrays that <c>Gather</c> returns and that <c>Reduce</c> takes.</para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    public int IndexCount => Mapping.Length;

    /// <summary>The target of every source index, for the conformance tests.</summary>
    internal ReadOnlySpan<int> AsSpan() => Mapping;

    /// <summary>Inverts <paramref name="mapping"/> into a target space of <paramref name="targetCount"/> indices.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="targetCount"/> is negative or larger than the largest array, or two sources map
    /// to one target.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A target is <paramref name="targetCount"/> or more.</exception>
    internal static int[] InvertArray(int[] mapping, int targetCount)
    {
        if (targetCount < 0 || targetCount > ArrayLimits.MaxArrayLength)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"targetCount out of range: {targetCount}"), nameof(targetCount));
        }
        var inverse = new int[targetCount];
        Array.Fill(inverse, -1);
        for (int i = 0; i < mapping.Length; i++)
        {
            int value = mapping[i];
            if (value < 0)
            {
                continue;
            }
            if (value >= targetCount)
            {
                throw new ArgumentOutOfRangeException(nameof(targetCount), targetCount,
                    string.Create(CultureInfo.InvariantCulture,
                        $"index {i} maps to {value}, but targetCount is {targetCount}"));
            }
            if (inverse[value] != -1)
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                    $"cannot invert mapping: indices {inverse[value]} and {i} both map to {value}"));
            }
            inverse[value] = i;
        }
        return inverse;
    }

    /// <summary>Composes two mapping arrays: <c>result[i] = outer[inner[i]]</c>; -1 propagates.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inner"/> maps outside <paramref name="outer"/>.</exception>
    internal static int[] ComposeArrays(int[] outer, int[] inner)
    {
        var result = new int[inner.Length];
        for (int i = 0; i < result.Length; i++)
        {
            int mid = inner[i];
            if (mid < 0)
            {
                result[i] = -1;
            }
            else if (mid < outer.Length)
            {
                result[i] = outer[mid];
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(inner), mid,
                    string.Create(CultureInfo.InvariantCulture,
                        $"inner maps {i} to {mid}, outside the indices 0..{outer.Length - 1} of this mapping"));
            }
        }
        return result;
    }

    /// <summary>Rejects writing the result into the input, which would overwrite unread values.</summary>
    internal static void CheckDistinct<T>(ReadOnlySpan<T> values, ReadOnlySpan<T> result, string name)
    {
        if (!result.IsEmpty && values.Overlaps(result))
        {
            throw new ArgumentException($"values and {name} must be distinct arrays", name);
        }
    }

    /// <summary>Counts the non-negative entries.</summary>
    internal static int CountValid(int[] mapping)
    {
        int count = 0;
        foreach (int v in mapping)
        {
            if (v >= 0)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary><c>result[i] = values[mapping[i]]</c>, <c>default</c> where unmapped.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A mapped target lies outside <paramref name="values"/>.</exception>
    internal static T[] GatherArray<T>(int[] mapping, ReadOnlySpan<T> values)
        where T : unmanaged
    {
        var result = new T[mapping.Length];
        for (int i = 0; i < result.Length; i++)
        {
            int source = mapping[i];
            if (source >= 0)
            {
                result[i] = values[CheckedSource(source, values.Length)];
            }
        }
        return result;
    }

    /// <summary><c>result[i] = values[mapping[i]]</c>, untouched where unmapped.</summary>
    /// <exception cref="ArgumentException">The result has the wrong length or overlaps the values.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A mapped target lies outside <paramref name="values"/>.</exception>
    internal static void GatherArray<T>(int[] mapping, ReadOnlySpan<T> values, Span<T> result)
    {
        CheckDistinct(values, result, "newValues");
        Checks.Length(result.Length, mapping.Length, "newValues");
        for (int i = 0; i < mapping.Length; i++)
        {
            int source = mapping[i];
            if (source >= 0)
            {
                result[i] = values[CheckedSource(source, values.Length)];
            }
        }
    }

    // The check that Java's array access makes, with its message: Java reads values[source] and
    // throws an ArrayIndexOutOfBoundsException, which is an IndexOutOfBoundsException.
    private static int CheckedSource(int source, int length)
    {
        if (source >= length)
        {
            throw new ArgumentOutOfRangeException("values", source,
                string.Create(CultureInfo.InvariantCulture, $"Index {source} out of bounds for length {length}"));
        }
        return source;
    }
}
