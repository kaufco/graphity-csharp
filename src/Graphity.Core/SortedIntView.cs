using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Graphity;

/// <summary>
/// A list of <c>int</c> values in ascending order and without duplicates, as a view of a range of
/// an array.
/// </summary>
/// <remarks>
/// <para>
/// Adjacency lookups such as <see cref="Graph.OutNodes"/> return it, as do other methods of this
/// library that return nodes or edge indices in ascending order, such as
/// <see cref="Graphs.ReachableFrom(Graph, int)"/>. It is a read-only list
/// (<see cref="IReadOnlyList{T}"/>) without boxing. Since its values are sorted and distinct,
/// <see cref="IndexOf"/> and <see cref="Contains"/> search in O(log Count), and <see cref="First"/>
/// and <see cref="Last"/> are the smallest and the largest value.
/// </para>
/// <para>
/// It is a <c>readonly struct</c> of three fields, so returning one allocates nothing. It converts
/// implicitly to <see cref="ReadOnlySpan{T}"/>, so a view feeds every method that takes a span,
/// such as <c>builder.AddOutEdges(source, graph.OutNodes(node))</c>. Unlike a span, a view can be
/// stored in fields and collections. <c>foreach</c> runs over it in any context, even in iterators
/// and in async methods that await inside the loop, with a struct enumerator that allocates
/// nothing. <c>foreach</c> over <see cref="AsSpan"/> is the fastest loop. <see cref="Slice"/> and
/// the range syntax <c>view[a..b]</c> return a part of it.
/// </para>
/// <para>
/// A view shares its array with the object it comes from instead of copying it; a method that
/// returns a copy, such as <see cref="Union"/>, says so. Instances are immutable and safe to share
/// between threads, as the arrays behind them never change. A view that <see cref="ViewOf"/> makes
/// of an array of the caller stays sorted and unchanged only as long as nobody modifies that range
/// of the array. <c>default(SortedIntView)</c> is the empty view.
/// </para>
/// <para><b>Memory:</b> constant; the values stay in the shared array.</para>
/// </remarks>
[DebuggerDisplay("Count = {Count}")]
public readonly struct SortedIntView : IReadOnlyList<int>, IEquatable<SortedIntView>
{
    // The view is _array[_from.._to - 1]. The array may be shared with a graph and other views, and
    // is never written; it is null in the default value, the empty view.
    private readonly int[]? _array;
    private readonly int _from;
    private readonly int _to;

    // Both take the array as it is, without a copy, and check only the bounds of the range: the
    // caller guarantees that the range is strictly ascending.
    internal SortedIntView(int[] array)
        : this(array, 0, array.Length)
    {
    }

    internal SortedIntView(int[] array, int fromIndex, int toIndex)
    {
        Checks.FromToIndex(fromIndex, toIndex, array.Length);
        _array = array;
        _from = fromIndex;
        _to = toIndex;
    }

    /// <summary>Gets an empty view, equal to <c>default(SortedIntView)</c>.</summary>
    public static SortedIntView Empty => default;

    /// <summary>Gets the number of values.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public int Count => _to - _from;

    /// <summary>Gets whether the view has no values. Equivalent to <c>Count == 0</c>.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public bool IsEmpty => _to == _from;

    /// <summary>Gets the value at position <paramref name="index"/>.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="index">Position in <c>0..Count-1</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public int this[int index]
    {
        get
        {
            Checks.Index(index, _to - _from);
            return _array![_from + index];
        }
    }

    /// <summary>Returns the first value, which is the smallest.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <returns>The smallest value.</returns>
    /// <exception cref="InvalidOperationException">The view is empty.</exception>
    public int First()
    {
        if (_from == _to)
        {
            throw new InvalidOperationException("the view is empty");
        }
        return _array![_from];
    }

    /// <summary>Returns the last value, which is the largest.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <returns>The largest value.</returns>
    /// <exception cref="InvalidOperationException">The view is empty.</exception>
    public int Last()
    {
        if (_from == _to)
        {
            throw new InvalidOperationException("the view is empty");
        }
        return _array![_to - 1];
    }

    /// <summary>
    /// Returns the position of <paramref name="value"/>, or -1 if the view does not contain it.
    /// </summary>
    /// <remarks><b>Time:</b> O(log Count).</remarks>
    /// <param name="value">The value to search for.</param>
    /// <returns>The position in <c>0..Count-1</c>, or -1.</returns>
    public int IndexOf(int value)
    {
        int index = AsSpan().BinarySearch(value);
        return index >= 0 ? index : -1;
    }

    /// <summary>
    /// Returns the position of <paramref name="value"/>, or -1 if the view does not contain it.
    /// Equivalent to <see cref="IndexOf"/>, since no value occurs twice.
    /// </summary>
    /// <remarks><b>Time:</b> O(log Count).</remarks>
    /// <param name="value">The value to search for.</param>
    /// <returns>The position in <c>0..Count-1</c>, or -1.</returns>
    public int LastIndexOf(int value) => IndexOf(value);

    /// <summary>Returns whether the view contains <paramref name="value"/>.</summary>
    /// <remarks><b>Time:</b> O(log Count).</remarks>
    /// <param name="value">The value to search for.</param>
    /// <returns><see langword="true"/> if the view contains the value.</returns>
    public bool Contains(int value) => AsSpan().BinarySearch(value) >= 0;

    /// <summary>Returns the values as a new array.</summary>
    /// <remarks>
    /// <b>Time:</b> O(Count).<br/>
    /// <b>Memory:</b> 4 x Count bytes for the result.
    /// </remarks>
    /// <returns>A copy of the values.</returns>
    public int[] ToArray() => AsSpan().ToArray();

    /// <summary>Copies the values into <paramref name="destination"/> from position 0 on.</summary>
    /// <remarks>
    /// <para>
    /// A part of the view copies with <c>Slice(start, length).CopyTo(...)</c>.
    /// </para>
    /// <para><b>Time:</b> O(Count).</para>
    /// </remarks>
    /// <param name="destination">The span to fill; at least <see cref="Count"/> long.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than the view.</exception>
    public void CopyTo(Span<int> destination) => AsSpan().CopyTo(destination);

    /// <summary>
    /// Returns the <paramref name="length"/> values from position <paramref name="start"/> on as a
    /// view of the same array, not a copy.
    /// </summary>
    /// <remarks>
    /// <para>The range syntax <c>view[a..b]</c> calls it with <c>Slice(a, b - a)</c>.</para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="start">The first position.</param>
    /// <param name="length">The number of values.</param>
    /// <returns>A view of the range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range is not within the view.</exception>
    public SortedIntView Slice(int start, int length)
    {
        int count = Count;
        if ((uint)start > (uint)count || (uint)length > (uint)(count - start))
        {
            throw new ArgumentOutOfRangeException(nameof(start), start,
                string.Create(CultureInfo.InvariantCulture,
                    $"Range [{start}, {(long)start + length}) out of bounds for length {count}"));
        }
        if (length == 0)
        {
            return Empty;
        }
        return new SortedIntView(_array!, _from + start, _from + start + length);
    }

    /// <summary>Returns the values as a read-only span over the shared array, not a copy.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <returns>The values.</returns>
    public ReadOnlySpan<int> AsSpan() => new(_array, _from, _to - _from);

    /// <summary>Converts the view into a read-only span over the shared array, not a copy.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="view">The view.</param>
    public static implicit operator ReadOnlySpan<int>(SortedIntView view) => view.AsSpan();

    /// <summary>Returns an enumerator over the values, in ascending order.</summary>
    /// <remarks>
    /// <para>
    /// A struct enumerator: <c>foreach</c> over a view allocates nothing and works in any context,
    /// even in iterators and in async methods that await inside the loop. A loop that has to be as
    /// fast as possible runs over the span instead: <c>foreach (int v in view.AsSpan())</c>.
    /// </para>
    /// <para><b>Time:</b> O(1) to create the enumerator.</para>
    /// </remarks>
    /// <returns>The enumerator.</returns>
    public Enumerator GetEnumerator() => new(_array, _from, _to);

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>An enumerator over the values of a <see cref="SortedIntView"/>, in ascending order.</summary>
    /// <remarks>
    /// A struct, so that <c>foreach</c> allocates nothing; through the interfaces it is boxed once
    /// per enumeration. <see cref="Reset"/> is not supported.
    /// </remarks>
    public struct Enumerator : IEnumerator<int>
    {
        private readonly int[]? _array;
        private readonly int _to;
        private int _index;

        internal Enumerator(int[]? array, int from, int to)
        {
            _array = array;
            _to = to;
            _index = from - 1;
        }

        /// <summary>Gets the value at the current position.</summary>
        /// <remarks><b>Time:</b> O(1).</remarks>
        public readonly int Current => _array![_index];

        readonly object IEnumerator.Current => Current;

        /// <summary>Advances to the next value.</summary>
        /// <remarks><b>Time:</b> O(1).</remarks>
        /// <returns><see langword="true"/> if there is a next value, <see langword="false"/> at the end.</returns>
        public bool MoveNext() => ++_index < _to;

        /// <summary>Not supported: a view is enumerated again with a new enumerator.</summary>
        /// <exception cref="NotSupportedException">Always.</exception>
        public readonly void Reset() => throw new NotSupportedException();

        /// <summary>Does nothing: the enumerator holds no resources.</summary>
        public readonly void Dispose()
        {
        }
    }

    /// <summary>
    /// Returns a view of <c>array[fromIndex..toIndex-1]</c>, which must be strictly ascending.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The range is checked, not copied: the view shares <paramref name="array"/>, and stays sorted
    /// and unchanged only as long as nobody modifies that range of it.
    /// </para>
    /// <para><b>Time:</b> O(toIndex - fromIndex).</para>
    /// </remarks>
    /// <param name="array">The array to view.</param>
    /// <param name="fromIndex">The first position of the range.</param>
    /// <param name="toIndex">The end position of the range, exclusive.</param>
    /// <returns>A view of the range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fromIndex"/> is negative, <paramref name="toIndex"/> is greater than
    /// <c>array.Length</c>, or <c>fromIndex &gt; toIndex</c>.
    /// </exception>
    /// <exception cref="ArgumentException">The range is not strictly ascending.</exception>
    public static SortedIntView ViewOf(int[] array, int fromIndex, int toIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        Checks.FromToIndex(fromIndex, toIndex, array.Length);
        if (fromIndex == toIndex)
        {
            return Empty;
        }
        for (int i = fromIndex + 1; i < toIndex; i++)
        {
            if (array[i] <= array[i - 1])
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                    $"array[{fromIndex}..{toIndex}) must be strictly ascending,"
                    + $" but {array[i]} follows {array[i - 1]} at index {i}"), nameof(array));
            }
        }
        return new SortedIntView(array, fromIndex, toIndex);
    }

    /// <summary>
    /// Returns the values of this view and <paramref name="other"/> together, in ascending order and
    /// without duplicates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The result is a copy, merged on every call, unless one of the two views is empty: then it is
    /// the other one.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(Count + other.Count).<br/>
    /// <b>Memory:</b> up to 4 x (Count + other.Count) bytes for the result; while it runs, up to as
    /// much again if the two views share values.
    /// </para>
    /// </remarks>
    /// <param name="other">The view to unite with this one.</param>
    /// <returns>The union.</returns>
    /// <exception cref="ArgumentException">
    /// The union has more than <c>Array.MaxLength</c> values, the most an array can hold.
    /// </exception>
    public SortedIntView Union(SortedIntView other)
    {
        if (other.IsEmpty)
        {
            return this;
        }
        if (IsEmpty)
        {
            return other;
        }
        return new SortedIntView(UnionInRange(_array!, _from, _to, other._array!, other._from, other._to));
    }

    /// <summary>
    /// Returns whether <paramref name="other"/> is a view with the same values in the same order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Equality is defined between views; to compare with another sequence, use
    /// <c>AsSpan().SequenceEqual(...)</c>.
    /// </para>
    /// <para><b>Time:</b> O(Count).</para>
    /// </remarks>
    /// <param name="other">The view to compare with.</param>
    /// <returns><see langword="true"/> if both hold the same values.</returns>
    public bool Equals(SortedIntView other) => AsSpan().SequenceEqual(other.AsSpan());

    /// <inheritdoc cref="Equals(SortedIntView)"/>
    public override bool Equals(object? obj) => obj is SortedIntView other && Equals(other);

    /// <summary>
    /// Returns a hash code over the values. It is the list hash code that the Java library computes
    /// as well.
    /// </summary>
    /// <remarks><b>Time:</b> O(Count).</remarks>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        int h = 1;
        foreach (int value in AsSpan())
        {
            h = unchecked((31 * h) + value);
        }
        return h;
    }

    /// <summary>Returns whether both views hold the same values in the same order.</summary>
    /// <remarks><b>Time:</b> O(Count).</remarks>
    /// <param name="left">The first view.</param>
    /// <param name="right">The second view.</param>
    public static bool operator ==(SortedIntView left, SortedIntView right) => left.Equals(right);

    /// <summary>Returns whether the views differ in a value or in length.</summary>
    /// <remarks><b>Time:</b> O(Count).</remarks>
    /// <param name="left">The first view.</param>
    /// <param name="right">The second view.</param>
    public static bool operator !=(SortedIntView left, SortedIntView right) => !left.Equals(right);

    /// <summary>Returns the values in brackets, such as <c>[1, 3, 5]</c>.</summary>
    /// <remarks><b>Time:</b> O(Count).</remarks>
    /// <returns>The text.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder("[");
        foreach (int value in AsSpan())
        {
            if (sb.Length > 1)
            {
                sb.Append(", ");
            }
            sb.Append(CultureInfo.InvariantCulture, $"{value}");
        }
        return sb.Append(']').ToString();
    }

    // IndexOf and Contains on a range of an array, without a view, for the adjacency lookups of the
    // graph classes. IndexOfInRange returns the position in the array, not in the range: in the
    // out-adjacency of a graph, that is the edge index.
    internal static int IndexOfInRange(int[] array, int fromIndex, int toIndex, int value)
    {
        int index = Array.BinarySearch(array, fromIndex, toIndex - fromIndex, value);
        return index >= 0 ? index : -1;
    }

    internal static bool ContainsInRange(int[] array, int fromIndex, int toIndex, int value) =>
        Array.BinarySearch(array, fromIndex, toIndex - fromIndex, value) >= 0;

    // The merge behind Union and BiAdjacentGraph.Neighbors, on ranges of arrays, so that neither
    // needs views. Throws ArgumentException if the union does not fit into an array.
    internal static int[] UnionInRange(int[] a, int aFrom, int aTo, int[] b, int bFrom, int bTo)
    {
        var output = new int[UnionCapacity(a, aFrom, aTo, b, bFrom, bTo, ArrayLimits.MaxArrayLength)];
        int outPos = 0;
        int aPos = aFrom;
        int bPos = bFrom;
        while (aPos < aTo && bPos < bTo)
        {
            int aValue = a[aPos];
            int bValue = b[bPos];
            int outValue;
            if (aValue < bValue)
            {
                outValue = aValue;
                aPos++;
            }
            else if (bValue < aValue)
            {
                outValue = bValue;
                bPos++;
            }
            else
            {
                outValue = aValue;
                aPos++;
                bPos++;
            }
            output[outPos++] = outValue;
        }
        if (aPos < aTo)
        {
            a.AsSpan(aPos, aTo - aPos).CopyTo(output.AsSpan(outPos));
            outPos += aTo - aPos;
        }
        else if (bPos < bTo)
        {
            b.AsSpan(bPos, bTo - bPos).CopyTo(output.AsSpan(outPos));
            outPos += bTo - bPos;
        }
        // Trim to the actual size where a and b share values.
        return outPos == output.Length ? output : output[..outPos];
    }

    // The length to allocate for the union of two sorted, duplicate-free ranges: the sum of their
    // sizes, or, if that exceeds maxLength, the size of the union itself, counted by a merge that
    // writes nothing - so that two large ranges with many common values still fit. maxLength is the
    // largest array length; tests pass a small one. Throws ArgumentException if the union has more
    // than maxLength values.
    internal static int UnionCapacity(int[] a, int aFrom, int aTo, int[] b, int bFrom, int bTo, int maxLength)
    {
        long sizeSum = (long)(aTo - aFrom) + (bTo - bFrom);
        if (sizeSum <= maxLength)
        {
            return (int)sizeSum;
        }
        int aPos = aFrom;
        int bPos = bFrom;
        long unionSize = 0;
        while (aPos < aTo && bPos < bTo)
        {
            int aValue = a[aPos];
            int bValue = b[bPos];
            if (aValue <= bValue)
            {
                aPos++;
            }
            if (bValue <= aValue)
            {
                bPos++;
            }
            unionSize++;
        }
        unionSize += (long)(aTo - aPos) + (bTo - bPos);
        if (unionSize > maxLength)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"the union has {unionSize} values, more than the largest array length of {maxLength}"));
        }
        return (int)unionSize;
    }
}
