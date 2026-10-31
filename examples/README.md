# Examples

`Graphity.Examples/` holds the examples of the documentation of Graphity for .NET: of the XML
documentation, the package overviews, the API documentation site, the README and, in `Guide/`, of
the [guide](https://kaufco.github.io/graphity-doc/?lang=csharp). The build compiles them against the current
API, and the tests in `tests/Graphity.Examples.Tests/` run each example and check what its comments
claim, so an example in the documentation cannot go stale unnoticed. The project is not packed.

`api-csharp.json` and `java-refs.txt` are the API mapping of the guide.

## How the documentation shows an example

Each topic has a file. An example is a method that returns or writes its result; the lines the
documentation shows are a region of it:

```csharp
// @start region="transposed"
int model = 4;
BiAdjacentGraph usedBy = BiAdjacentGraph.Of(dependencies).Transposed();
SortedIntView affected = Graphs.ReachableFrom(usedBy, model);
// affected: [0, 2, 3, 4, 5] - app, ui, service, model and storage
// @end
```

The marker `<!-- example: Transposing.cs#transposed -->` before a code block names the region the
block shows, and the tests check that the block equals the region. A change to an example goes into
its file first; the tests then name every block that no longer matches.

A new example goes into the file of its topic, with a region name that says what it shows, and a
test that checks every claim of its comments. Data that several examples need belongs in one of the
example domains: `SmallProgram`, `SmallGrammar` or `SmallFamily`.

The guide links the full example below each block. A region that the guide shows therefore has no
comment that only makes up for the code the excerpt leaves out.

## Line length

No line of the example sources is longer than 90 characters, counted with the indentation. That is
the maximum: longer lines wrap in the guide at its standard width, which spoils the formatting of
the example. The limit holds for every file of `Graphity.Examples/`, whether the guide shows it or
not.

A line over the limit is brought under it according to what makes it too long:

- **A comment line** is wrapped, and the rest is joined with the next comment line, so that the
  comment reads as one paragraph. Every line starts with the comment marker, `//` or `///`. A
  `<summary>` too long for one line gets lines of its own between its tags.
- **A code line that is too long only because of a trailing comment** keeps the code, and the
  comment moves to its own line directly above it, with the same indentation. A result comment such
  as `// 3 - nextLine` moves the same way. Where it would land directly under another comment, a
  blank line keeps the two apart.
- **A code line that is too long by itself** is wrapped the way the C# code here is: after the `=`,
  the opening parenthesis or a comma, or before a `.`, `?`, `:` or `+`. `dotnet format` must leave the
  result as it is.
