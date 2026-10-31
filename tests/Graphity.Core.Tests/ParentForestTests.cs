namespace Graphity.Core.Tests;

public sealed class ParentForestTests
{
    [Fact]
    public void GroupIndicesListsEachGroupAscendingAndSkipsNegativeKeys()
    {
        var groups = ParentForest.GroupIndices([2, -1, 0, 2, -1, 0, 3], 5);
        Assert.Equal([0, 2, 2, 4, 5, 5], groups.Start);
        Assert.Equal([2, 5, 0, 3, 6], groups.Members);
    }

    [Fact]
    public void GroupIndicesWithoutKeysOrIndices()
    {
        var noKeys = ParentForest.GroupIndices([-1, -1], 0);
        Assert.Equal([0], noKeys.Start);
        Assert.Empty(noKeys.Members);
        var noIndices = ParentForest.GroupIndices([], 3);
        Assert.Equal([0, 0, 0, 0], noIndices.Start);
        Assert.Empty(noIndices.Members);
    }

    [Fact]
    public void GroupIndicesMatchesAScanPerKey()
    {
        var random = new JavaRandom(7);
        for (int round = 0; round < 500; round++)
        {
            int keyCount = random.NextInt(12);
            var keyOf = new int[random.NextInt(40)];
            for (int i = 0; i < keyOf.Length; i++)
            {
                keyOf[i] = random.NextInt(keyCount + 2) - 2; // -2 and -1 are in no group
            }
            var groups = ParentForest.GroupIndices(keyOf, keyCount);
            Assert.Equal(keyCount + 1, groups.Start.Length);
            int at = 0;
            for (int key = 0; key < keyCount; key++)
            {
                Assert.Equal(at, groups.Start[key]);
                for (int i = 0; i < keyOf.Length; i++)
                {
                    if (keyOf[i] == key)
                    {
                        Assert.Equal(i, groups.Members[at++]);
                    }
                }
            }
            Assert.Equal(at, groups.Start[keyCount]);
            Assert.Equal(at, groups.Members.Length);
        }
    }

    [Fact]
    public void ChildrenOfGroupsByParent()
    {
        var children = ParentForest.ChildrenOf([-1, 0, 0, 1, -1, 4]);
        Assert.Equal([0, 2, 3, 3, 3, 4, 4], children.Start);
        Assert.Equal([1, 2, 3, 5], children.Members);
    }

    [Fact]
    public void PreorderListsRootsAndChildrenAscendingWithSubtreeSizes()
    {
        int[] parent = [-1, 0, 0, 1, -1, 4];
        var preorderIndex = new int[6];
        var order = new int[6];
        var subtreeSize = new int[6];
        ParentForest.Preorder(parent, preorderIndex, order, subtreeSize);
        Assert.Equal([0, 1, 3, 2, 4, 5], order);
        Assert.Equal([0, 1, 3, 2, 4, 5], preorderIndex);
        Assert.Equal([4, 2, 1, 1, 2, 1], subtreeSize);
    }
}
