using System.Collections.Immutable;
using Graphity;

namespace Examples.Guide;

/// <summary>Whether a graph has a cycle, and one of its cycles.</summary>
internal static class FindingCycles
{
    /// <summary>What the example finds.</summary>
    internal sealed record Found(bool DependenciesAcyclic, ImmutableArray<int> Cycle);

    /// <summary>
    /// Returns whether the packages have a cycle, and a cycle.
    /// </summary>
    internal static Found Cycles()
    {
        Graph dependencies = SmallProgram.Dependencies();
        // @start region="findCycle"
        bool dependenciesAcyclic = Graphs.IsDag(dependencies);         // false
        // [4, 5] - model, storage
        ImmutableArray<int> cycle = Graphs.FindCycle(dependencies);
        // @end
        return new Found(dependenciesAcyclic, cycle);
    }
}
