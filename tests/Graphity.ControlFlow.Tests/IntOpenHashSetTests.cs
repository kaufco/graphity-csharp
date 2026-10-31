namespace Graphity.ControlFlow.Tests;

/// <summary>
/// The module's counterparts of fastutil's <c>IntOpenHashSet</c> and <c>IntArrayList</c>, which the
/// Java code takes from the library and so has no tests of its own: set semantics against
/// <see cref="HashSet{T}"/>, and the size limit that <see cref="IteratedDominanceFrontier.FrontierOf"/>
/// documents.
/// </summary>
public sealed class IntOpenHashSetTests
{
    [Fact]
    public void AddReportsNewKeysAndContainsFindsThem()
    {
        var set = new IntOpenHashSet();
        Assert.True(set.Add(5));
        Assert.False(set.Add(5));
        Assert.True(set.Add(0));
        Assert.False(set.Add(0));
        Assert.True(set.Add(-7));
        Assert.True(set.Add(int.MinValue));
        Assert.True(set.Add(int.MaxValue));
        Assert.Equal(5, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(-7));
        Assert.True(set.Contains(int.MinValue));
        Assert.False(set.Contains(6));
        Assert.False(new IntOpenHashSet().Contains(0));
    }

    [Fact]
    public void GrowingKeepsEveryKey()
    {
        var rnd = new JavaRandom(42);
        var set = new IntOpenHashSet(4);
        var expected = new HashSet<int>();
        for (int i = 0; i < 20_000; i++)
        {
            int key = rnd.NextInt(30_000) - 10_000;
            Assert.Equal(expected.Add(key), set.Add(key));
        }
        Assert.Equal(expected.Count, set.Count);
        for (int key = -10_000; key < 20_000; key++)
        {
            Assert.Equal(expected.Contains(key), set.Contains(key));
        }
    }

    [Fact]
    public void TableSizesFollowFastutil()
    {
        // Power of two, at least 2, at load factor 0.75.
        Assert.Equal(2, IntOpenHashSet.ArraySize(0, IntOpenHashSet.DefaultLoadFactor));
        Assert.Equal(2, IntOpenHashSet.ArraySize(1, IntOpenHashSet.DefaultLoadFactor));
        Assert.Equal(32, IntOpenHashSet.ArraySize(16, IntOpenHashSet.DefaultLoadFactor));
        Assert.Equal(2048, IntOpenHashSet.ArraySize(1024, IntOpenHashSet.DefaultLoadFactor));
        Assert.Equal(12, IntOpenHashSet.MaxFill(16, IntOpenHashSet.DefaultLoadFactor));
        Assert.Equal(1, IntOpenHashSet.MaxFill(2, IntOpenHashSet.DefaultLoadFactor));
        // fastutil's HashCommon.mix.
        Assert.Equal(0, IntOpenHashSet.Mix(0));
        Assert.Equal(unchecked((int)0x9E3779B9) ^ (int)(0x9E3779B9u >> 16), IntOpenHashSet.Mix(1));
    }

    [Fact]
    public void TheLargestTableHoldsAboutEightHundredMillionKeys()
    {
        // 2^30 slots at load factor 0.75: 805,306,368 keys fit, a table for more is rejected with the
        // exception Java's IllegalArgumentException maps to.
        Assert.Equal(IntOpenHashSet.MaxTableSize, IntOpenHashSet.ArraySize(805_306_368, IntOpenHashSet.DefaultLoadFactor));
        Assert.Equal(805_306_368, IntOpenHashSet.MaxFill(IntOpenHashSet.MaxTableSize, IntOpenHashSet.DefaultLoadFactor));
        var ex = Assert.Throws<ArgumentException>(() => IntOpenHashSet.ArraySize(806_000_000, IntOpenHashSet.DefaultLoadFactor));
        Assert.Equal("Too large (806000000 expected elements with load factor 0.75)", ex.Message);
        Assert.Throws<ArgumentException>(() => new IntOpenHashSet(int.MaxValue));
    }

    [Fact]
    public void IntArrayListGrowsAndCopiesOut()
    {
        var list = new IntArrayList();
        Assert.Equal(0, list.Count);
        Assert.Empty(list.ToArray());
        for (int i = 0; i < 1000; i++)
        {
            list.Add(i * 3);
        }
        Assert.Equal(1000, list.Count);
        Assert.Equal(2997, list[999]);
        int[] copy = list.ToArray();
        Assert.Equal(1000, copy.Length);
        Assert.Equal(1500, copy[500]);
    }
}
