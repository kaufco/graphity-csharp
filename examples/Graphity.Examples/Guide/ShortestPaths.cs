using System.Collections.Immutable;
using Graphity;

namespace Examples.Guide;

/// <summary>
/// Shortest chains of calls, from one method and from the nearest of several.
/// </summary>
internal static class ShortestPaths
{
    /// <summary>What the example reads from one source.</summary>
    internal sealed record FromMainResult(int DistanceToWrite,
        ImmutableArray<int> PathToWrite, bool ReachesParseArgs);

    /// <summary>What the example reads from two sources.</summary>
    internal sealed record FromTwoResult(BfsDistances FromEither, int DistanceToFormat,
        ImmutableArray<int> PathToFormat, bool ReachesMain);

    /// <summary>Returns a shortest chain of calls from main to write.</summary>
    internal static FromMainResult FromMain()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int main = SmallProgram.NodeOf(names, "main");
        int write = SmallProgram.NodeOf(names, "write");
        int parseArgs = SmallProgram.NodeOf(names, "parseArgs");
        // @start region="pathTo"
        BfsDistances fromMain = BfsDistances.Of(calls, main);
        int distance = fromMain.DistanceOf(write);           // 3 - three calls away
        // [0, 2, 4, 9] - main, run, save, write
        ImmutableArray<int> path = fromMain.PathTo(write);
        bool reached = fromMain.IsReachable(parseArgs);      // true
        // @end
        return new FromMainResult(distance, path, reached);
    }

    /// <summary>Returns how many calls away from main the farthest method is.</summary>
    internal static int FarthestCall()
    {
        Graph calls = SmallProgram.Calls();
        int main = SmallProgram.NodeOf(SmallProgram.MethodNames(), "main");
        // @start region="maxDistance"
        // How many calls away from main is the farthest method?
        BfsDistances fromMain = BfsDistances.Of(calls, main);
        // 3 - every method is at most 3 calls away from main
        int farthest = fromMain.MaxDistance;
        // @end
        return farthest;
    }

    /// <summary>Returns the distances from the nearer of load and save.</summary>
    internal static FromTwoResult FromTwo()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int load = SmallProgram.NodeOf(names, "load");
        int save = SmallProgram.NodeOf(names, "save");
        int format = SmallProgram.NodeOf(names, "format");
        int main = SmallProgram.NodeOf(names, "main");
        // @start region="nearest"
        // The search starts at both load and save.
        BfsDistances fromEither = BfsDistances.Of(calls, [load, save]);
        int distance = fromEither.DistanceOf(format);           // 1 - save calls it
        ImmutableArray<int> path = fromEither.PathTo(format);   // [4, 7] - save, format
        bool reached = fromEither.IsReachable(main);            // false
        // @end
        return new FromTwoResult(fromEither, distance, path, reached);
    }
}
