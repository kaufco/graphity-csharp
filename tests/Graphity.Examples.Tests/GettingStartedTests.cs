namespace Examples.Tests;

public sealed class GettingStartedTests
{
    [Fact]
    public void ProgramWritesTheCycleOfModelAndStorage()
    {
        TextWriter original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            GettingStarted.Main();
        }
        finally
        {
            Console.SetOut(original);
        }
        Assert.Equal(["[model, storage]"], Names.LinesOf(output.ToString()));
    }

    // The program keeps its data to itself, so the source must hold the data of SmallProgram: the
    // examples that continue the program in the README work on SmallProgram.
    [Fact]
    public void ProgramBuildsTheDependenciesOfSmallProgram()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string names = $"string[] names =\n            [{string.Join(", ", SmallProgram.PackageNames().Select(name => $"\"{name}\""))}];";
        string uses = "int[][] uses = ["
            + string.Join(", ", dependencies.Nodes().Select(pkg => $"[{string.Join(", ", dependencies.OutNodes(pkg))}]"))
            + "];";
        string? source = Sources.Example("GettingStarted.cs");
        Assert.NotNull(source);
        Assert.Contains(names, source, StringComparison.Ordinal);
        Assert.Contains(uses, source, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCycleIsTheNodes4And5ModelAndStorage()
    {
        Assert.Equal([4, 5], Graphs.FindCycle(SmallProgram.Dependencies()).AsSpan().ToArray());
        Assert.Equal("model", SmallProgram.PackageNames()[4]);
        Assert.Equal("storage", SmallProgram.PackageNames()[5]);
    }
}
