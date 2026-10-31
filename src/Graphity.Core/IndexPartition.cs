namespace Graphity;

/// <summary>
/// A partition of indices into groups: the common base of <see cref="NodePartition"/> and
/// <see cref="EdgePartition"/>.
/// </summary>
/// <remarks>
/// <para>
/// The indices are node IDs or edge indices. Each index is in at most one group, and the groups are
/// numbered by group ID <c>0..GroupCount-1</c>. <c>Get(i)</c> returns the group of index <c>i</c>,
/// or -1 if it is in no group, and <see cref="IndicesOf"/> the indices of a group in ascending
/// order. <c>Reduce</c> combines data on the indices into one value per group. The data is an array
/// indexed by node ID or edge index.
/// </para>
/// <para>
/// A partition is a mapping from indices to groups that maps several indices to the same group,
/// so unlike a <see cref="NodeMapping"/> or an <see cref="EdgeMapping"/>, it cannot be inverted.
/// </para>
/// <para>
/// Instances are immutable and safe to share between threads. No other assembly can derive from
/// this class.
/// </para>
/// <para>
/// <b>Memory:</b> 4 x (IndexCount + MappedCount + GroupCount) bytes, where MappedCount is the
/// number of indices that belong to a group.
/// </para>
/// </remarks>
public abstract class IndexPartition : IndexMapping
{
    // The indices of each group in the layout of the out-adjacency of a Graph: the indices of group
    // g are Members[MemberStart[g]..MemberStart[g + 1] - 1], sorted ascending, with the sentinel
    // MemberStart[GroupCount] == Members.Length.
    internal readonly int[] Members;
    internal readonly int[] MemberStart;

    // Takes the arrays as they are, neither copied nor checked: the caller guarantees the layout,
    // and that mapping and members describe the same groups.
    private protected IndexPartition(int[] mapping, int[] members, int[] memberStart)
        : base(mapping, members.Length)
    {
        Members = members;
        MemberStart = memberStart;
    }

    /// <summary>Gets the number of groups.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    public int GroupCount => MemberStart.Length - 1;

    /// <summary>Returns the indices in group <paramref name="groupId"/>, in ascending order.</summary>
    /// <remarks>
    /// <para>The result is a view of this partition's own array, not a copy.</para>
    /// <para><b>Time:</b> O(1).</para>
    /// </remarks>
    /// <param name="groupId">Group ID in <c>0..GroupCount-1</c>.</param>
    /// <returns>The indices in the group.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="groupId"/> is out of range.</exception>
    public SortedIntView IndicesOf(int groupId)
    {
        Checks.Index(groupId, MemberStart.Length - 1);
        return new SortedIntView(Members, MemberStart[groupId], MemberStart[groupId + 1]);
    }

    /// <summary>Returns the number of indices in group <paramref name="groupId"/>.</summary>
    /// <remarks><b>Time:</b> O(1).</remarks>
    /// <param name="groupId">Group ID in <c>0..GroupCount-1</c>.</param>
    /// <returns>The size of the group.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="groupId"/> is out of range.</exception>
    public int SizeOf(int groupId)
    {
        Checks.Index(groupId, MemberStart.Length - 1);
        return MemberStart[groupId + 1] - MemberStart[groupId];
    }

    /// <summary>Returns one value per group, combined from the values of its indices.</summary>
    /// <remarks>
    /// <para>
    /// For each group, starts from <paramref name="identity"/> and combines the result so far with
    /// the value of each index in the group, in ascending order of the indices. A group without
    /// indices gets <paramref name="identity"/>, and the values of indices in no group are left out.
    /// One generic method serves <c>int</c>, <c>long</c>, <c>double</c> and any other element type.
    /// </para>
    /// <para>
    /// <b>Time:</b> O(IndexCount + GroupCount).<br/>
    /// <b>Memory:</b> one element per group for the result, 4 x GroupCount bytes for <c>int</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// Here, <c>byPackageCall</c> groups the calls between the methods of a program by the call
    /// between packages that each belongs to (see <see cref="EdgePartition"/>), and the call sites in
    /// <c>siteCount</c> are summed per call between packages:
    /// <!-- example: Partitions.cs#reduce -->
    /// <code language="csharp">
    /// int[] packageSiteCount = byPackageCall.Reduce(siteCount, 0, (a, b) => a + b);
    /// // 2 for app -> ui, app -> service, service -> model and service -> storage,
    /// // 1 for app -> cli and ui -> model
    /// </code>
    /// </example>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="values">The value of each index; length <see cref="IndexMapping.IndexCount"/>.</param>
    /// <param name="identity">The start value of each group.</param>
    /// <param name="combine">
    /// Called with the result so far and the value of the next index, returns the two combined.
    /// </param>
    /// <returns>One value per group, by group ID.</returns>
    /// <exception cref="ArgumentException"><c>values.Length != IndexCount</c>.</exception>
    public T[] Reduce<T>(ReadOnlySpan<T> values, T identity, Func<T, T, T> combine)
    {
        ArgumentNullException.ThrowIfNull(combine);
        Checks.Length(values.Length, IndexCount, "values");
        int[] members = Members;
        int[] memberStart = MemberStart;
        int groups = memberStart.Length - 1;
        var result = new T[groups];
        for (int g = 0; g < groups; g++)
        {
            T acc = identity;
            for (int i = memberStart[g], end = memberStart[g + 1]; i < end; i++)
            {
                acc = combine(acc, values[members[i]]);
            }
            result[g] = acc;
        }
        return result;
    }

    /// <summary>
    /// Writes one value per group into <paramref name="groupValues"/>, combined from the values of
    /// the indices of that group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For each group, starts from the value of its first index and combines the result so far with
    /// the value of each further index, in ascending order of the indices. A group without indices
    /// keeps its entry in <paramref name="groupValues"/>, and the values of indices in no group are
    /// left out.
    /// </para>
    /// <para><b>Time:</b> O(IndexCount + GroupCount).</para>
    /// </remarks>
    /// <typeparam name="T">The element type, nullable or not.</typeparam>
    /// <param name="values">The value of each index; length <see cref="IndexMapping.IndexCount"/>.</param>
    /// <param name="groupValues">
    /// Receives one value per group, by group ID; length <see cref="GroupCount"/>, and not
    /// overlapping <paramref name="values"/>.
    /// </param>
    /// <param name="combine">
    /// Called with the result so far and the value of the next index, returns the two combined.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <c>values.Length != IndexCount</c>, <c>groupValues.Length != GroupCount</c>, or the two
    /// overlap.
    /// </exception>
    public void Reduce<T>(ReadOnlySpan<T> values, Span<T> groupValues, Func<T, T, T> combine)
    {
        ArgumentNullException.ThrowIfNull(combine);
        CheckDistinct(values, groupValues, "groupValues");
        Checks.Length(values.Length, IndexCount, "values");
        int[] members = Members;
        int[] memberStart = MemberStart;
        int groups = memberStart.Length - 1;
        Checks.Length(groupValues.Length, groups, "groupValues");
        for (int g = 0; g < groups; g++)
        {
            int i = memberStart[g];
            int end = memberStart[g + 1];
            if (i == end)
            {
                continue;
            }
            T acc = values[members[i]];
            for (i++; i < end; i++)
            {
                acc = combine(acc, values[members[i]]);
            }
            groupValues[g] = acc;
        }
    }
}
