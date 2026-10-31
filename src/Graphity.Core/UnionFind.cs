namespace Graphity;

/// <summary>
/// Union-find with path halving and union by rank, as static helpers over two arrays the caller
/// holds: <c>parent</c>, from <see cref="Singletons(int)"/>, and a <c>byte[] rank</c> of the same
/// length, all zero. <see cref="Union(int[], byte[], int, int)"/> reports whether it merged two
/// distinct sets, which is what lets a caller count components without a second pass. Union by
/// rank keeps every rank at most <c>log2(elementCount)</c>, so a byte per element suffices for any
/// array .NET can allocate.
/// </summary>
/// <remarks>
/// Static helpers rather than an object: <see cref="Components.WccsOf"/>, which the peer benchmarks
/// compare with other libraries, ran its own union-find over local arrays in this form, and sharing
/// it was measured there without a difference. The predicates in <see cref="Graphs"/> take the same
/// form, so there is one.
/// </remarks>
internal static class UnionFind
{
    /// <summary>A parent array of <paramref name="elementCount"/> singleton sets: every element is its own root.</summary>
    internal static int[] Singletons(int elementCount)
    {
        int[] parent = new int[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            parent[i] = i;
        }
        return parent;
    }

    /// <summary>The root of <paramref name="element"/>'s set.</summary>
    internal static int Find(int[] parent, int element)
    {
        int x = element;
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]]; // path halving
            x = parent[x];
        }
        return x;
    }

    /// <summary>
    /// Merges the sets of <paramref name="a"/> and <paramref name="b"/>; <c>false</c> if they were
    /// one set already. <c>rank[r]</c> bounds the height of the tree below root <c>r</c> and means
    /// nothing for a non-root.
    /// </summary>
    internal static bool Union(int[] parent, byte[] rank, int a, int b)
    {
        int ra = Find(parent, a);
        int rb = Find(parent, b);
        if (ra == rb)
        {
            return false;
        }
        if (rank[ra] < rank[rb])
        {
            parent[ra] = rb;
        }
        else if (rank[ra] > rank[rb])
        {
            parent[rb] = ra;
        }
        else
        {
            parent[rb] = ra;
            rank[ra]++;
        }
        return true;
    }
}
