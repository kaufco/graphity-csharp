namespace Examples.Tests;

/// <summary>
/// The files of the repository that the tests read: the sources of the examples, the README, the
/// package overviews and the sources of the packages. The tests run from the build output inside the
/// repository, which holds <c>Graphity.slnx</c> at its root.
/// </summary>
internal static class Sources
{
    private static readonly Lazy<string> RootDirectory = new(FindRoot);

    /// <summary>The root of the repository, the directory of <c>Graphity.slnx</c>.</summary>
    internal static string Root => RootDirectory.Value;

    /// <summary>The text of a file, by its path from the root, with line feeds as line breaks.</summary>
    internal static string Read(string path)
    {
        string? text = ReadOrNull(path);
        Assert.True(text is not null, $"{path} is missing");
        return text;
    }

    /// <summary>The text of a file, by its path from the root, or <see langword="null"/> if there is none.</summary>
    internal static string? ReadOrNull(string path)
    {
        string file = Path.Combine(Root, path);
        return File.Exists(file) ? File.ReadAllText(file).ReplaceLineEndings("\n") : null;
    }

    /// <summary>The source of an example, by its path below the examples, such as <c>Connectivity.cs</c>.</summary>
    internal static string? Example(string file) => ReadOrNull(Path.Combine("examples", "Graphity.Examples", file));

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Graphity.slnx")))
            {
                return dir.FullName;
            }
        }
        throw new InvalidOperationException($"no Graphity.slnx above {AppContext.BaseDirectory}");
    }
}
