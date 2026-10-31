using Graphity;

namespace Examples;

/// <summary>The example domain: a small program, seen as graphs.</summary>
/// <remarks>
/// <para>
/// The program is a notebook on the command line. Its packages depend on each other, its
/// methods call each other, and each method has a control flow between its basic blocks.
/// This class gives six of these graphs, each with the names of its nodes:
/// </para>
/// <list type="bullet">
///   <item><see cref="Dependencies"/> with <see cref="PackageNames"/> - the packages and
///   the packages they use; <c>model</c> and <c>storage</c> use each other, the one
///   cycle.</item>
///   <item><see cref="Calls"/> with <see cref="MethodNames"/> - the methods and the
///   methods they call, built from the <see cref="CallSites"/>; <c>parse</c> and
///   <c>format</c> call themselves. <see cref="PackageOf"/> gives the package of each
///   method.</item>
///   <item><see cref="Cfg"/> with <see cref="BlockNames"/> - the control flow of
///   <c>load</c>, a loop with a branch in it, from the entry block to the exit
///   block.</item>
///   <item><see cref="CfgOfShow"/> with <see cref="BlockNamesOfShow"/> - the control flow
///   of <c>show</c>, a loop over the lines of a note inside a loop over the notes.</item>
///   <item><see cref="BuildSteps"/> with <see cref="StepNames"/> - the steps that build
///   and publish the program, an edge from each step to each step that may start only
///   after it.</item>
///   <item><see cref="CfgOfParseArgs"/> with <see cref="BlockNamesOfParseArgs"/> - the
///   control flow of <c>parseArgs</c>, a state machine whose loop is entered in either of
///   its two states.</item>
/// </list>
/// <para>
/// Node IDs follow the order of the names. The graphs never change: the comments of the
/// examples, and the documentation that shows them, depend on every node and edge.
/// </para>
/// </remarks>
public static class SmallProgram
{
    private static readonly string[] Packages =
        ["app", "cli", "ui", "service", "model", "storage", "json", "util"];

    // A package, then the packages it uses.
    private static readonly string[][] Uses =
    [
        ["app", "cli", "ui", "service"],
        ["cli", "util"],
        ["ui", "service", "model", "util"],
        ["service", "model", "storage"],
        ["model", "storage"],
        ["storage", "model", "json", "util"],
        ["json", "util"],
    ];

    private static readonly string[] Methods =
    [
        "main", "parseArgs", "run", "load", "save",
        "show", "parse", "format", "read", "write",
    ];

    // A method, then the methods it calls, once per call site: run shows the notes before
    // and after saving them, and parse calls itself for nested arrays and for nested
    // objects.
    private static readonly string[][] CallSitesOfMethods =
    [
        ["main", "parseArgs", "run"],
        ["run", "load", "show", "save", "show"],
        ["load", "read", "parse"],
        ["save", "format", "write"],
        ["show", "format"],
        ["parse", "parse", "parse"],
        ["format", "format"],
    ];

    // The methods of each package, the package first. Packages without methods are left
    // out.
    private static readonly string[][] MethodsOfPackage =
    [
        ["app", "main", "run"],
        ["cli", "parseArgs"],
        ["ui", "show"],
        ["service", "load", "save"],
        ["model", "parse", "format"],
        ["storage", "read", "write"],
    ];

    private static readonly string[] Blocks =
        ["entry", "header", "body", "then", "next", "exit"];

    // The control flow of load, a block, then the blocks it may continue with:
    //
    //   List<Note> Load(string file)
    //   {
    //       string[] lines = Read(file);                // entry
    //       var notes = new List<Note>();
    //       for (int i = 0; i < lines.Length; i++)      // header: i < lines.Length
    //       {
    //           string line = lines[i];                 // body
    //           if (!string.IsNullOrWhiteSpace(line))
    //           {
    //               notes.Add(Parse(line));             // then
    //           }
    //       }                                           // next: i++
    //       return notes;                               // exit
    //   }
    private static readonly string[][] Flow =
    [
        ["entry", "header"],
        ["header", "body", "exit"],
        ["body", "then", "next"],
        ["then", "next"],
        ["next", "header"],
    ];

    private static readonly string[] ShowBlocks =
        ["entry", "nextNote", "format", "nextLine", "print", "exit"];

