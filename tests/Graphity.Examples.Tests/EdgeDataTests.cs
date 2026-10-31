namespace Examples.Tests;

public sealed class EdgeDataTests
{
    [Fact]
    public void EdgeIndexOfCountsTwoCallSitesForRunToShowAndParseToParse()
    {
        Graph calls = SmallProgram.Calls();
        string[] names = SmallProgram.MethodNames();
        int[] siteCount = EdgeData.CallSitesPerEdge();
        Assert.Equal(calls.EdgeCount, siteCount.Length);
        calls.ForEachEdge((caller, callee, edge) =>
        {
            string call = $"{names[caller]} -> {names[callee]}";
            int expected = call is "run -> show" or "parse -> parse" ? 2 : 1;
            Assert.True(expected == siteCount[edge], call);
        });
    }

    [Fact]
    public void OutEdgesWriteTheCalleesOfRunWithTheirCallSites() =>
        Assert.Equal(["load: 1", "save: 1", "show: 2"], Names.LinesWrittenBy(EdgeData.CalleesOfRun));
}
