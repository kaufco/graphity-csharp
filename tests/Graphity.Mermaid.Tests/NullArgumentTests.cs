namespace Graphity.Mermaid.Tests;

/// <summary>
/// The null contract of the package, as for the core: a <see langword="null"/> argument where the type
/// of a parameter does not allow one throws <see cref="ArgumentNullException"/>.
/// </summary>
/// <remarks>Not in Java, whose library checks nothing at run time.</remarks>
public sealed class NullArgumentTests
{
    private static readonly Graph Sample = TestGraphs.Path(3);

    private static object SampleOf(Type type)
    {
        if (type == typeof(Graph))
        {
            return Sample;
        }
        if (type == typeof(MermaidWriter))
        {
            return MermaidWriter.Of(Sample);
        }
        if (type == typeof(NodePartition))
        {
            return Components.WccsOf(Sample);
        }
        if (type == typeof(string))
        {
            return "x";
        }
        if (type == typeof(Func<int, string?>))
        {
            return new Func<int, string?>(_ => null);
        }
        if (type == typeof(Func<int, MermaidWriter.Shape?>))
        {
            return new Func<int, MermaidWriter.Shape?>(_ => null);
        }
        if (type == typeof(Func<int, bool>))
        {
            return new Func<int, bool>(_ => true);
        }
        if (type == typeof(TextWriter))
        {
            return new StringWriter();
        }
        throw new InvalidOperationException($"no sample of {type}");
    }

    [Fact]
    public void EveryPublicEntryPointRejectsNull()
    {
        var result = NullArguments.Check(typeof(MermaidWriter).Assembly, SampleOf);
        Assert.Empty(result.Failures);
        Assert.True(result.Checked >= 12, $"only {result.Checked} parameters checked");
        Assert.Empty(result.NotCallable);
    }
}
