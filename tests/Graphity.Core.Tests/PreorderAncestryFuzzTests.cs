namespace Graphity.Core.Tests;

/// <summary>
/// Cross-validates <see cref="PreorderAncestry"/> against <see cref="Ancestry"/>, whose ancestor test
/// goes through a lowest-common-ancestor query instead of preorder ranges, and against a preorder
/// walk of the forest itself.
/// </summary>
public sealed class PreorderAncestryFuzzTests
{
    private const int Iterations = 60;
    private const int QueriesPerGraph = 300;

    [Fact]
    public void IsAncestorOfMatchesTheLowestCommonAncestorTest()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyForest(seed);
            int n = labeled.Graph.NodeCount;
            var ancestry = Ancestry.Of(labeled.Graph);
            var preorder = PreorderAncestry.Of(ancestry);

            var rnd = new JavaRandom((seed * 31) + 4);
            for (int q = 0; q < QueriesPerGraph; q++)
            {
                int node = rnd.NextInt(n);
                int descendant = rnd.NextBoolean() ? rnd.NextInt(n) : SomeDescendant(ancestry, node, rnd);
                bool expected = ancestry.LowestCommonAncestorOf(node, descendant) == node;
                Assert.True(expected == preorder.IsAncestorOf(node, descendant),
                    $"IsAncestorOf({node}, {descendant}) for {labeled.Describe()}");
            }
        }
    }

    [Fact]
    public void PreorderAndSubtreesMatchAWalkOfTheForest()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyForest(seed);
            var forest = labeled.Graph;
            int n = forest.NodeCount;
            var ancestry = Ancestry.Of(forest);
            var preorder = PreorderAncestry.Of(ancestry);

            var expected = new List<int>(n);
            for (int node = 0; node < n; node++)
            {
                if (ancestry.ParentOf(node) < 0)
                {
                    Walk(forest, node, expected);
                }
            }
            Assert.True(expected.SequenceEqual(preorder.Preorder), $"preorder for {labeled.Describe()}");

            for (int node = 0; node < n; node++)
            {
                var subtree = new List<int>();
                Walk(forest, node, subtree);
                Assert.True(preorder.SubtreeOf(node).SequenceEqual(subtree.ToArray()),
                    $"subtree of {node} for {labeled.Describe()}");
                Assert.Equal(subtree.Count, preorder.SubtreeSizeOf(node));
                Assert.Equal(node, preorder.Preorder[preorder.PreorderIndexOf(node)]);
            }
        }
    }

    /// <summary>Preorder walk over the forest's own adjacency, children in their stored ascending order.</summary>
    private static void Walk(Graph forest, int root, List<int> output)
    {
        var stack = new Stack<int>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            int node = stack.Pop();
            output.Add(node);
            var children = forest.OutNodes(node);
            for (int i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }
        }
    }

    /// <summary>A random node on the path from <paramref name="node"/> down, found by walking up from random nodes.</summary>
    private static int SomeDescendant(Ancestry ancestry, int node, JavaRandom rnd)
    {
        int n = ancestry.Parent.Length;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int candidate = rnd.NextInt(n);
            for (int x = candidate; x >= 0; x = ancestry.ParentOf(x))
            {
                if (x == node)
                {
                    return candidate;
                }
            }
        }
        return node;
    }
}
