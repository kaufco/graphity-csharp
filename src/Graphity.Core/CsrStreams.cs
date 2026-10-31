using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Graphity;

// The binary format of Graph.WriteTo and Graph.ReadFrom, whose documentation is its specification.
// It handles the bare arrays, not Graph, so that Graph and it do not depend on each other.
internal static class CsrStreams
{
    private const int HeaderBytes = 8;

    // Ints per block: the 64 KiB blocks that Graph.WriteTo and Graph.ReadFrom document.
    private const int ChunkInts = 1 << 14;

    internal static void Write(int[] outAdjacencyStart, int[] outAdjacency, Stream output)
    {
        Span<byte> header = stackalloc byte[HeaderBytes];
        BinaryPrimitives.WriteInt32LittleEndian(header, outAdjacencyStart.Length - 1);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], outAdjacency.Length);
        output.Write(header);
        WriteInts(outAdjacencyStart, output);
        WriteInts(outAdjacency, output);
    }

    // The arrays as read, not yet checked: Graph.ReadFrom checks them as it builds the graph.
    internal static (int[] OutAdjacencyStart, int[] OutAdjacency) Read(Stream input)
    {
        Span<byte> header = stackalloc byte[HeaderBytes];
        ReadFully(input, header);
        int nodeCount = BinaryPrimitives.ReadInt32LittleEndian(header);
        int edgeCount = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
        if (nodeCount < 0 || nodeCount > ArrayLimits.MaxNodeCount
            || edgeCount < 0 || edgeCount > ArrayLimits.MaxArrayLength)
        {
            throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture,
                $"invalid header: nodeCount {nodeCount}, edgeCount {edgeCount}"));
        }
        int[] start = ReadInts(input, nodeCount + 1);
        int[] adjacency = ReadInts(input, edgeCount);
        return (start, adjacency);
    }

    // Writes in blocks of ChunkInts, straight from the array on a little-endian machine: the byte
    // length of a whole array of 2^29 or more ints does not fit an int.
    private static void WriteInts(int[] values, Stream output)
    {
        byte[]? buffer = BitConverter.IsLittleEndian ? null : new byte[sizeof(int) * ChunkInts];
        for (int from = 0; from < values.Length; from += ChunkInts)
        {
            ReadOnlySpan<int> chunk = values.AsSpan(from, Math.Min(ChunkInts, values.Length - from));
            if (buffer is null)
            {
                output.Write(MemoryMarshal.AsBytes(chunk));
                continue;
            }
            for (int i = 0; i < chunk.Length; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(sizeof(int) * i), chunk[i]);
            }
            output.Write(buffer, 0, sizeof(int) * chunk.Length);
        }
    }

    // Grows the result as data arrives instead of allocating count ints up front: count comes from
    // the header, and a header may announce more than the stream holds. A block that needs a larger
    // array is read into a buffer first, so that the array grows only once the block has arrived.
    private static int[] ReadInts(Stream input, int count)
    {
        var values = new int[Math.Min(count, ChunkInts)];
        int[]? buffer = null;
        for (int from = 0; from < count; from += ChunkInts)
        {
            int chunk = Math.Min(ChunkInts, count - from);
            Span<int> target;
            if (from + chunk <= values.Length)
            {
                target = values.AsSpan(from, chunk);
                ReadFully(input, MemoryMarshal.AsBytes(target));
            }
            else
            {
                buffer ??= new int[ChunkInts];
                ReadFully(input, MemoryMarshal.AsBytes(buffer.AsSpan(0, chunk)));
                Array.Resize(ref values, (int)Math.Min(count, 2L * values.Length));
                target = values.AsSpan(from, chunk);
                buffer.AsSpan(0, chunk).CopyTo(target);
            }
            if (!BitConverter.IsLittleEndian)
            {
                BinaryPrimitives.ReverseEndianness(target, target);
            }
        }
        return values;
    }

    private static void ReadFully(Stream input, Span<byte> bytes)
    {
        int read = input.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
        if (read < bytes.Length)
        {
            throw new EndOfStreamException(string.Create(CultureInfo.InvariantCulture,
                $"the stream ended inside a graph: {read} of {bytes.Length} bytes of the last block"));
        }
    }
}
