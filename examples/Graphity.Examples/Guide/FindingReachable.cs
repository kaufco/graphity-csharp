using Graphity;

namespace Examples.Guide;

/// <summary>
/// The methods a method may end up calling, and the methods that may end up calling it.
/// </summary>
internal static class FindingReachable
{
    /// <summary>The nodes reached forward.</summary>
    internal sealed record ForwardResult(SortedIntView FromLoad,
        SortedIntView FromLoadOrSave);

    /// <summary>
    /// Returns the methods that load, and load or save, may end up calling.
    /// </summary>
    internal static ForwardResult Forward()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int load = SmallProgram.NodeOf(names, "load");
        int save = SmallProgram.NodeOf(names, "save");
        // @start region="reachableFrom"
        SortedIntView fromLoad = Graphs.ReachableFrom(calls, load);
        // fromLoad: [3, 6, 8] - load, parse and read
        SortedIntView fromLoadOrSave = Graphs.ReachableFrom(calls, [load, save]);
        // fromLoadOrSave: [3, 4, 6, 7, 8, 9] - load, save, parse, format, read and write
        // @end
        return new ForwardResult(fromLoad, fromLoadOrSave);
    }

    /// <summary>Returns the methods that may end up calling format.</summary>
    internal static SortedIntView CallersOfFormat()
    {
        Graph calls = SmallProgram.Calls();
        int format = SmallProgram.NodeOf(SmallProgram.MethodNames(), "format");
        // @start region="reaching"
        // Turn every call around, to search from format to the methods that may end up
        // calling it.
        BiAdjacentGraph calledBy = BiAdjacentGraph.Of(calls).Transposed();
        SortedIntView affected = Graphs.ReachableFrom(calledBy, format);
        // affected: [0, 2, 4, 5, 7] - main, run, save, show and format itself
        // @end
        return affected;
    }
}
