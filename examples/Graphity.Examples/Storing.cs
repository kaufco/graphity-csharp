using Graphity;

namespace Examples;

/// <summary>A graph with the names of its nodes.</summary>
internal sealed record Named(Graph Graph, string[] Names);

/// <summary>
/// Storing a graph in a file together with the data on its nodes, and loading both again.
/// </summary>
internal static class Storing
{
    /// <summary>
    /// Writes <paramref name="calls"/> to <paramref name="file"/>, followed by the names
    /// of the methods.
    /// </summary>
    internal static void Store(Graph calls, string[] names, string file)
    {
        // @start region="writeTo"
        using (var output = new BinaryWriter(File.Create(file)))
        {
            calls.WriteTo(output.BaseStream);
            foreach (string name in names)
            {
                output.Write(name);
            }
        }
        // @end
    }

    /// <summary>Reads what <see cref="Store"/> wrote.</summary>
    internal static Named Load(string file)
    {
        // @start region="readFrom"
        Graph calls;
        string[] names;
        using (var input = new BinaryReader(File.OpenRead(file)))
        {
            calls = Graph.ReadFrom(input.BaseStream);
            // equal to the graph written, edge indices included
            names = new string[calls.NodeCount];
            for (int method = 0; method < names.Length; method++)
            {
                names[method] = input.ReadString();
            }
        }
        // @end
        return new Named(calls, names);
    }
}
