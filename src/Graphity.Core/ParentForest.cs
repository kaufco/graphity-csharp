namespace Graphity;

/// <summary>
/// Helpers for a forest given as a parent array, -1 for a root - the form in which
/// <see cref="Ancestry"/> and <see cref="Dominators"/> hold their trees -, and the grouping by key
/// that its child lists are one case of: the partitions build their member lists with it as well.
/// </summary>
internal static class ParentForest
{
    /// <summary>
    /// Indices grouped by key as CSR: those with key <c>k</c> are
    /// <c>Members[Start[k]..Start[k + 1])</c>, ascending.
    /// </summary>
    internal readonly record struct Groups(int[] Start, int[] Members);

    /// <summary>
    /// The child lists of the forest, each ascending: <see cref="GroupIndices"/> with the parent as
    /// key. <b>Memory:</b> 4 x (nodeCount + 1 + childCount) bytes.
    /// </summary>
    internal static Groups ChildrenOf(int[] parent) => GroupIndices(parent, parent.Length);

    /// <summary>
    /// Groups the indices <c>i</c> of <paramref name="keyOf"/> by their key <c>keyOf[i]</c>, which
    /// lies in <c>0..keyCount-1</c>; an index with a negative key is in no group.
    /// </summary>
    internal static Groups GroupIndices(int[] keyOf, int keyCount)
    {
        var start = new int[keyCount + 1];
        foreach (int key in keyOf)
        {
            if (key >= 0)
            {
                start[key]++;
            }
        }
        return new Groups(start, GroupCounted(keyOf, start));
    }

    /// <summary>
    /// The second half of <see cref="GroupIndices"/>, for a caller that counts the keys in a pass it
    /// makes anyway. On entry <c>start[k]</c> holds the number of indices with key <c>k</c> and
    /// <c>start[keyCount]</c> is 0; on return <paramref name="start"/> holds the offsets of the
    /// groups.
    /// </summary>
    /// <returns>The members of all groups, grouped by key.</returns>
    internal static int[] GroupCounted(int[] keyOf, int[] start)
    {
        int keyCount = start.Length - 1;
        int next = 0;
        for (int key = 0; key < keyCount; key++)
        {
            int count = start[key];
            start[key] = next;
            next += count;
        }
        var members = new int[next];
        // The ascending scan leaves every group sorted; start[k] doubles as write pointer and is
        // shifted back afterwards.
        for (int i = 0; i < keyOf.Length; i++)
        {
            int key = keyOf[i];
            if (key >= 0)
            {
                members[start[key]++] = i;
            }
        }
        for (int key = keyCount; key > 0; key--)
        {
            start[key] = start[key - 1];
        }
        start[0] = 0;
        return members;
    }

    /// <summary>
    /// The canonical preorder of the forest - roots ascending, children ascending - with the size of
    /// every subtree. Fills the three given arrays, each of length nodeCount.
    /// <c>IteratedDominanceFrontier.Of</c> in <c>Graphity.ControlFlow</c> keeps a copy, extended by
    /// the depth of every node, and <c>Loops.Of</c> there copies this and <see cref="GroupCounted"/>;
    /// change them together. <b>Memory:</b> 4 x (nodeCount + 1 + childCount) bytes for the child
    /// lists.
    /// </summary>
    internal static void Preorder(int[] parent, int[] preorderIndex, int[] order, int[] subtreeSize)
    {
        int n = parent.Length;
        var children = ChildrenOf(parent);
        int[] start = children.Start;
        int[] nodes = children.Members;

        // Every node is pushed exactly once, so the stack fits in n slots; the subtree-size array
        // serves as the stack until it is filled below.
        int[] stack = subtreeSize;
        int top = 0;
        for (int node = n - 1; node >= 0; node--)
        {
            if (parent[node] < 0)
            {
                stack[top++] = node;
            }
        }
        int position = 0;
        while (top > 0)
        {
            int node = stack[--top];
            preorderIndex[node] = position;
            order[position++] = node;
            for (int i = start[node + 1] - 1; i >= start[node]; i--)
            {
                stack[top++] = nodes[i];
            }
        }

        // Reverse preorder visits every child before its parent.
        Array.Fill(subtreeSize, 1);
        for (int i = n - 1; i >= 0; i--)
        {
            int node = order[i];
            int p = parent[node];
            if (p >= 0)
            {
                subtreeSize[p] += subtreeSize[node];
            }
        }
    }
}
