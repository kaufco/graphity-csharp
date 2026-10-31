using Graphity;

namespace Examples.Guide;

/// <summary>
/// The edges and nodes whose removal splits the loop of the method Load.
/// </summary>
internal static class SplittingGroups
{
    /// <summary>The strong bridges and strong articulation points.</summary>
    internal sealed record Splitters(Edges StrongBridges,
        SortedIntView StrongArticulationPoints);

    /// <summary>
    /// Returns the strong bridges and strong articulation points of the control flow of
    /// load.
    /// </summary>
    internal static Splitters OfLoad()
    {
        Graph cfg = SmallProgram.Cfg();
        // @start region="strongConnectivity"
        // The loop of cfg - header, body, then, next - is a strongly connected component.
        // The analysis follows edges against their direction too.
        StrongConnectivity strong =
            Components.StrongConnectivityOf(BiAdjacentGraph.Of(cfg));
        Edges strongBridges = strong.StrongBridges;
        // strongBridges: header -> body, body -> then, then -> next, next -> header
        SortedIntView strongArticulationPoints = strong.StrongArticulationPoints;
        // strongArticulationPoints: [1, 2, 4] - header, body and next
        // @end
        return new Splitters(strongBridges, strongArticulationPoints);
    }
}
