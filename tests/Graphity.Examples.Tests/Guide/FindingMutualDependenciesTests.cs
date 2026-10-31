using Examples.Guide;

namespace Examples.Tests.Guide;

public sealed class FindingMutualDependenciesTests
{
    [Fact]
    public void TheStatementsAndTheExpressionsAreTheRecursiveGroups()
    {
        FindingMutualDependencies.Groups groups = FindingMutualDependencies.RecursiveGroups();
        Assert.Equal(6, groups.SccCount);
        Assert.Equal([["expression", "term", "factor"], ["statement", "block", "ifStatement"]], groups.Recursive);
    }

    [Fact]
    public void FactorAndExpressionUseEachOtherButAssignmentOnlyUsesExpression()
    {
        FindingMutualDependencies.SameGroupResult sameGroup = FindingMutualDependencies.SameGroup();
        Assert.Equal(2, sameGroup.Expressions);
        Assert.True(sameGroup.FactorInIt);
        Assert.False(sameGroup.AssignmentInIt);
    }
}
