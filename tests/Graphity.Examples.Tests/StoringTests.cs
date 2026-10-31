namespace Examples.Tests;

public sealed class StoringTests
{
    [Fact]
    public void LoadGivesTheGraphWithItsEdgeIndicesAndTheNames()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        DirectoryInfo dir = Directory.CreateTempSubdirectory();
        try
        {
            string file = Path.Combine(dir.FullName, "calls.bin");

            Storing.Store(calls, names, file);
            Named loaded = Storing.Load(file);

            Assert.Equal(calls, loaded.Graph);
            Assert.Equal(Csr.OutAdjacency(calls).ToArray(), Csr.OutAdjacency(loaded.Graph).ToArray());
            Assert.Equal(names, loaded.Names);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
