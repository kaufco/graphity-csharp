using System.Buffers.Binary;

namespace Graphity.Core.Tests;

/// <summary>
/// Java reports bad data as <c>IOException</c> and a short stream as <c>EOFException</c>; the C#
/// port throws <see cref="InvalidDataException"/> and <see cref="EndOfStreamException"/>.
/// </summary>
public sealed class GraphStreamTests
{
    private static byte[] BytesOf(Graph graph)
    {
        using var output = new MemoryStream();
        graph.WriteTo(output);
        return output.ToArray();
    }

    private static Graph ReadBack(byte[] bytes) => Graph.ReadFrom(new MemoryStream(bytes));

    private static void AssertSameTopology(Graph actual, Graph expected)
    {
        Assert.Equal(Csr.OutAdjacencyStart(expected).ToArray(), Csr.OutAdjacencyStart(actual).ToArray());
        Assert.Equal(Csr.OutAdjacency(expected).ToArray(), Csr.OutAdjacency(actual).ToArray());
    }

    /// <summary>The 8 header bytes of a graph with the given counts, and nothing after them.</summary>
    private static byte[] Header(int nodeCount, int edgeCount)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, nodeCount);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), edgeCount);
        return bytes;
    }

    [Fact]
    public void RoundTripsRandomGraphs()
    {
        for (long seed = 0; seed < 20; seed++)
        {
            Graph graph = RandomGraphs.Gnp(60, 0.07, seed);
            AssertSameTopology(ReadBack(BytesOf(graph)), graph);
        }
    }

    [Fact]
    public void RoundTripsTheEmptyGraph()
    {
        Graph read = ReadBack(BytesOf(TestGraphs.Empty()));
        Assert.Equal(0, read.NodeCount);
        Assert.Equal(0, read.EdgeCount);
    }

    [Fact]
    public void RoundTripsArraysLargerThanOneBuffer()
    {
        Graph graph = RandomGraphs.Gnm(40_000, 100_000, 7);
        AssertSameTopology(ReadBack(BytesOf(graph)), graph);
    }

    [Fact]
    public void RoundTripsArraysAtTheBufferBoundaries()
    {
        int chunk = 1 << 14;
        foreach (int count in new[] { chunk - 1, chunk, chunk + 1, 2 * chunk, (2 * chunk) + 1 })
        {
            Graph edges = TestGraphs.Path(count + 1);
            Assert.Equal(count, edges.EdgeCount);
            AssertSameTopology(ReadBack(BytesOf(edges)), edges);

            Graph offsets = TestGraphs.Isolated(count - 1);
            AssertSameTopology(ReadBack(BytesOf(offsets)), offsets);
        }
    }

    [Fact]
    public void WritesInBlocksOfAtMost64KiB()
    {
        // One write of a whole array fails from 2^29 ints on, where its byte length overflows an
        // int; Java writes through a 64 KiB buffer.
        using var output = new WriteRecordingStream();
        Graph graph = RandomGraphs.Gnm(40_000, 100_000, 7);
        graph.WriteTo(output);

        Assert.Equal(8 + (4L * (graph.NodeCount + 1 + graph.EdgeCount)), output.Length);
        Assert.True(output.LargestWrite <= 4 << 14, $"largest write {output.LargestWrite} bytes");
    }

    [Fact]
    public void WritesTheDocumentedLayout()
    {
        Graph graph = TestGraphs.Of(3, 0, 1, 0, 2, 2, 0);

        Assert.Equal(
            [
                3, 0, 0, 0, // nodeCount
                3, 0, 0, 0, // edgeCount
                0, 0, 0, 0, // outAdjacencyStart
                2, 0, 0, 0,
                2, 0, 0, 0,
                3, 0, 0, 0,
                1, 0, 0, 0, // outAdjacency
                2, 0, 0, 0,
                0, 0, 0, 0,
            ],
            BytesOf(graph));
    }

    [Fact]
    public void WritesLittleEndian()
    {
        byte[] bytes = BytesOf(TestGraphs.Isolated(0x0102));

        Assert.Equal([0x02, 0x01, 0, 0, 0, 0, 0, 0], bytes[..8]);
    }

    [Fact]
    public void BiAdjacentGraphIsWrittenAsItsOutAdjacency()
    {
        Graph graph = RandomGraphs.Gnp(30, 0.1, 3);
        Assert.Equal(BytesOf(graph), BytesOf(BiAdjacentGraph.Of(graph)));
    }

    [Fact]
    public void BiAdjacentGraphIsReadBackAsAGraph()
    {
        BiAdjacentGraph written = BiAdjacentGraph.Of(RandomGraphs.Gnp(30, 0.1, 3));

        Graph read = ReadBack(BytesOf(written));

        Assert.Equal(typeof(Graph), read.GetType());
        AssertSameTopology(read, written);
    }

    [Fact]
    public void ReadFromCalledOnBiAdjacentGraphReturnsAGraph()
    {
        BiAdjacentGraph written = BiAdjacentGraph.Of(RandomGraphs.Gnp(30, 0.1, 3));

        // The static method that BiAdjacentGraph inherits from Graph.
        Graph read = BiAdjacentGraph.ReadFrom(new MemoryStream(BytesOf(written)));

        Assert.Equal(typeof(Graph), read.GetType());
        AssertSameTopology(read, written);
    }

    [Fact]
    public void ReadsExactlyOneGraphAndLeavesTheRest()
    {
        using var output = new MemoryStream();
        Graph first = TestGraphs.Path(4);
        Graph second = TestGraphs.Cycle(3);
        first.WriteTo(output);
        second.WriteTo(output);
        output.WriteByte(42);
        using var input = new MemoryStream(output.ToArray());

        AssertSameTopology(Graph.ReadFrom(input), first);
        AssertSameTopology(Graph.ReadFrom(input), second);
        Assert.Equal(42, input.ReadByte());
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(0, -5)]
    [InlineData(0, int.MaxValue)]
    [InlineData(4, -1)]
    [InlineData(4, int.MinValue)]
    public void RejectsCountsOutsideTheirRange(int offset, int value)
    {
        byte[] bytes = BytesOf(TestGraphs.Path(3));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
        var ex = Assert.Throws<InvalidDataException>(() => ReadBack(bytes));
        Assert.Contains("invalid header", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsCountsBeyondTheLargestArrayFromTheHeaderAlone()
    {
        // A stream of the header only: a count the layout allows passes the header and then meets
        // the end of the stream; one beyond the largest array is rejected as an invalid header. The
        // largest .NET array, Array.MaxLength, is 2^31 - 57, so the limits are 48 below Java's:
        // Java's own limits are rejected here too.
        int nodeLimit = Array.MaxLength - 1;
        int edgeLimit = Array.MaxLength;
        Assert.Throws<EndOfStreamException>(() => ReadBack(Header(nodeLimit, 0)));
        Assert.Throws<EndOfStreamException>(() => ReadBack(Header(0, edgeLimit)));
        int[][] beyond =
        [
            [nodeLimit + 1, 0], [int.MaxValue - 9, 0], [int.MaxValue - 1, 0], [0, edgeLimit + 1],
            [0, int.MaxValue - 8],
        ];
        foreach (int[] counts in beyond)
        {
            var ex = Assert.Throws<InvalidDataException>(() => ReadBack(Header(counts[0], counts[1])));
            Assert.Contains("invalid header", ex.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RejectsATruncatedStream()
    {
        byte[] bytes = BytesOf(TestGraphs.Path(5));
        foreach (int length in new[] { 0, 3, 7, 8, 12, bytes.Length - 1 })
        {
            byte[] truncated = bytes[..length];
            Assert.Throws<EndOfStreamException>(() => ReadBack(truncated));
        }
    }

    [Fact]
    public void AHugeAnnouncedSizeFailsAtTheEndOfTheStreamNotWithAnAllocation()
    {
        // The largest node count and edge count the header allows.
        int[][] fieldAndCount = [[0, Array.MaxLength - 1], [4, Array.MaxLength]];
        foreach (int[] c in fieldAndCount)
        {
            byte[] bytes = BytesOf(TestGraphs.Path(3));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(c[0]), c[1]);

            long before = GC.GetAllocatedBytesForCurrentThread();
            Assert.Throws<EndOfStreamException>(() => ReadBack(bytes));
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(allocated < 16L << 20, $"allocated {allocated} bytes");
        }
    }

    [Fact]
    public void AnArrayGrowsOnlyOnceTheDataForItHasArrived()
    {
        // After 2, 4 and 8 chunks of 2^14 edges the array is full when the stream ends. Grown only
        // for data that arrived, the arrays sum to less than twice that data, plus the 64 KiB buffer.
        int chunk = 1 << 14;
        foreach (int chunks in new[] { 2, 4, 8 })
        {
            int arrived = 4 * chunks * chunk;
            using var input = new MemoryStream([.. Header(0, 1 << 20), .. new byte[4], .. new byte[arrived]]);

            long before = GC.GetAllocatedBytesForCurrentThread();
            Assert.Throws<EndOfStreamException>(() => Graph.ReadFrom(input));
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(allocated < (2L * arrived) + (4 * chunk) + (16 << 10),
                $"{chunks} chunks: allocated {allocated} bytes for {arrived} bytes of data");
        }
    }

    [Fact]
    public void RejectsArraysThatDoNotFormAGraph()
    {
        byte[] bytes = BytesOf(TestGraphs.Of(2, 0, 1));
        // The only target, at byte 8 + 4 x 3, points past the last node.
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8 + 12), 5);

        var ex = Assert.Throws<InvalidDataException>(() => ReadBack(bytes));
        Assert.Contains("valid graph", ex.Message, StringComparison.Ordinal);
        Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
    }

    [Fact]
    public void TheMessageOfInvalidDataIsJavasWithoutAParameterOfGraphOf()
    {
        // The caller of ReadFrom passed no outAdjacency or outAdjacencyStart, so the message names
        // neither and carries no actual value of its own, as Java's IOException does not.
        byte[] outNode = BytesOf(TestGraphs.Of(2, 0, 1));
        BinaryPrimitives.WriteInt32LittleEndian(outNode.AsSpan(8 + 12), 5);
        var ex = Assert.Throws<InvalidDataException>(() => ReadBack(outNode));
        Assert.Equal("the stream does not hold a valid graph: out-node 5 of node 0 at position 0 is not in 0..1",
            ex.Message);
        var inner = Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
        Assert.Null(inner.ParamName);
        Assert.Null(inner.ActualValue);

        byte[] offset = BytesOf(TestGraphs.Of(2, 0, 1));
        BinaryPrimitives.WriteInt32LittleEndian(offset.AsSpan(8), 3);
        ex = Assert.Throws<InvalidDataException>(() => ReadBack(offset));
        Assert.Equal("the stream does not hold a valid graph: outAdjacencyStart[0] must be 0, but was 3", ex.Message);
        Assert.Null(Assert.IsType<ArgumentException>(ex.InnerException).ParamName);
    }

    [Fact]
    public void GraphOfStillNamesItsParameters()
    {
        var outNode = Assert.Throws<ArgumentOutOfRangeException>(() => Graph.Of([0, 1, 1], [5]));
        Assert.Equal("outAdjacency", outNode.ParamName);
        Assert.Equal(5, outNode.ActualValue);
        var offset = Assert.Throws<ArgumentException>(() => Graph.Of([3, 1, 1], [1]));
        Assert.Equal("outAdjacencyStart", offset.ParamName);
    }

    [Fact]
    public void RejectsAnEdgeCountThatDisagreesWithTheOffsets()
    {
        // Path(3) has the offsets 0, 1, 2, 2; announce and send one edge instead of two.
        byte[] bytes = BytesOf(TestGraphs.Path(3));
        byte[] shortened = bytes[..^4];
        BinaryPrimitives.WriteInt32LittleEndian(shortened.AsSpan(4), 1);

        var ex = Assert.Throws<InvalidDataException>(() => ReadBack(shortened));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void RejectsUnsortedListsWithTheCauseFromGraphOf()
    {
        byte[] bytes = BytesOf(TestGraphs.Of(3, 0, 1, 0, 2));
        // Swap the two targets of node 0, at bytes 8 + 4 x 4 and 8 + 4 x 5.
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), 1);

        var ex = Assert.Throws<InvalidDataException>(() => ReadBack(bytes));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void DoesNotCloseTheStreams()
    {
        using var output = new CloseTrackingStream();
        TestGraphs.Path(3).WriteTo(output);
        using var input = new CloseTrackingStream(output.ToArray());
        Graph.ReadFrom(input);

        Assert.False(output.Closed);
        Assert.False(input.Closed);
    }

    private sealed class WriteRecordingStream : MemoryStream
    {
        public int LargestWrite { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            LargestWrite = Math.Max(LargestWrite, count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            LargestWrite = Math.Max(LargestWrite, buffer.Length);
            base.Write(buffer);
        }
    }

    private sealed class CloseTrackingStream : MemoryStream
    {
        public CloseTrackingStream()
        {
        }

        public CloseTrackingStream(byte[] bytes)
            : base(bytes)
        {
        }

        public bool Closed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Closed = true;
            base.Dispose(disposing);
        }
    }
}
