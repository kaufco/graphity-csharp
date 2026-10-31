namespace Graphity.ControlFlow.Tests;

/// <summary>
/// The null contract of the package, as for the core: a <see langword="null"/> argument where the type
/// of a parameter does not allow one throws <see cref="ArgumentNullException"/>.
/// </summary>
/// <remarks>Not in Java, whose library checks nothing at run time.</remarks>
public sealed class NullArgumentTests
{
    private static readonly BiAdjacentGraph Sample = BiAdjacentGraph.Of(TestGraphs.Of(3, 0, 1, 1, 2, 2, 0));

    private static object SampleOf(Type type)
    {
        if (type == typeof(Graph) || type == typeof(BiAdjacentGraph))
        {
            return Sample;
        }
        if (type == typeof(Dominators))
        {
            return Dominators.Of(Sample, 0);
        }
        throw new InvalidOperationException($"no sample of {type}");
    }

    [Fact]
    public void EveryPublicEntryPointRejectsNull()
    {
        var result = NullArguments.Check(typeof(Loops).Assembly, SampleOf);
        Assert.Empty(result.Failures);
        Assert.True(result.Checked >= 3, $"only {result.Checked} parameters checked");
        Assert.Equal(["Loops.Of(BiAdjacentGraph, ReadOnlySpan`1)"], result.NotCallable);
        Assert.Equal("graph", Assert.Throws<ArgumentNullException>(() => Loops.Of(null!, [0])).ParamName);
    }
}
