namespace Graphity.ControlFlow.Tests;

/// <summary>
/// Oracle for the loop nesting forest, taken literally from its definition by strongly connected
/// components: the outermost loops are the components with an edge, the header of each is the node
/// that a depth-first search from the entries reaches first, and the nested loops are the components
/// of the rest once the header is taken out. Recursive over the nesting and
/// O(NodeCount x (NodeCount + EdgeCount)); meant for graphs of a few hundred nodes.
/// </summary>
public sealed class NaiveLoops
{
    private readonly Graph _graph;
    private readonly int[] _entries;

    /// <summary>Builds the forest of <paramref name="graph"/> searched from <paramref name="entries"/>.</summary>
    public NaiveLoops(Graph graph, ReadOnlySpan<int> entries)
    {
        _graph = graph;
        _entries = entries.ToArray();
        int n = graph.NodeCount;
        Preorder = SearchOrder(graph, entries);
        Reachable = new bool[n];
        var all = new List<int>();
        for (int v = 0; v < n; v++)
        {
            if (Preorder[v] >= 0)
            {
                Reachable[v] = true;
                all.Add(v);
            }
        }
        LoopOfHeader = new Loop?[n];
        Decompose(all, -1);
    }

    /// <summary>One loop: its header, its body with nested loops, and the header of its enclosing loop.</summary>
    public sealed record Loop(int Header, bool[] Body, int ParentHeader);

    /// <summary>Loops keyed by header, <c>null</c> for a node that heads none.</summary>
    public Loop?[] LoopOfHeader { get; }

    /// <summary>Reachability from the entries.</summary>
    public bool[] Reachable { get; }

    /// <summary>Preorder position of each node in the search, -1 if unreachable.</summary>
    public int[] Preorder { get; }

    /// <summary>Records the loops among <paramref name="region"/>, all inside the loop headed by <paramref name="parentHeader"/>.</summary>
    private void Decompose(List<int> region, int parentHeader)
    {
        var mapping = NodeMapping.ForSubgraph(region.ToArray());
        var sub = Graphs.Remapped(_graph, mapping);
        var sccs = Components.SccsOf(sub);
        for (int c = 0; c < sccs.GroupCount; c++)
        {
            if (!sccs.ContainsCycle(c))
            {
                continue;
            }
            var members = sccs.NodesOf(c);
            int header = -1;
            var body = new bool[_graph.NodeCount];
            foreach (int member in members)
            {
                int node = mapping.Get(member);
                body[node] = true;
                if (header < 0 || Preorder[node] < Preorder[header])
                {
                    header = node;
                }
            }
            LoopOfHeader[header] = new Loop(header, body, parentHeader);
            var rest = new List<int>();
            foreach (int member in members)
            {
                int node = mapping.Get(member);
                if (node != header)
                {
                    rest.Add(node);
                }
            }
            rest.Sort();
            Decompose(rest, header);
        }
    }

    /// <summary>
    /// The entries of a loop: its nodes with a reachable predecessor outside it, and the entries of
    /// the graph inside it.
    /// </summary>
    public List<int> EntriesOf(Loop loop)
    {
        int n = _graph.NodeCount;
        var isGraphEntry = new bool[n];
        foreach (int entry in _entries)
        {
            isGraphEntry[entry] = true;
        }
        var result = new List<int>();
        for (int x = 0; x < n; x++)
        {
            if (!loop.Body[x])
            {
                continue;
            }
            bool entered = isGraphEntry[x];
            for (int p = 0; p < n && !entered; p++)
            {
                if (Reachable[p] && !loop.Body[p] && _graph.HasOutNode(p, x))
                {
                    entered = true;
                }
            }
            if (entered)
            {
                result.Add(x);
            }
        }
        return result;
    }

    /// <summary>Preorder of a depth-first search from the entries in list order, successors ascending.</summary>
    private static int[] SearchOrder(Graph graph, ReadOnlySpan<int> entries)
    {
        int n = graph.NodeCount;
        var preorder = new int[n];
        Array.Fill(preorder, -1);
        var nodeStack = new List<int>();
        var cursorStack = new List<int>();
        int count = 0;
        foreach (int entry in entries)
        {
            if (preorder[entry] >= 0)
            {
                continue;
            }
            preorder[entry] = count++;
            nodeStack.Add(entry);
            cursorStack.Add(0);
            while (nodeStack.Count > 0)
            {
                int top = nodeStack.Count - 1;
                int u = nodeStack[top];
                int cursor = cursorStack[top];
                var outs = graph.OutNodes(u);
                if (cursor == outs.Count)
                {
                    nodeStack.RemoveAt(top);
                    cursorStack.RemoveAt(top);
                    continue;
                }
                cursorStack[top] = cursor + 1;
                int v = outs[cursor];
                if (preorder[v] < 0)
                {
                    preorder[v] = count++;
                    nodeStack.Add(v);
                    cursorStack.Add(0);
                }
            }
        }
        return preorder;
    }
}
