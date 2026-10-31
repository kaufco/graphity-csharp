namespace Examples.Tests;

public sealed class TransposingTests
{
    [Fact]
    public void TransposedReachesTheDependentsOfModel()
    {
        Assert.Equal("model", SmallProgram.PackageNames()[4]);
        Assert.Equal("[0, 2, 3, 4, 5]", Transposing.AffectedByModel().ToString());
        Assert.Equal(
            ["app", "ui", "service", "model", "storage"],
            Names.Of(Transposing.AffectedByModel(), SmallProgram.PackageNames()));
    }
}
