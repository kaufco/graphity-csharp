using System.Collections.Immutable;
using Graphity;

namespace Examples.Guide;

/// <summary>The build steps in an order that respects every edge.</summary>
internal static class SortingTopologically
{
    /// <summary>Returns the build steps in topological order, by name.</summary>
    internal static List<string> RunOrder()
    {
        Graph steps = SmallProgram.BuildSteps();
        string[] names = SmallProgram.StepNames();
        // @start region="topOrder"
        ImmutableArray<int> order = TopOrder.Of(steps);   // [2, 0, 1, 4, 3, 5, 6, 7, 8]
        List<string> runOrder = [.. order.Select(step => names[step])];
        // runOrder: [lint, generate, compile, docs, compileTests, test, package, sign,
        //            publish]
        // @end
        return runOrder;
    }
}
