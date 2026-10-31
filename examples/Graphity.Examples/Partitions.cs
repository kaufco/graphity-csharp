using Graphity;

namespace Examples;

/// <summary>
/// Grouping the nodes or the edges of a graph, and the graph between the groups.
/// </summary>
internal static class Partitions
{
    /// <summary>Returns the calls grouped by the package of each method.</summary>
    internal static NodePartition ByPackage() =>
        NodePartition.Of(
            SmallProgram.Calls(), SmallProgram.PackageOf(),
            SmallProgram.Dependencies().NodeCount);

    /// <summary>
    /// Returns the calls between packages, condensed from the calls between methods.
    /// </summary>
    internal static Graph PackageCalls()
    {
        Graph calls = SmallProgram.Calls();
        Graph dependencies = SmallProgram.Dependencies();
        int[] packageOf = SmallProgram.PackageOf();
        // @start region="of"
        NodePartition byPackage =
            NodePartition.Of(calls, packageOf, dependencies.NodeCount);
        Graph packageCalls = byPackage.Condense();
        // packageCalls: app -> [cli, ui, service], ui -> [model],
        //               service -> [model, storage]
        // @end
        return packageCalls;
    }

    /// <summary>
    /// Returns the calls from methods of <c>service</c> to methods of <c>model</c>.
    /// </summary>
    internal static SortedIntView CallsFromServiceToModel()
    {
        NodePartition byPackage = ByPackage();
        Graph packageCalls = byPackage.Condense();
        string[] packageNames = SmallProgram.PackageNames();
        int service = SmallProgram.NodeOf(packageNames, "service");
        int model = SmallProgram.NodeOf(packageNames, "model");
        // @start region="edgesOf"
        EdgePartition byPackageCall = Graphs.EdgePartition(packageCalls, byPackage);
        int serviceToModel = packageCalls.EdgeIndexOf(service, model);
        SortedIntView behind = byPackageCall.EdgesOf(serviceToModel);
        // behind: [5, 7] - the calls load -> parse and save -> format
        // @end
        return behind;
    }

    /// <summary>
    /// Returns the number of call sites behind each call between packages.
    /// </summary>
    internal static int[] CallSitesPerPackageCall()
    {
        NodePartition byPackage = ByPackage();
        EdgePartition byPackageCall =
            Graphs.EdgePartition(byPackage.Condense(), byPackage);
        int[] siteCount = EdgeData.CallSitesPerEdge();
        // @start region="reduce"
        int[] packageSiteCount = byPackageCall.Reduce(siteCount, 0, (a, b) => a + b);
        // 2 for app -> ui, app -> service, service -> model and service -> storage,
        // 1 for app -> cli and ui -> model
        // @end
        return packageSiteCount;
    }
}