    // The control flow of show, a block, then the blocks it may continue with:
    //
    //   void Show(List<Note> notes)
    //   {
    //       output.WriteLine($"{notes.Count} notes");          // entry
    //       foreach (Note note in notes)                       // nextNote: another note?
    //       {
    //           string[] lines = Format(note).Split('\n');     // format
    //           foreach (string line in lines)                 // nextLine: another line?
    //           {
    //               output.WriteLine("  " + line);             // print
    //           }
    //       }
    //       output.Flush();                                    // exit
    //   }
    private static readonly string[][] ShowFlow =
    [
        ["entry", "nextNote"],
        ["nextNote", "format", "exit"],
        ["format", "nextLine"],
        ["nextLine", "print", "nextNote"],
        ["print", "nextLine"],
    ];

    private static readonly string[] Steps =
    [
        "generate", "compile", "lint", "compileTests", "docs", "test", "package", "sign",
        "publish",
    ];

    // A step, then the steps that may start only after it.
    private static readonly string[][] Before =
    [
        ["generate", "compile"],
        ["compile", "compileTests", "docs", "package"],
        ["lint", "package"],
        ["compileTests", "test"],
        ["docs", "publish"],
        ["test", "package"],
        ["package", "sign"],
        ["sign", "publish"],
    ];

    private static readonly string[] ParseArgsBlocks =
        ["entry", "option", "value", "exit"];

    // The control flow of parseArgs, a block, then the blocks it may continue with.
    // parseArgs is generated from a state machine with two states that jump to each
    // other; it starts in the state "option" or, if the first argument is not an option,
    // in the state "value":
    //
    //   entry:   if (IsOption(args[0])) goto option; else goto value;
    //   option:  read an option;  if (done) goto exit; else goto value;
    //   value:   read a value;    if (done) goto exit; else goto option;
    //   exit:    return the settings;
    private static readonly string[][] ParseArgsFlow =
    [
        ["entry", "option", "value"],
        ["option", "value", "exit"],
        ["value", "option", "exit"],
    ];

    /// <summary>
    /// Returns the dependencies between the packages: an edge from each package to each
    /// package it uses.
    /// </summary>
    /// <returns>8 nodes, 14 edges.</returns>
    public static Graph Dependencies() => Build(Packages, Uses);

    /// <summary>
    /// Returns the names of the packages, by node ID of <see cref="Dependencies"/>.
    /// </summary>
    /// <returns>app, cli, ui, service, model, storage, json, util.</returns>
    public static string[] PackageNames() => [.. Packages];

    /// <summary>
    /// Returns the calls between the methods: an edge from each method to each method it
    /// calls, built with <see cref="GraphBuilder"/> from one edge per call site, which
    /// merges the duplicates.
    /// </summary>
    /// <returns>10 nodes, 12 edges.</returns>
    public static Graph Calls() => Build(Methods, CallSitesOfMethods);

    /// <summary>
    /// Returns the call sites in source order, one per place in the source where a method
    /// calls another: <c>run</c> calls <c>show</c> twice, and <c>parse</c> calls itself
    /// twice.
    /// </summary>
    /// <returns>14 call sites, as node IDs of <see cref="Calls"/>.</returns>
    public static IReadOnlyList<CallSite> CallSites()
    {
        var sites = new List<CallSite>();
        foreach (string[] row in CallSitesOfMethods)
        {
            int caller = NodeOf(Methods, row[0]);
            for (int i = 1; i < row.Length; i++)
            {
                sites.Add(new CallSite(caller, NodeOf(Methods, row[i])));
            }
        }
        return sites.AsReadOnly();
    }

    /// <summary>
    /// Returns the names of the methods, by node ID of <see cref="Calls"/>.
    /// </summary>
    /// <returns>
    /// main, parseArgs, run, load, save, show, parse, format, read, write.
    /// </returns>
    public static string[] MethodNames() => [.. Methods];

