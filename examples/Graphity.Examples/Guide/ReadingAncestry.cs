using System.Collections.Immutable;
using Graphity;

namespace Examples.Guide;

/// <summary>Parents, generations, ancestors and descendants in the family tree.</summary>
internal static class ReadingAncestry
{
    /// <summary>Parents and depths.</summary>
    internal sealed record ParentsResult(int ParentOfGreta, int GenerationOfGreta,
        int ParentOfAda, int[] Generations);

    /// <summary>Lowest common ancestors.</summary>
    internal sealed record CommonResult(int GretaAndIda, int GretaAndEmil,
        int HugoAndGreta, int DoraAndHugo);

    /// <summary>Ancestor tests.</summary>
    internal sealed record TestsResult(bool BenIsAncestor, bool CleoIsAncestor,
        bool HugoIsAncestor, bool FastBenIsAncestor);

    /// <summary>Subtrees; the subtree copied.</summary>
    internal sealed record SubtreesResult(int[] SubtreeOfBen, int SubtreeSizeOfBen,
        ImmutableArray<int> Preorder, int PositionOfCleo);

    /// <summary>
    /// Returns the parent and generation of Greta, the parent of Ada and every
    /// generation.
    /// </summary>
    internal static ParentsResult Parents()
    {
        Graph family = SmallFamily.Tree();
        int ada = SmallFamily.PersonOf("Ada");
        int greta = SmallFamily.PersonOf("Greta");
        // @start region="parentOf"
        Ancestry ancestry = Ancestry.Of(family);
        int parent = ancestry.ParentOf(greta);         // 3 - Dora
        int generation = ancestry.DepthOf(greta);      // 3 - three generations below Ada
        int parentOfAda = ancestry.ParentOf(ada);      // -1 - Ada is the root

        // The generation of every person, by node ID.
        int[] generations = new int[family.NodeCount];
        for (int person = 0; person < generations.Length; person++)
        {
            generations[person] = ancestry.DepthOf(person);
        }
        // generations: [0, 1, 1, 2, 2, 2, 3, 3, 3, 3] - Ada in 0, Greta, Hugo, Ida and
        // Jonas in 3
        // @end
        return new ParentsResult(parent, generation, parentOfAda, generations);
    }

    /// <summary>Returns the lowest common ancestors of four pairs.</summary>
    internal static CommonResult Common()
    {
        Graph family = SmallFamily.Tree();
        int dora = SmallFamily.PersonOf("Dora");
        int emil = SmallFamily.PersonOf("Emil");
        int greta = SmallFamily.PersonOf("Greta");
        int hugo = SmallFamily.PersonOf("Hugo");
        int ida = SmallFamily.PersonOf("Ida");
        // @start region="lowestCommonAncestor"
        Ancestry ancestry = Ancestry.Of(family);
        // 0 - Ada
        int commonOfSecondCousins = ancestry.LowestCommonAncestorOf(greta, ida);
        // 1 - Ben
        int commonOfUncleAndNiece = ancestry.LowestCommonAncestorOf(greta, emil);
        // 3 - Dora
        int commonOfSiblings = ancestry.LowestCommonAncestorOf(hugo, greta);
        // 3 - Dora herself
        int commonOfMotherAndSon = ancestry.LowestCommonAncestorOf(dora, hugo);
        // @end
        return new CommonResult(commonOfSecondCousins, commonOfUncleAndNiece,
            commonOfSiblings, commonOfMotherAndSon);
    }

    /// <summary>
    /// Returns some ancestor tests, with Ancestry and with PreorderAncestry.
    /// </summary>
    internal static TestsResult Tests()
    {
        Graph family = SmallFamily.Tree();
        int ben = SmallFamily.PersonOf("Ben");
        int cleo = SmallFamily.PersonOf("Cleo");
        int hugo = SmallFamily.PersonOf("Hugo");
        // @start region="isAncestorOf"
        Ancestry ancestry = Ancestry.Of(family);
        // true - his grandfather
        bool benIsAncestor = ancestry.IsAncestorOf(ben, hugo);
        // false - his great-aunt
        bool cleoIsAncestor = ancestry.IsAncestorOf(cleo, hugo);
        // true - his own ancestor
        bool hugoIsAncestor = ancestry.IsAncestorOf(hugo, hugo);

        // For many tests, the index with the preorder answers each one in constant time.
        PreorderAncestry index = PreorderAncestry.Of(ancestry);
        bool fastBenIsAncestor = index.IsAncestorOf(ben, hugo);    // true
        // @end
        return new TestsResult(benIsAncestor, cleoIsAncestor, hugoIsAncestor,
            fastBenIsAncestor);
    }

    /// <summary>Returns the subtree of Ben and the preorder of the family.</summary>
    internal static SubtreesResult Subtrees()
    {
        Graph family = SmallFamily.Tree();
        int ben = SmallFamily.PersonOf("Ben");
        int cleo = SmallFamily.PersonOf("Cleo");
        // @start region="subtreeOf"
        PreorderAncestry index = PreorderAncestry.Of(Ancestry.Of(family));
        ReadOnlySpan<int> subtree = index.SubtreeOf(ben);
        // subtree: [1, 3, 6, 7, 4] - Ben, Dora, Greta, Hugo, Emil
        int size = index.SubtreeSizeOf(ben);   // 5 - Ben included

        // The whole family in preorder, and where Cleo stands in it.
        ImmutableArray<int> order = index.Preorder;
        // order: [0, 1, 3, 6, 7, 4, 2, 5, 8, 9] - Ada, then the subtree of Ben, then that
        // of Cleo

        // 6 - after Ada and Ben's subtree
        int positionOfCleo = index.PreorderIndexOf(cleo);
        // @end
        return new SubtreesResult(subtree.ToArray(), size, order, positionOfCleo);
    }
}
