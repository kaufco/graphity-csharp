using Graphity;

namespace Examples.Guide;

/// <summary>The type of each edge in a depth-first search of the packages.</summary>
internal static class ClassifyingEdges
{
    /// <summary>What the example computes and reads.</summary>
    internal sealed record Classified(EdgeTypes Types, EdgeType StorageToModel,
        EdgeType AppToService, Edges CrossEdges);

    /// <summary>
    /// Returns the types of the edges of the packages, two of them read, and the cross
    /// edges.
    /// </summary>
    internal static Classified Classify()
    {
        Graph dependencies = SmallProgram.Dependencies();
        string[] names = SmallProgram.PackageNames();
        int app = SmallProgram.NodeOf(names, "app");
        int service = SmallProgram.NodeOf(names, "service");
        int model = SmallProgram.NodeOf(names, "model");
        int storage = SmallProgram.NodeOf(names, "storage");
        // @start region="edgeTypes"
        EdgeTypes types = EdgeTypes.Of(dependencies);
        // Back - closes a cycle
        EdgeType storageToModel =
            types.TypeOf(dependencies.EdgeIndexOf(storage, model));
        // Forward - also through ui
        EdgeType appToService =
            types.TypeOf(dependencies.EdgeIndexOf(app, service));

        // All edges of one type, as an edge list.
        Edges crossEdges = Edges.Of(types, EdgeType.Cross);
        // crossEdges: ui -> util, storage -> util, json -> util
        // @end
        return new Classified(types, storageToModel, appToService, crossEdges);
    }
}
