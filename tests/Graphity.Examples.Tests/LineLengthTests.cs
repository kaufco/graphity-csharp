namespace Examples.Tests;

/// <summary>
/// Checks that no line of the examples is longer than 90 characters, counted with its indentation.
/// Longer lines wrap in the guide at its standard width; see the README of the examples.
/// </summary>
public sealed class LineLengthTests
{
    private const int MaxLine = 90;

    [Fact]
    public void NoLineOfTheExamplesIsLongerThanTheLimit()
    {
        string examples = Path.Combine(Sources.Root, "examples", "Graphity.Examples");
        char separator = Path.DirectorySeparatorChar;
        string[] files = Directory.GetFiles(examples, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}obj{separator}"))
            .Where(file => !file.Contains($"{separator}bin{separator}"))
            .ToArray();
        Assert.NotEmpty(files);
        List<string> failures = [];
        foreach (string file in files)
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > MaxLine)
                {
                    failures.Add($"{Path.GetRelativePath(Sources.Root, file)}:{i + 1}");
                }
            }
        }
        Assert.Empty(failures);
    }
}
