using System.Globalization;
using System.Numerics;

namespace Graphity.ControlFlow;

/// <summary>
/// A set of <c>int</c>s by open addressing with linear probing, the counterpart of fastutil's
/// <c>IntOpenHashSet</c>, which <see cref="IteratedDominanceFrontier"/> uses in Java. Only the part
/// the module needs: construction for an expected size, <see cref="Add"/> and <see cref="Contains"/>.
/// </summary>
/// <remarks>
/// Ported with fastutil's layout and limits rather than replaced by <see cref="HashSet{T}"/>: one
/// <c>int</c> per slot, 0 marks a free slot and the key 0 is held in a flag, the table size is a power
/// of two at a load factor of 0.75, hashed with fastutil's <c>HashCommon.mix</c>. A table may have at
/// most 2^30 slots, so a set holds 805,306,368 keys (about 8 x 10^8); the next add throws
/// <see cref="ArgumentException"/> where fastutil throws <c>IllegalArgumentException</c>. Ported from
/// fastutil 8.5.17, the version the Java build uses.
/// </remarks>
internal sealed class IntOpenHashSet
{
    /// <summary>fastutil's <c>Hash.DEFAULT_LOAD_FACTOR</c>.</summary>
    internal const float DefaultLoadFactor = 0.75f;

    /// <summary>fastutil's <c>Hash.DEFAULT_INITIAL_SIZE</c>, the expected size of a set created without one.</summary>
    internal const int DefaultInitialSize = 16;

    /// <summary>The largest table, 2^30 slots.</summary>
    internal const int MaxTableSize = 1 << 30;

    /// <summary>fastutil's <c>HashCommon.INT_PHI</c>, 2^32 divided by the golden ratio.</summary>
    private const int IntPhi = unchecked((int)0x9E3779B9);

    /// <summary>The keys; slot <c>_n</c> stays free, 0 marks a free slot.</summary>
    private int[] _key;

    /// <summary>Table size minus one, for the slot of a hash.</summary>
    private int _mask;

    /// <summary>Whether the key 0 is in the set; it has no slot.</summary>
    private bool _containsNull;

    /// <summary>The table size, a power of two.</summary>
    private int _n;

    /// <summary>The number of keys at which the table grows.</summary>
    private int _maxFill;

    /// <summary>The number of keys, 0 included.</summary>
    private int _size;

    /// <summary>A set sized for <see cref="DefaultInitialSize"/> keys.</summary>
    internal IntOpenHashSet()
        : this(DefaultInitialSize)
    {
    }

    /// <summary>A set sized for <paramref name="expected"/> keys without growing.</summary>
    /// <exception cref="ArgumentException">The table for that many keys would exceed 2^30 slots.</exception>
    internal IntOpenHashSet(int expected)
    {
        _n = ArraySize(expected, DefaultLoadFactor);
        _mask = _n - 1;
        _maxFill = MaxFill(_n, DefaultLoadFactor);
        _key = new int[_n + 1];
    }

    /// <summary>The number of keys.</summary>
    internal int Count => _size;

    /// <summary>Adds <paramref name="k"/> and returns whether it was new.</summary>
    /// <exception cref="ArgumentException">The set would outgrow a table of 2^30 slots.</exception>
    internal bool Add(int k)
    {
        if (k == 0)
        {
            if (_containsNull)
            {
                return false;
            }
            _containsNull = true;
        }
        else
        {
            int[] key = _key;
            int pos = Mix(k) & _mask;
            int curr = key[pos];
            if (curr != 0)
            {
                if (curr == k)
                {
                    return false;
                }
                while ((curr = key[pos = (pos + 1) & _mask]) != 0)
                {
                    if (curr == k)
                    {
                        return false;
                    }
                }
            }
            key[pos] = k;
        }
        if (_size++ >= _maxFill)
        {
            Rehash(ArraySize(_size + 1, DefaultLoadFactor));
        }
        return true;
    }

    /// <summary>Whether <paramref name="k"/> is in the set.</summary>
    internal bool Contains(int k)
    {
        if (k == 0)
        {
            return _containsNull;
        }
        int[] key = _key;
        int pos = Mix(k) & _mask;
        int curr = key[pos];
        if (curr == 0)
        {
            return false;
        }
        if (curr == k)
        {
            return true;
        }
        while (true)
        {
            curr = key[pos = (pos + 1) & _mask];
            if (curr == 0)
            {
                return false;
            }
            if (curr == k)
            {
                return true;
            }
        }
    }

    /// <summary>Moves the keys into a table of <paramref name="newN"/> slots.</summary>
    private void Rehash(int newN)
    {
        int[] key = _key;
        int mask = newN - 1;
        var newKey = new int[newN + 1];
        int i = _n;
        for (int j = _containsNull ? _size - 1 : _size; j-- != 0;)
        {
            while (key[--i] == 0)
            {
            }
            int pos = Mix(key[i]) & mask;
            if (newKey[pos] != 0)
            {
                while (newKey[pos = (pos + 1) & mask] != 0)
                {
                }
            }
            newKey[pos] = key[i];
        }
        _n = newN;
        _mask = mask;
        _maxFill = MaxFill(_n, DefaultLoadFactor);
        _key = newKey;
    }

    /// <summary>fastutil's <c>HashCommon.mix</c>: multiply by <see cref="IntPhi"/>, fold the high half down.</summary>
    internal static int Mix(int x)
    {
        int h = unchecked(x * IntPhi);
        return h ^ (int)((uint)h >> 16);
    }

    /// <summary>
    /// fastutil's <c>HashCommon.maxFill</c>: the number of keys a table of <paramref name="n"/> slots
    /// holds at load factor <paramref name="f"/>, leaving at least one slot free. In double precision,
    /// as there.
    /// </summary>
    internal static int MaxFill(int n, float f) => Math.Min((int)Math.Ceiling(n * (double)f), n - 1);

    /// <summary>
    /// fastutil's <c>HashCommon.arraySize</c>: the smallest power of two, at least 2, that holds
    /// <paramref name="expected"/> keys at load factor <paramref name="f"/>. In double precision, as
    /// there.
    /// </summary>
    /// <exception cref="ArgumentException">The size would exceed <see cref="MaxTableSize"/>.</exception>
    internal static int ArraySize(int expected, float f)
    {
        long s = Math.Max(2L, NextPowerOfTwo((long)Math.Ceiling(expected / (double)f)));
        if (s > MaxTableSize)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"Too large ({expected} expected elements with load factor {f})"));
        }
        return (int)s;
    }

    /// <summary>fastutil's <c>HashCommon.nextPowerOfTwo(long)</c>; 1 for 0 and 1, as there.</summary>
    private static long NextPowerOfTwo(long x) => 1L << (64 - BitOperations.LeadingZeroCount((ulong)(x - 1)));
}
