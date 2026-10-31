using System.Runtime.InteropServices;

namespace Graphity.Core.Tests;

/// <summary>One case of the conformance fixtures: a graph and the results of the Java library on it.</summary>
public sealed record FixtureCase(string Name, bool IsDag, int SccCount, int WccCount, int LevelCount)
{
    public override string ToString() => Name;
}

internal static class Fixtures
{
    /// <summary>
    /// The fixtures, copied next to the test assembly from <c>Conformance/</c>: written by the Java
    /// library, so that the port compares with Java's results instead of computing its own.
    /// </summary>
    internal static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "Conformance");

    internal static IEnumerable<object[]> Cases() =>
        File.ReadLines(Path.Combine(Dir, "cases.tsv"))
            .Skip(1)
            .Select(line => line.Split('\t'))
            .Select(f => new object[]
            {
                new FixtureCase(f[0], f[4] == "true", int.Parse(f[5]), int.Parse(f[6]), int.Parse(f[7])),
            });

    internal static Graph Graph(string file)
    {
        using var input = File.OpenRead(Path.Combine(Dir, file));
        return Graphity.Graph.ReadFrom(input);
    }

    internal static int[] Ints(FixtureCase c, string suffix) =>
        MemoryMarshal.Cast<byte, int>(File.ReadAllBytes(Path.Combine(Dir, $"{c.Name}.{suffix}"))).ToArray();

    internal static byte[] Bytes(string file) => File.ReadAllBytes(Path.Combine(Dir, file));
}
