using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Graphity;

/// <summary>
/// An <see cref="Ancestry"/> that also holds the preorder of its forest, for an ancestor test in O(1)
/// and the nodes of every subtree.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Of"/> builds it from an index and shares the arrays of that index. In preorder, every
/// subtree is a contiguous range: the subtree of <c>node</c> takes the positions
/// <c>PreorderIndexOf(node)..PreorderIndexOf(node) + SubtreeSizeOf(node) - 1</c> of
/// <see cref="Preorder"/>. So <see cref="IsAncestorOf"/> is a range test, and
/// <see cref="SubtreeOf"/> returns a view of that range.
/// </para>
/// <para>
/// The preorder visits the roots in ascending order, and the children of each node in ascending
/// order. If the node IDs were assigned in this order, as is usual for a syntax tree numbered in
/// source order, the preorder is <c>0..NodeCount-1</c>, and the subtree of <c>node</c> is
/// <c>node..node + SubtreeSizeOf(node) - 1</c>.
/// </para>
/// <para>Instances are immutable and safe to share between threads.</para>
/// <para><b>Memory:</b> 12 x NodeCount bytes on top of the <see cref="Ancestry"/> it extends.</para>
/// </remarks>
/// <example>
/// Here, the basic blocks of the method <c>load</c> that <c>body</c> dominates, from the
/// <c>dominators</c> of <c>load</c>:
/// <!-- example: Dominance.cs#subtreeOf -->
/// <code language="csharp">
/// PreorderAncestry dominance = PreorderAncestry.Of(Ancestry.Of(dominators));
/// ReadOnlySpan&lt;int> dominatedByBody = dominance.SubtreeOf(body);
/// // dominatedByBody: [2, 3, 4] - body, then and next
/// </code>
/// </example>
public sealed class PreorderAncestry : Ancestry
{
    // The position of each node in the preorder.
    private readonly int[] _preorderIndex;

    // The number of nodes in the subtree of each node, the node itself included.
    private readonly int[] _subtreeSize;

    private PreorderAncestry(Ancestry ancestry, int[] preorderIndex, int[] subtreeSize, int[] order)
        : base(ancestry.Parent, ancestry.Depth, ancestry.ChainHead)
    {
        _preorderIndex = preorderIndex;
        _subtreeSize = subtreeSize;
        Preorder = ImmutableCollectionsMarshal.AsImmutableArray(order);
    }

    /// <summary>
    /// Returns a <see cref="PreorderAncestry"/> for the forest of <paramref name="ancestry"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The result shares the arrays of <paramref name="ancestry"/> instead of copying them. The preorder
    /// is computed anew on every call, not cached: keep the result to use it more than once. If
    /// <paramref name="ancestry"/> already is a <see cref="PreorderAncestry"/>, returns it.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(NodeCount), or O(1) if already a <see cref="PreorderAncestry"/>.<br/>
    /// <b>Memory:</b> +12 x NodeCount bytes for the result; while it runs, 8 x NodeCount bytes more;
    /// none if already a <see cref="PreorderAncestry"/>.
    /// </para>
    /// </remarks>
    /// <param name="ancestry">The index to add the preorder to.</param>
    /// <returns>The index with the preorder.</returns>
    public static PreorderAncestry Of(Ancestry ancestry)
    {
        ArgumentNullException.ThrowIfNull(ancestry);
        if (ancestry is PreorderAncestry preorderAncestry)
        {
            return preorderAncestry;
        }
        int n = ancestry.Parent.Length;
        var preorderIndex = new int[n];
        var order = new int[n];
        var subtreeSize = new int[n];
        ParentForest.Preorder(ancestry.Parent, preorderIndex, order, subtreeSize);
        return new PreorderAncestry(ancestry, preorderIndex, subtreeSize, order);
    }

    /// <summary>
    /// Returns whether <paramref name="node"/> is an ancestor of <paramref name="descendant"/>: whether
    /// it lies on the path from the root of its tree to <paramref name="descendant"/>. A node is its own
    /// ancestor.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <param name="descendant">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="node"/> is an ancestor of <paramref name="descendant"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="node"/> or <paramref name="descendant"/> is out of range.
    /// </exception>
    public override bool IsAncestorOf(int node, int descendant)
    {
        Checks.Index(node, _preorderIndex.Length);
        Checks.Index(descendant, _preorderIndex.Length);
        int offset = _preorderIndex[descendant] - _preorderIndex[node];
        return offset >= 0 && offset < _subtreeSize[node];
    }

    /// <summary>
    /// Returns the number of nodes in the subtree of <paramref name="node"/>, <paramref name="node"/>
    /// included.
    /// </summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The subtree size, at least 1.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int SubtreeSizeOf(int node)
    {
        Checks.Index(node, _subtreeSize.Length);
        return _subtreeSize[node];
    }

    /// <summary>
    /// Returns the nodes of the subtree of <paramref name="node"/>, in preorder, starting with
    /// <paramref name="node"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The result is a read-only span over this index's own array, not a copy; to keep it beyond a
    /// method, copy it with <c>ToArray()</c>.
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The nodes of the subtree.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public ReadOnlySpan<int> SubtreeOf(int node)
    {
        Checks.Index(node, _preorderIndex.Length);
        return Preorder.AsSpan(_preorderIndex[node], _subtreeSize[node]);
    }

    /// <summary>
    /// Gets all nodes in preorder: the roots in ascending order, and the children of each node in
    /// ascending order.
    /// </summary>
    /// <remarks>
    /// <para>The result is an unmodifiable view of this index's own array, not a copy.</para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    public ImmutableArray<int> Preorder { get; }

    /// <summary>
    /// Returns the position of <paramref name="node"/> in <see cref="Preorder"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data in an array indexed by preorder position has every subtree in a contiguous range. Prefix
    /// sums over such an array, for example, give the sum over any subtree in O(1).
    /// </para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="node">Node ID in <c>0..NodeCount-1</c>.</param>
    /// <returns>The position in <c>0..NodeCount-1</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int PreorderIndexOf(int node)
    {
        Checks.Index(node, _preorderIndex.Length);
        return _preorderIndex[node];
    }
}
