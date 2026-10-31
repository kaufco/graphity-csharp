namespace Graphity.Core.Tests;

/// <summary>
/// Cross-validates <see cref="Ancestry"/> against two independent oracles: a lowest common ancestor
/// by intersecting ancestor sets (<see cref="NaiveOracles.LowestCommonAncestor"/>, in place of
/// JGraphT's NaiveLCAFinder), and a parent walk that decides the ancestor relation without any
/// preprocessing.
/// </summary>
public sealed class AncestryFuzzTests
{
    private const int Iterations = 60;
    private const int QueriesPerGraph = 200;

    [Fact]
    public void LowestCommonAncestorMatchesTheAncestorSetOracle()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyForest(seed);
            var forest = labeled.Graph;
            int n = forest.NodeCount;
            var ancestry = Ancestry.Of(forest);

            var rnd = new JavaRandom((seed * 31) + 1);
            for (int q = 0; q < QueriesPerGraph; q++)
            {
                int a = rnd.NextInt(n);
                int b = rnd.NextInt(n);
                int expected = NaiveOracles.LowestCommonAncestor(forest, a, b);
                Assert.True(expected == ancestry.LowestCommonAncestorOf(a, b), $"lca({a}, {b}) for {labeled.Describe()}");
            }
        }
    }

    [Fact]
    public void LowestCommonAncestorIsSymmetricAndReflexive()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyForest(seed);
            int n = labeled.Graph.NodeCount;
            var ancestry = Ancestry.Of(labeled.Graph);

            var rnd = new JavaRandom((seed * 31) + 2);
            for (int q = 0; q < QueriesPerGraph; q++)
            {
                int a = rnd.NextInt(n);
                int b = rnd.NextInt(n);
                Assert.True(ancestry.LowestCommonAncestorOf(b, a) == ancestry.LowestCommonAncestorOf(a, b),
                    $"symmetry of lca({a}, {b}) for {labeled.Describe()}");
                Assert.True(ancestry.LowestCommonAncestorOf(a, a) == a, $"reflexivity at {a} for {labeled.Describe()}");
                int root = RootOf(ancestry, a);
                Assert.True(ancestry.LowestCommonAncestorOf(root, a) == root,
                    $"lca of {a} with its root {root} for {labeled.Describe()}");
            }
        }
    }

    [Fact]
    public void IsAncestorOfMatchesAParentWalk()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyForest(seed);
            int n = labeled.Graph.NodeCount;
            var ancestry = Ancestry.Of(labeled.Graph);

            var rnd = new JavaRandom((seed * 31) + 3);
            for (int q = 0; q < QueriesPerGraph; q++)
            {
                int node = rnd.NextInt(n);
                int descendant = rnd.NextInt(n);
                Assert.True(ReachesByWalkingUp(ancestry, node, descendant) == ancestry.IsAncestorOf(node, descendant),
                    $"IsAncestorOf({node}, {descendant}) for {labeled.Describe()}");
            }
        }
    }

    [Fact]
    public void DepthAndParentAgreeWithTheGraph()
    {
        for (long seed = 0; seed < Iterations; seed++)
        {
            var labeled = RandomGraphs.AnyForest(seed);
            var forest = labeled.Graph;
            var ancestry = Ancestry.Of(forest);

            for (int node = 0; node < forest.NodeCount; node++)
            {
                foreach (int child in forest.OutNodes(node))
                {
                    Assert.True(ancestry.ParentOf(child) == node, $"parent of {child} for {labeled.Describe()}");
                    Assert.True(ancestry.DepthOf(child) == ancestry.DepthOf(node) + 1,
                        $"depth of {child} for {labeled.Describe()}");
                }
            }
        }
    }

    /// <summary>Walks from <paramref name="node"/> to the root of its tree, one parent at a time.</summary>
    private static int RootOf(Ancestry ancestry, int node)
    {
        int root = node;
        for (int up = ancestry.ParentOf(root); up >= 0; up = ancestry.ParentOf(root))
        {
            root = up;
        }
        return root;
    }

    /// <summary>Walks from <paramref name="descendant"/> to its root, one parent at a time.</summary>
    private static bool ReachesByWalkingUp(Ancestry ancestry, int node, int descendant)
    {
        for (int x = descendant; x >= 0; x = ancestry.ParentOf(x))
        {
            if (x == node)
            {
                return true;
            }
        }
        return false;
    }
}
