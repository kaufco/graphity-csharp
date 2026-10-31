using Graphity;

namespace Examples;

/// <summary>Following edges against their direction.</summary>
internal static class Transposing
{
    /// <summary>
    /// Returns <c>model</c> and the packages that depend on it, directly or indirectly.
    /// </summary>
    internal static SortedIntView AffectedByModel()
    {
        Graph dependencies = SmallProgram.Dependencies();
        // @start region="transposed"
        int model = 4;
        BiAdjacentGraph usedBy = BiAdjacentGraph.Of(dependencies).Transposed();
        SortedIntView affected = Graphs.ReachableFrom(usedBy, model);
        // affected: [0, 2, 3, 4, 5] - app, ui, service, model and storage
        // @end
        return affected;
    }
}
