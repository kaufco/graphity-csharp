namespace Graphity.TestUtils;

/// <summary>
/// The linear congruential generator of <c>java.util.Random</c>, bit for bit, so that the seeded
/// generators of this project produce the same graphs as the Java test utilities for the same seeds.
/// </summary>
public sealed class JavaRandom
{
    private const long Multiplier = 0x5DEECE66DL;
    private const long Addend = 0xBL;
    private const long Mask = (1L << 48) - 1;
    private const double DoubleUnit = 1.0 / (1L << 53);

    private long _seed;

    /// <summary>A generator with the given seed, as <c>new java.util.Random(seed)</c>.</summary>
    public JavaRandom(long seed)
    {
        _seed = (seed ^ Multiplier) & Mask;
    }

    private int Next(int bits)
    {
        _seed = unchecked((_seed * Multiplier) + Addend) & Mask;
        return (int)((ulong)_seed >> (48 - bits));
    }

    /// <summary>A uniformly distributed <c>int</c>, as Java's <c>nextInt()</c>.</summary>
    public int NextInt() => Next(32);

    /// <summary>A uniformly distributed value in <c>0..bound-1</c>, as Java's <c>nextInt(bound)</c>.</summary>
    public int NextInt(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);
        int r = Next(31);
        int m = bound - 1;
        if ((bound & m) == 0)
        {
            return (int)((bound * (long)r) >> 31);
        }
        for (int u = r; unchecked(u - (r = u % bound) + m) < 0; u = Next(31))
        {
        }
        return r;
    }

    /// <summary>A uniformly distributed <c>long</c>, as Java's <c>nextLong()</c>.</summary>
    public long NextLong() => unchecked(((long)Next(32) << 32) + Next(32));

    /// <summary>A uniformly distributed <c>bool</c>, as Java's <c>nextBoolean()</c>.</summary>
    public bool NextBoolean() => Next(1) != 0;

    /// <summary>A uniformly distributed value in <c>[0, 1)</c>, as Java's <c>nextDouble()</c>.</summary>
    public double NextDouble() => (((long)Next(26) << 27) + Next(27)) * DoubleUnit;
}
