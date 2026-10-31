using System.Collections;
using System.Diagnostics;

namespace Graphity.Core.Tests;

/// <summary>
/// The C# <see cref="SortedIntView"/> is a <c>readonly struct</c> over an array range, not a fastutil
/// <c>IntList</c>. Dropped from the Java test: the spliterator characteristics (no counterpart; LINQ
/// covers streaming), the bidirectional <c>listIterator</c> (enumeration is forward only; the indexer
/// covers the rest), <c>toArray(dest)</c> allocating a new array for a short destination (C#'s
/// <see cref="SortedIntView.CopyTo"/> follows span semantics and throws instead), equality with other
/// list types (equality is between views; spans and sequences compare with <c>SequenceEqual</c>), and
/// reference identity of sub-views (a struct has none - equality is checked instead).
/// </summary>
public sealed class SortedIntViewTests
{
    [Fact]
    public void ConstructorFullArrayCoversWholeArray()
    {
        var view = new SortedIntView([1, 3, 5, 7]);
        Assert.Equal(4, view.Count);
        Assert.Equal([1, 3, 5, 7], view.ToArray());
    }

    [Fact]
    public void ConstructorRangeSubsetCoversRange()
    {
        var view = new SortedIntView([1, 3, 5, 7, 9], 1, 4);
        Assert.Equal(3, view.Count);
        Assert.Equal([3, 5, 7], view.ToArray());
    }

    [Fact]
    public void ConstructorEmptyRangeIsEmpty()
    {
        var view = new SortedIntView([1, 2, 3], 2, 2);
        Assert.Empty(view);
        Assert.True(view.IsEmpty);
    }

