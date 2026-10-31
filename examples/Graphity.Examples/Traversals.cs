using System.Collections.Immutable;
using Graphity;

namespace Examples;

/// <summary>The type of an edge, and the edges of another type.</summary>
internal sealed record Classified(EdgeType LoopEdge, Edges ForwardEdges);

/// <summary>A distance, and a shortest path of that length.</summary>
internal sealed record Chain(int Depth, ImmutableArray<int> Path);

/// <summary>Traversals of your own, and what the built-in searches find.</summary>
/// <remarks>
/// Java declares each traverser as a local class inside the example; C# declares no types
/// inside a method, so each example here is a nested class and the method that runs it,
/// and a method without parameters around it hands it the graph and the start node, as
/// the other examples set them up.
/// </remarks>
internal static class Traversals
{
    /// <summary>
    /// Returns the methods that <c>main</c> reaches, each after the methods it calls.
    /// </summary>
    internal static List<int> CalleesFirstFromMain()
    {
        Graph calls = SmallProgram.Calls();
        int main = SmallProgram.NodeOf(SmallProgram.MethodNames(), "main");
        return RunCalleesFirst(calls, main);
    }

    // @start region="leaveNode"
    private sealed class CalleesFirst(Graph calls) : DfsTraverser(calls)
    {
        public List<int> Order { get; } = [];

        protected override bool EnterNode(int method) => true;

        protected override void LeaveNode(int method) => Order.Add(method);
    }

    private static List<int> RunCalleesFirst(Graph calls, int main)
    {
        var calleesFirst = new CalleesFirst(calls);
        calleesFirst.Run(main);
        // calleesFirst.Order: [1, 6, 8, 3, 7, 9, 4, 5, 2, 0] - parseArgs, parse, read,
        // load, format, write, save, show, run and main
        return calleesFirst.Order;
    }
    // @end

    /// <summary>
    /// Returns the edge index of the dependency that closes the first cycle the search
    /// finds.
    /// </summary>
    internal static int FirstCycleEdge() => RunFirstCycle(SmallProgram.Dependencies());

    // @start region="visitEdge"
    private sealed class FirstCycle(Graph dependencies) : DfsEdgeTraverser(dependencies)
    {
        public int ClosingEdge { get; private set; } = -1;

        protected override EdgeAction VisitEdge(
            int source, int target, int edgeIndex, TargetState state)
        {
            if (state == TargetState.OnStack)
            {
                ClosingEdge = edgeIndex;
                return EdgeAction.Stop;
            }
            return base.VisitEdge(source, target, edgeIndex, state);
        }
    }

    private static int RunFirstCycle(Graph dependencies)
    {
        var firstCycle = new FirstCycle(dependencies);
        firstCycle.Run();
        // firstCycle.ClosingEdge: 10 - the edge storage -> model
        return firstCycle.ClosingEdge;
    }
    // @end

    /// <summary>
    /// Returns the methods that <c>main</c> reaches with at most two calls, nearest
    /// first.
    /// </summary>
    internal static List<int> WithinTwoCallsOfMain()
    {
        Graph calls = SmallProgram.Calls();
        int main = SmallProgram.NodeOf(SmallProgram.MethodNames(), "main");
        return RunWithinTwoCalls(calls, main);
    }

    // @start region="currentLevel"
    private sealed class WithinTwoCalls(Graph calls) : FrontierTraverser(calls)
    {
        public List<int> Reached { get; } = [];

        protected override void VisitNode(int method)
        {
            Reached.Add(method);
            if (CurrentLevel < 2)
            {
                EnqueueChildren(method);
            }
        }
    }

    private static List<int> RunWithinTwoCalls(Graph calls, int main)
    {
        var withinTwoCalls = new WithinTwoCalls(calls);
        withinTwoCalls.Run(main);
        // withinTwoCalls.Reached: [0, 1, 2, 3, 4, 5] - main, parseArgs, run, load, save
        // and show
        return withinTwoCalls.Reached;
    }
    // @end

    /// <summary>
    /// Returns the type of the edge that closes the loop of <c>load</c>, and its forward
    /// edges.
    /// </summary>
    internal static Classified EdgeTypesOfLoad()
    {
        Graph cfg = SmallProgram.Cfg();
        string[] names = SmallProgram.BlockNames();
        int header = SmallProgram.NodeOf(names, "header");
        int next = SmallProgram.NodeOf(names, "next");
        // @start region="edgeTypes"
        EdgeTypes types = EdgeTypes.Of(cfg);
        EdgeType loopEdge = types.TypeOf(cfg.EdgeIndexOf(next, header));
        Edges forwardEdges = Edges.Of(types, EdgeType.Forward);
        // loopEdge: Back
        // forwardEdges: body -> next
        // @end
        return new Classified(loopEdge, forwardEdges);
    }

    /// <summary>
    /// Returns the length of a shortest chain of calls from <c>main</c> to <c>parse</c>.
    /// </summary>
    internal static Chain CallChainToParse()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int main = SmallProgram.NodeOf(names, "main");
        int parse = SmallProgram.NodeOf(names, "parse");
        // @start region="pathTo"
        BfsDistances fromMain = BfsDistances.Of(calls, main);
        int depth = fromMain.DistanceOf(parse);
        ImmutableArray<int> chain = fromMain.PathTo(parse);
        // depth: 3
        // chain: [0, 2, 3, 6] - main -> run -> load -> parse
        // @end
        return new Chain(depth, chain);
    }
}