    /// <summary>
    /// Returns the package of each method, a node ID of <see cref="Dependencies"/>, by
    /// node ID of <see cref="Calls"/>. Every call between two packages is a dependency
    /// between them.
    /// </summary>
    /// <returns>
    /// app for main and run, cli for parseArgs, ui for show, service for load and save,
    /// model for parse and format, storage for read and write.
    /// </returns>
    public static int[] PackageOf()
    {
        int[] packageOf = new int[Methods.Length];
        foreach (string[] row in MethodsOfPackage)
        {
            int package = NodeOf(Packages, row[0]);
            for (int i = 1; i < row.Length; i++)
            {
                packageOf[NodeOf(Methods, row[i])] = package;
            }
        }
        return packageOf;
    }

    /// <summary>
    /// Returns the control flow of the method <c>load</c> between its basic blocks.
    /// </summary>
    /// <returns>6 nodes, 7 edges.</returns>
    public static Graph Cfg() => Build(Blocks, Flow);

    /// <summary>
    /// Returns the names of the basic blocks, by node ID of <see cref="Cfg"/>.
    /// </summary>
    /// <returns>entry, header, body, then, next, exit.</returns>
    public static string[] BlockNames() => [.. Blocks];

    /// <summary>
    /// Returns the control flow of the method <c>show</c> between its basic blocks.
    /// </summary>
    /// <returns>6 nodes, 7 edges.</returns>
    public static Graph CfgOfShow() => Build(ShowBlocks, ShowFlow);

    /// <summary>
    /// Returns the names of the basic blocks of <c>show</c>, by node ID of
    /// <see cref="CfgOfShow"/>.
    /// </summary>
    /// <returns>entry, nextNote, format, nextLine, print, exit.</returns>
    public static string[] BlockNamesOfShow() => [.. ShowBlocks];

    /// <summary>
    /// Returns the steps that build and publish the program: an edge <c>a -> b</c> if
    /// step <c>b</c> may start only after step <c>a</c> has finished.
    /// </summary>
    /// <returns>9 nodes, 10 edges.</returns>
    public static Graph BuildSteps() => Build(Steps, Before);

    /// <summary>
    /// Returns the names of the build steps, by node ID of <see cref="BuildSteps"/>.
    /// </summary>
    /// <returns>
    /// generate, compile, lint, compileTests, docs, test, package, sign, publish.
    /// </returns>
    public static string[] StepNames() => [.. Steps];

    /// <summary>
    /// Returns the control flow of the method <c>parseArgs</c> between its basic blocks.
    /// </summary>
    /// <returns>4 nodes, 6 edges.</returns>
    public static Graph CfgOfParseArgs() => Build(ParseArgsBlocks, ParseArgsFlow);

    /// <summary>
    /// Returns the names of the basic blocks of <c>parseArgs</c>, by node ID of
    /// <see cref="CfgOfParseArgs"/>.
    /// </summary>
    /// <returns>entry, option, value, exit.</returns>
    public static string[] BlockNamesOfParseArgs() => [.. ParseArgsBlocks];

    /// <summary>Returns the node ID of the node named <paramref name="name"/>.</summary>
    /// <param name="names">The names of the nodes, by node ID.</param>
    /// <param name="name">The name to look up.</param>
    /// <returns>The node ID.</returns>
    /// <exception cref="ArgumentException">No node has this name.</exception>
    public static int NodeOf(string[] names, string name)
    {
        ArgumentNullException.ThrowIfNull(names);
        int node = Array.IndexOf(names, name);
        return node >= 0
            ? node
            : throw new ArgumentException($"no node is named {name}", nameof(name));
    }

    private static Graph Build(string[] names, string[][] rows)
    {
        var builder = new GraphBuilder();
        builder.AddNodes(names.Length);
        foreach (string[] row in rows)
        {
            int source = NodeOf(names, row[0]);
            for (int i = 1; i < row.Length; i++)
            {
                builder.AddEdge(source, NodeOf(names, row[i]));
            }
        }
        return builder.Build();
    }
}

/// <summary>A place in the source where one method calls another.</summary>
/// <remarks>
/// Java nests it in <c>SmallProgram</c>; here it stands beside it, so that the examples
/// name it <c>CallSite</c>, as they do in Java.
/// </remarks>
/// <param name="Caller">
/// The calling method, a node ID of <see cref="SmallProgram.Calls"/>.
/// </param>
/// <param name="Callee">
/// The called method, a node ID of <see cref="SmallProgram.Calls"/>.
/// </param>
public readonly record struct CallSite(int Caller, int Callee);