    [Fact]
    public void ConstructorNegativeFromIndexThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SortedIntView([1, 2, 3], -1, 2));

    [Fact]
    public void ConstructorToIndexBeyondLengthThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SortedIntView([1, 2, 3], 0, 4));

    [Fact]
    public void ConstructorFromGreaterThanToThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SortedIntView([1, 2, 3], 2, 1));

    [Fact]
    public void EmptyConstantHasSizeZero()
    {
        Assert.Empty(SortedIntView.Empty);
        Assert.True(SortedIntView.Empty.Count == 0);
        Assert.True(SortedIntView.Empty.IsEmpty);
        Assert.Empty(SortedIntView.Empty.ToArray());
    }

    [Fact]
    public void DefaultIsTheEmptyView()
    {
        SortedIntView view = default;
        Assert.True(view.IsEmpty);
        Assert.Equal(SortedIntView.Empty, view);
        Assert.Equal(-1, view.IndexOf(0));
        Assert.False(view.Contains(0));
        Assert.Equal("[]", view.ToString());
        Assert.Empty((IEnumerable<int>)view);
    }

    [Fact]
    public void IndexerValidIndexReturnsElement()
    {
        var view = new SortedIntView([10, 20, 30, 40], 1, 4);
        Assert.Equal(20, view[0]);
        Assert.Equal(30, view[1]);
        Assert.Equal(40, view[2]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(100)]
    public void IndexerOutOfBoundsThrows(int index)
    {
        var view = new SortedIntView([1, 2, 3]);
        Assert.Throws<ArgumentOutOfRangeException>(() => view[index]);
    }

    [Fact]
    public void FirstReturnsSmallestElement() => Assert.Equal(5, new SortedIntView([5, 10, 15]).First());

    [Fact]
    public void LastReturnsLargestElement() => Assert.Equal(15, new SortedIntView([5, 10, 15]).Last());

    [Fact]
    public void FirstOnEmptyThrows() => Assert.Throws<InvalidOperationException>(() => SortedIntView.Empty.First());

    [Fact]
    public void LastOnEmptyThrows() => Assert.Throws<InvalidOperationException>(() => SortedIntView.Empty.Last());

    [Fact]
    public void FirstOnSubViewRespectsRange()
    {
        var view = new SortedIntView([1, 2, 3, 4, 5], 2, 5);
        Assert.Equal(3, view.First());
        Assert.Equal(5, view.Last());
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 1)]
    [InlineData(5, 2)]
    [InlineData(7, 3)]
    [InlineData(9, 4)]
    [InlineData(0, -1)]
    [InlineData(2, -1)]
    [InlineData(4, -1)]
    [InlineData(10, -1)]
    public void IndexOfOnFullView(int value, int expectedIndex) =>
        Assert.Equal(expectedIndex, new SortedIntView([1, 3, 5, 7, 9]).IndexOf(value));

    [Fact]
    public void IndexOfOnSubViewReturnsViewRelativeIndex()
    {
        var view = new SortedIntView([1, 3, 5, 7, 9], 2, 5);
        Assert.Equal(0, view.IndexOf(5));
        Assert.Equal(1, view.IndexOf(7));
        Assert.Equal(2, view.IndexOf(9));
        Assert.Equal(-1, view.IndexOf(3));
        Assert.Equal(-1, view.IndexOf(1));
    }

    [Fact]
    public void LastIndexOfBehavesLikeIndexOf()
    {
        var view = new SortedIntView([1, 3, 5, 7, 9]);
        Assert.Equal(view.IndexOf(5), view.LastIndexOf(5));
        Assert.Equal(view.IndexOf(4), view.LastIndexOf(4));
    }

    [Fact]
    public void ContainsUsesTheSortedOrder()
    {
        var view = new SortedIntView([1, 3, 5]);
        Assert.True(view.Contains(3));
        Assert.False(view.Contains(4));
        Assert.False(new SortedIntView([1, 3, 5, 7], 1, 3).Contains(7));
    }

    [Fact]
    public void IndexOfInRangeReturnsAbsoluteIndex()
    {
        int[] array = [1, 3, 5, 7, 9];
        Assert.Equal(2, SortedIntView.IndexOfInRange(array, 2, 5, 5));
        Assert.Equal(3, SortedIntView.IndexOfInRange(array, 2, 5, 7));
        Assert.Equal(-1, SortedIntView.IndexOfInRange(array, 2, 5, 3));
    }

    [Fact]
    public void ContainsInRangeDetectsMembership()
    {
        int[] array = [1, 3, 5, 7, 9];
        Assert.True(SortedIntView.ContainsInRange(array, 2, 5, 7));
        Assert.False(SortedIntView.ContainsInRange(array, 2, 5, 3));
    }

    [Fact]
    public void ToArrayEmptyReturnsEmptyArray() => Assert.Empty(SortedIntView.Empty.ToArray());

    [Fact]
    public void ToArraySubViewReturnsCopyOfRange() =>
        Assert.Equal([3, 5, 7], new SortedIntView([1, 3, 5, 7, 9], 1, 4).ToArray());

    [Fact]
    public void ToArrayReturnsIndependentCopy()
    {
        int[] backing = [1, 3, 5];
        var view = new SortedIntView(backing);
        int[] copy = view.ToArray();
        copy[0] = 999;
        Assert.Equal(1, backing[0]);
    }

    [Fact]
    public void CopyToExactSizeDestination()
    {
        var view = new SortedIntView([1, 3, 5]);
        var dest = new int[3];
        view.CopyTo(dest);
        Assert.Equal([1, 3, 5], dest);
    }

    [Fact]
    public void CopyToLargerDestinationKeepsTheRest()
    {
        var view = new SortedIntView([1, 3, 5]);
        int[] dest = [9, 9, 9, 9, 9];
        view.CopyTo(dest);
        Assert.Equal([1, 3, 5, 9, 9], dest);
    }

    [Fact]
    public void CopyToTooSmallDestinationThrows()
    {
        var view = new SortedIntView([1, 3, 5]);
        Assert.Throws<ArgumentException>(() => view.CopyTo(new int[2]));
    }

    [Fact]
    public void SliceThenCopyToCopiesARange()
    {
        // Java's getElements(1, dest, 0, 3).
        var view = new SortedIntView([1, 3, 5, 7, 9]);
        var dest = new int[4];
        view.Slice(1, 3).CopyTo(dest);
        Assert.Equal([3, 5, 7, 0], dest);
    }

    [Fact]
    public void SliceInvalidSourceRangeThrows()
    {
        var view = new SortedIntView([1, 3, 5]);
        Assert.Throws<ArgumentOutOfRangeException>(() => view.Slice(0, 5));
    }

    [Fact]
    public void ForeachVisitsAllInOrder()
    {
        var view = new SortedIntView([1, 3, 5, 7], 1, 4);
        var collected = new List<int>();
        foreach (int v in view)
        {
            collected.Add(v);
        }
        Assert.Equal([3, 5, 7], collected);
    }

    [Fact]
    public void EnumeratorThroughTheInterfaceTraversesForward()
    {
        IEnumerable<int> view = new SortedIntView([10, 20, 30]);
        using var it = view.GetEnumerator();
        Assert.True(it.MoveNext());
        Assert.Equal(10, it.Current);
        Assert.True(it.MoveNext());
        Assert.Equal(20, it.Current);
        Assert.True(it.MoveNext());
        Assert.Equal(30, it.Current);
        Assert.False(it.MoveNext());
    }

    [Fact]
    public void NonGenericEnumeratorTraversesForward()
    {
        IEnumerable view = new SortedIntView([10, 20, 30], 1, 3);
        var collected = new List<object>();
        foreach (object value in view)
        {
            collected.Add(value);
        }
        Assert.Equal([20, 30], collected.Cast<int>());
    }

    [Fact]
    public void ForeachWorksInAnIterator()
    {
        // A ref struct enumerator could not live across yield return (CS4007); the view's can.
        static IEnumerable<int> Doubled(SortedIntView view)
        {
            foreach (int v in view)
            {
                yield return 2 * v;
            }
        }
        Assert.Equal([6, 10, 14], Doubled(new SortedIntView([1, 3, 5, 7], 1, 4)));
    }

    [Fact]
    public async Task ForeachWorksAcrossAnAwait()
    {
        var view = new SortedIntView([1, 3, 5, 7], 1, 4);
        var collected = new List<int>();
        foreach (int v in view)
        {
            await Task.Yield();
            collected.Add(v);
        }
        Assert.Equal([3, 5, 7], collected);
    }

    [Fact]
    public void TheEnumeratorOfTheDefaultViewEndsAtOnce()
    {
        SortedIntView.Enumerator it = default(SortedIntView).GetEnumerator();
        Assert.False(it.MoveNext());
        Assert.False(it.MoveNext());
    }

    [Fact]
    public void TheEnumeratorDoesNotSupportReset()
    {
        IEnumerator<int> it = ((IEnumerable<int>)new SortedIntView([1, 2])).GetEnumerator();
        Assert.True(it.MoveNext());
        Assert.Throws<NotSupportedException>(it.Reset);
    }

    [Fact]
    public void TheDebuggerShowsTheCountNotTheValues()
    {
        // ToString lists every value, as Java's does; the debugger would build it at every step.
        var display = Assert.IsType<DebuggerDisplayAttribute>(
            Assert.Single(typeof(SortedIntView).GetCustomAttributes(typeof(DebuggerDisplayAttribute), false)));
        Assert.Equal("Count = {Count}", display.Value);
    }

    [Fact]
    public void EnumeratorOfAnEmptySubViewEndsAtOnce()
    {
        IEnumerable<int> view = new SortedIntView([10, 20], 2, 2);
        using var it = view.GetEnumerator();
        Assert.False(it.MoveNext());
    }

    [Fact]
    public void BackwardTraversalByIndex()
    {
        var view = new SortedIntView([10, 20, 30]);
        var collected = new List<int>();
        for (int i = view.Count - 1; i >= 0; i--)
        {
            collected.Add(view[i]);
        }
        Assert.Equal([30, 20, 10], collected);
    }

    [Fact]
    public void LinqStreamsTheViewSequentiallyAndInParallel()
    {
        var view = new SortedIntView([0, 1, 3, 5, 7, 9], 1, 6);
        Assert.Equal([1, 3, 5, 7, 9], view.Order().ToArray());
        Assert.Equal([1, 3, 5, 7, 9], view.AsParallel().OrderBy(x => x).ToArray());
        Assert.Equal(16, new SortedIntView([1, 3, 5, 7]).Sum());
        Assert.Equal(3, Enumerable.Count(new SortedIntView([1, 3, 5, 7], 1, 4)));
    }

    [Fact]
    public void ConvertsImplicitlyToASpanOverTheBackingArray()
    {
        int[] backing = [1, 3, 5, 7];
        ReadOnlySpan<int> span = new SortedIntView(backing, 1, 3);
        Assert.Equal(2, span.Length);
        Assert.Equal([3, 5], span.ToArray());
        backing[1] = 4;
        Assert.Equal(4, span[0]);
    }

    [Fact]
    public void SliceFullRangeEqualsTheView()
    {
        var view = new SortedIntView([1, 3, 5]);
        Assert.Equal(view, view.Slice(0, 3));
    }

    [Fact]
    public void SliceEmptyRangeReturnsEmpty()
    {
        var view = new SortedIntView([1, 3, 5]);
        Assert.Equal(SortedIntView.Empty, view.Slice(1, 0));
        Assert.True(view[1..1].IsEmpty);
    }

    [Fact]
    public void SlicePartialRangeCreatesSubView()
    {
        var view = new SortedIntView([1, 3, 5, 7, 9]);
        Assert.Equal([3, 5, 7], view.Slice(1, 3).ToArray());
        Assert.Equal([3, 5, 7], view[1..4].ToArray());
        Assert.Equal([7, 9], view[^2..].ToArray());
    }

    [Fact]
    public void SliceOutOfBoundsThrows()
    {
        var view = new SortedIntView([1, 3, 5]);
        Assert.Throws<ArgumentOutOfRangeException>(() => view.Slice(-1, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => view.Slice(0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => view.Slice(2, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => view.Slice(int.MaxValue, 1));
    }

    [Fact]
    public void SliceFromZeroToLessThanSizeCreatesSubView()
    {
        var view = new SortedIntView([1, 3, 5]);
        var sub = view.Slice(0, 2);
        Assert.NotEqual(view, sub);
        Assert.Equal([1, 3], sub.ToArray());
    }

    [Fact]
    public void ViewOfEmptyRangeReturnsEmpty() =>
        Assert.Equal(SortedIntView.Empty, SortedIntView.ViewOf([1, 3, 5], 2, 2));

    [Fact]
    public void ViewOfNonEmptyRangeCreatesView() =>
        Assert.Equal([3, 5], SortedIntView.ViewOf([1, 3, 5, 7], 1, 3).ToArray());

    [Fact]
    public void ViewOfInvalidRangeThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SortedIntView.ViewOf([1, 3], 0, 5));

    [Fact]
    public void ViewOfUnsortedRangeThrows()
    {
        var ex = Assert.Throws<ArgumentException>(() => SortedIntView.ViewOf([5, 1, 3], 0, 3));
        Assert.Contains("1 follows 5", ex.Message);
    }

    [Fact]
    public void ViewOfRangeWithDuplicatesThrows()
    {
        var ex = Assert.Throws<ArgumentException>(() => SortedIntView.ViewOf([1, 2, 2, 3], 0, 4));
        Assert.Contains("2 follows 2", ex.Message);
    }

    [Fact]
    public void ViewOfChecksOnlyItsRange() =>
        Assert.Equal([1, 3, 5], SortedIntView.ViewOf([9, 1, 3, 5, 0], 1, 4).ToArray());

    [Fact]
    public void ViewOfSharesTheArray()
    {
        int[] backing = [1, 3, 5];
        var view = SortedIntView.ViewOf(backing, 0, 3);
        backing[1] = 4;
        Assert.Equal([1, 4, 5], view.ToArray());
    }

    [Fact]
    public void EqualsSameInstanceTrue()
    {
        var view = new SortedIntView([1, 3, 5]);
        Assert.True(view.Equals(view));
    }

    [Fact]
    public void EqualsSameContentDifferentBackingArrayTrue()
    {
        var a = new SortedIntView([1, 3, 5]);
        var b = new SortedIntView([1, 3, 5]);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void EqualsSameContentViaSubViewTrue()
    {
        var full = new SortedIntView([0, 1, 3, 5, 9], 1, 4);
        var direct = new SortedIntView([1, 3, 5]);
        Assert.Equal(direct, full);
    }

    [Fact]
    public void EqualsDifferentSizeFalse()
    {
        var a = new SortedIntView([1, 3, 5]);
        var b = new SortedIntView([1, 3]);
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void EqualsSameSizeDifferentContentFalse() =>
        Assert.NotEqual(new SortedIntView([1, 3, 5]), new SortedIntView([1, 3, 6]));

    [Fact]
    public void ContentComparesWithOtherSequences()
    {
        var view = new SortedIntView([0, 1, 3, 5], 1, 4);
        Assert.True(view.AsSpan().SequenceEqual([1, 3, 5]));
        Assert.True(view.SequenceEqual(new List<int> { 1, 3, 5 }));
        Assert.False(view.SequenceEqual(new List<int> { 1, 3, 6 }));
        Assert.False(view.SequenceEqual(new List<int> { 1, 3 }));
    }

    [Fact]
    public void EqualsWithNullFalse() => Assert.False(new SortedIntView([1, 3, 5]).Equals(null));

    [Fact]
    public void EqualsWithUnrelatedTypeFalse()
    {
        Assert.False(new SortedIntView([1, 3, 5]).Equals("not a list"));
        Assert.False(new SortedIntView([1, 3, 5]).Equals(new[] { 1, 3, 5 }));
    }

    [Fact]
    public void HashCodeMatchesTheJavaListContract()
    {
        // 31 * (31 * (31 * 1 + 1) + 3) + 5, as java.util.List.hashCode computes it.
        Assert.Equal(30850, new SortedIntView([1, 3, 5]).GetHashCode());
        Assert.Equal(1, SortedIntView.Empty.GetHashCode());
    }

    [Fact]
    public void HashCodeConsistentWithEquals()
    {
        var a = new SortedIntView([1, 3, 5, 7, 9], 1, 4);
        var b = new SortedIntView([3, 5, 7]);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToStringListsTheElements()
    {
        Assert.Equal("[1, 3, 5]", new SortedIntView([1, 3, 5]).ToString());
        Assert.Equal("[3]", new SortedIntView([1, 3, 5], 1, 2).ToString());
    }

    [Fact]
    public void ToStringWritesNegativeValuesAsJavaDoesInEveryCulture()
    {
        using var culture = new ForeignCulture();
        Assert.Equal("[-3, -1, 2]", SortedIntView.ViewOf([-3, -1, 2], 0, 3).ToString());
    }

    [Fact]
    public void UnionBothEmptyReturnsEmpty() => Assert.True(SortedIntView.Empty.Union(SortedIntView.Empty).IsEmpty);

    [Fact]
    public void UnionThisEmptyReturnsOther()
    {
        var other = new SortedIntView([1, 3, 5]);
        Assert.Equal(other, SortedIntView.Empty.Union(other));
    }

    [Fact]
    public void UnionOtherEmptyReturnsThis()
    {
        var self = new SortedIntView([1, 3, 5]);
        Assert.Equal(self, self.Union(SortedIntView.Empty));
    }

    [Fact]
    public void UnionDisjointRangesMergesAndSorts() =>
        Assert.Equal([1, 2, 3, 4, 5, 6], new SortedIntView([1, 3, 5]).Union(new SortedIntView([2, 4, 6])).ToArray());

    [Fact]
    public void UnionOverlappingDeduplicates() =>
        Assert.Equal([1, 2, 3, 4, 5, 6], new SortedIntView([1, 2, 3, 4]).Union(new SortedIntView([3, 4, 5, 6])).ToArray());

    [Fact]
    public void UnionIdenticalReturnsSameContent() =>
        Assert.Equal([1, 3, 5], new SortedIntView([1, 3, 5]).Union(new SortedIntView([1, 3, 5])).ToArray());

    [Fact]
    public void UnionSubsetReturnsLargerSet() =>
        Assert.Equal([1, 2, 3, 4, 5], new SortedIntView([1, 2, 3, 4, 5]).Union(new SortedIntView([2, 4])).ToArray());

    [Fact]
    public void UnionInRangeWithOffsetRangesDeduplicates()
    {
        int[] a = [0, 1, 3, 5, 9];
        int[] b = [0, 3, 4, 5, 9];
        Assert.Equal([1, 3, 4, 5], SortedIntView.UnionInRange(a, 1, 4, b, 1, 4));
    }

    // The size of a union against the largest array, on small limits: two views that reach the
    // real one take gigabytes.

    [Fact]
    public void UnionCapacityIsTheSumOfTheSizesWithinTheLimit()
    {
        int[] a = [1, 2, 3];
        int[] b = [2, 3, 4];
        Assert.Equal(6, SortedIntView.UnionCapacity(a, 0, 3, b, 0, 3, 6));
    }

    [Fact]
    public void UnionCapacityCountsTheUnionWhenTheSumExceedsTheLimit()
    {
        // The sizes add up to 6; the union {1, 2, 3, 4} has 4 values.
        int[] a = [1, 2, 3];
        int[] b = [2, 3, 4];
        Assert.Equal(4, SortedIntView.UnionCapacity(a, 0, 3, b, 0, 3, 5));
        Assert.Equal(4, SortedIntView.UnionCapacity(a, 0, 3, b, 0, 3, 4));
        var e = Assert.Throws<ArgumentException>(() => SortedIntView.UnionCapacity(a, 0, 3, b, 0, 3, 3));
        Assert.Contains("4 values", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnionCapacityCountsTheUnionOfRandomRanges()
    {
        // With the limit at the size of the union, the count runs whenever the ranges share a value.
        var random = new JavaRandom(42);
        for (int round = 0; round < 500; round++)
        {
            int[] a = RandomSet(random);
            int[] b = RandomSet(random);
            int aFrom = random.NextInt(a.Length + 1);
            int aTo = aFrom + random.NextInt(a.Length - aFrom + 1);
            int bFrom = random.NextInt(b.Length + 1);
            int bTo = bFrom + random.NextInt(b.Length - bFrom + 1);
            int size = SortedIntView.UnionInRange(a, aFrom, aTo, b, bFrom, bTo).Length;
            Assert.Equal(size, SortedIntView.UnionCapacity(a, aFrom, aTo, b, bFrom, bTo, size));
            if (size > 0)
            {
                Assert.Throws<ArgumentException>(() => SortedIntView.UnionCapacity(a, aFrom, aTo, b, bFrom, bTo, size - 1));
            }
        }
    }

    /// <summary>Up to 20 distinct values in -30..29, sorted ascending.</summary>
    private static int[] RandomSet(JavaRandom random)
    {
        int count = random.NextInt(21);
        var values = new SortedSet<int>();
        for (int i = 0; i < count; i++)
        {
            values.Add(random.NextInt(60) - 30);
        }
        return [.. values];
    }

    [Fact]
    public void IsAReadOnlyListOfInts()
    {
        IReadOnlyList<int> list = new SortedIntView([2, 4, 6], 1, 3);
        Assert.Equal(2, list.Count);
        Assert.Equal(6, list[1]);
    }
}
