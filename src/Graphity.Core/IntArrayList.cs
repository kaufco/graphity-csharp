using System.Runtime.CompilerServices;

namespace Graphity;

/// <summary>
/// A growable <c>int[]</c>, the counterpart of fastutil's <c>IntArrayList</c> that the Java code
/// uses for results of unknown size. Growth follows fastutil: a list starts empty and allocates 10
/// slots on the first add, then grows by half; <see cref="EnsureCapacity"/> allocates exactly what
/// it is asked for.
/// </summary>
internal sealed class IntArrayList
{
    private const int DefaultInitialCapacity = 10;

    private int[] _elements;
    private int _count;

    /// <summary>Whether the backing array is still the empty one the list started with.</summary>
    private bool _defaultEmpty;

    /// <summary>An empty list that allocates on the first add.</summary>
    internal IntArrayList()
    {
        _elements = [];
        _defaultEmpty = true;
    }

    /// <summary>The backing array; only the first <see cref="Count"/> entries are elements.</summary>
    internal int[] Elements => _elements;

    internal int Count => _count;

    internal bool IsEmpty => _count == 0;

    internal int this[int index] => _elements[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(int value)
    {
        if (_count == _elements.Length)
        {
            Grow(_count + 1);
        }
        _elements[_count++] = value;
    }

    /// <summary>Grows the backing array to exactly <paramref name="capacity"/> if it is smaller.</summary>
    internal void EnsureCapacity(int capacity)
    {
        if (capacity <= _elements.Length || (_defaultEmpty && capacity <= DefaultInitialCapacity))
        {
            return;
        }
        Resize(capacity);
    }

    /// <summary>A copy of the elements, at their exact length.</summary>
    internal int[] ToArray() => _elements.AsSpan(0, _count).ToArray();

    private void Grow(int capacity)
    {
        if (capacity <= _elements.Length)
        {
            return;
        }
        if (!_defaultEmpty)
        {
            capacity = (int)Math.Max(Math.Min(_elements.Length + (long)(_elements.Length >> 1), ArrayLimits.MaxArrayLength), capacity);
        }
        else if (capacity < DefaultInitialCapacity)
        {
            capacity = DefaultInitialCapacity;
        }
        Resize(capacity);
    }

    private void Resize(int capacity)
    {
        var grown = new int[capacity];
        _elements.AsSpan(0, _count).CopyTo(grown);
        _elements = grown;
        _defaultEmpty = false;
    }
}
