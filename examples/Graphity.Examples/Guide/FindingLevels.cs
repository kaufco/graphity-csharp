using Graphity;

namespace Examples.Guide;

/// <summary>
/// The build steps in stages: the steps of one stage can run at the same time.
/// </summary>
internal static class FindingLevels
{
    /// <summary>The stages and two values read on the way.</summary>
    internal sealed record StagesResult(List<List<string>> Stages, int LevelOfTest,
        int LevelCount);

    /// <summary>Returns the build steps grouped by level.</summary>
    internal static StagesResult Stages()
    {
        Graph steps = SmallProgram.BuildSteps();
        string[] names = SmallProgram.StepNames();
        int test = SmallProgram.NodeOf(names, "test");
        // @start region="levels"
        TopOrderAndLevels levels = TopOrderAndLevels.Of(steps);
        // 3 - three steps run before it, one after the other
        int levelOfTest = levels.LevelOf(test);
        int levelCount = levels.LevelCount;       // 7

        // One stage per level: the steps of a stage have no edge between them.
        List<List<string>> stages = [];
        for (int level = 0; level < levelCount; level++)
        {
            stages.Add([]);
        }
        foreach (int step in levels.Order)
        {
            stages[levels.LevelOf(step)].Add(names[step]);
        }
        // stages: [[generate, lint], [compile], [compileTests, docs], [test], [package],
        //          [sign], [publish]]
        // @end
        return new StagesResult(stages, levelOfTest, levelCount);
    }
}
