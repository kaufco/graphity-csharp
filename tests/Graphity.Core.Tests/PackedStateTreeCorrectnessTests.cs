namespace Graphity.Core.Tests;

/// <summary>
/// Hard correctness check on a large random tree, to rule out any under-traversal or packed-state
/// bit bug that acyclic inputs could hide: a tree has fully determined results for every DFS
/// algorithm.
/// </summary>
public sealed class PackedStateTreeCorrectnessTests
{
    [Fact]
    public void AllDfsAlgorithmsAreCorrectOnLargeRandomTree()
    {
        const int n = 100_000;
        var rnd = new JavaRandom(7);
        var parent = new int[n];
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int i = 1; i < n; i++)
        {
            parent[i] = rnd.NextInt(i);
            b.AddEdge(parent[i], i); // out-tree rooted at 0, parent < child, acyclic
        }
        var tree = b.Build();

        // SCC: every node is its own component -> n singletons covering all nodes.
        var sccs = Components.SccsOf(tree);
        Assert.Equal(n, sccs.GroupCount);
        long covered = 0;
        for (int g = 0; g < sccs.GroupCount; g++)
        {
            Assert.Single(sccs.NodesOf(g));
            covered += sccs.NodesOf(g).Count;
        }
        Assert.Equal(n, covered); // proves every node was visited

        // IsDag: a tree is acyclic.
        Assert.True(Graphs.IsDag(tree));

        // TopOrder: complete and valid, parent before child for every edge.
        var order = TopOrder.Of(tree);
        Assert.Equal(n, order.Length);
        var pos = new int[n];
        for (int k = 0; k < n; k++)
        {
            pos[order[k]] = k;
        }
        for (int i = 1; i < n; i++)
        {
            Assert.True(pos[parent[i]] < pos[i]);
        }

        // BackEdges: none in a tree.
        Assert.Equal(0, BackEdges.Of(tree).Count);

        // EdgeTypes: every one of the n - 1 edges is a tree edge.
        var et = EdgeTypes.Of(tree);
        int treeEdges = 0;
        for (int e = 0; e < tree.EdgeCount; e++)
        {
            Assert.Equal(EdgeType.Tree, et.TypeOf(e));
            treeEdges++;
        }
        Assert.Equal(n - 1, treeEdges);
    }

    [Fact]
    public void PackedStateHandlesCyclesToo()
    {
        const int n = 1000;
        var b = new GraphBuilder();
        b.AddNodes(n);
        for (int i = 0; i < n; i++)
        {
            b.AddEdge(i, (i + 1) % n); // one big directed cycle
        }
        var cycle = b.Build();

        Assert.Equal(1, Components.SccsOf(cycle).GroupCount); // whole cycle = one SCC
        Assert.False(Graphs.IsDag(cycle));
        Assert.Equal(1, BackEdges.Of(cycle).Count); // exactly one back edge closes it
    }
}
