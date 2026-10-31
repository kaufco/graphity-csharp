using Graphity;

namespace Examples.Guide;

/// <summary>Two graphs combined into one, and a graph extended by an edge.</summary>
internal static class CombiningGraphs
{
    /// <summary>The combined graph and the offsets of the two graphs in it.</summary>
    internal sealed record Combined(Graph Graph, int ProgramOffset, int LibraryOffset);

    /// <summary>
    /// Returns the packages of the program and of a library, combined into one graph.
    /// </summary>
    internal static Combined ProgramWithLibrary()
    {
        Graph dependencies = SmallProgram.Dependencies();
        // @start region="combine"
        // library is a JSON library of three packages - api uses parser and writer - that
        // the program's package json (6) uses.
        var libraryBuilder = new GraphBuilder();
        libraryBuilder.AddNodes(3);
        // api -> parser, api -> writer
        libraryBuilder.AddOutEdges(0, 1, 2);
        Graph library = libraryBuilder.Build();

        // Put both into one graph; the nodes of each graph keep their order, after an
        // offset.
        var builder = new GraphBuilder();
        // 0 - the program keeps 0 to 7
        int programOffset = builder.AddGraph(dependencies);
        // 8 - the library becomes 8 to 10
        int libraryOffset = builder.AddGraph(library);
        builder.AddEdge(programOffset + 6, libraryOffset);   // json -> api
        Graph combined = builder.Build();
        // combined: 11 nodes, 17 edges
        // @end
        return new Combined(combined, programOffset, libraryOffset);
    }

    /// <summary>
    /// Returns the dependencies of a next version of the program, in which ui also uses
    /// storage.
    /// </summary>
    internal static Graph NextVersion()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        int ui = SmallProgram.NodeOf(names, "ui");
        int storage = SmallProgram.NodeOf(names, "storage");
        // @start region="extend"
        // A graph never changes: to add an edge, start a builder with it and build a new
        // graph.
        var builder = new GraphBuilder();
        // into an empty builder: the node IDs stay the same
        builder.AddGraph(dependencies);
        builder.AddEdge(ui, storage);
        Graph nextVersion = builder.Build();
        // nextVersion: 8 nodes, 15 edges
        // @end
        return nextVersion;
    }
}
