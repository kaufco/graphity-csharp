using System.Runtime.CompilerServices;

namespace Graphity.ControlFlow;

/// <summary>
/// A growable <c>int[]</c>, the counterpart of fastutil's <c>IntArrayList</c> that the Java code uses
/// for results of unknown size. The module's copy of the part of the core's internal list it needs.
/// Growth follows fastutil: the list starts empty and allocates 10 slots on the first add, then grows
/// by half, capped at <see cref="ControlFlow.MaxArrayLength"/>, the module's one array limit.
/// </summary>
internal sealed class IntArrayList
{
    private const int DefaultInitialCapacity = 10;

    private int[] _elements = [];
    private int _count;

    internal int Count => _count;

    internal int this[int index] => _elements[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(int value)
    {
        if (_count == _elements.Length)
        {
            Grow();
        }
        _elements[_count++] = value;
    }

    /// <summary>A copy of the elements, at their exact length.</summary>
    internal int[] ToArray() => _elements.AsSpan(0, _count).ToArray();

    private void Grow()
    {
        int capacity = _elements.Length == 0
            ? DefaultInitialCapacity
            : (int)Math.Max(Math.Min(_elements.Length + (long)(_elements.Length >> 1), ControlFlow.MaxArrayLength), _count + 1L);
        var grown = new int[capacity];
        _elements.AsSpan(0, _count).CopyTo(grown);
        _elements = grown;
    }
}
