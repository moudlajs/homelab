using Xunit;

namespace HomeLab.Cli.Tests.Services;

/// <summary>
/// This test is designed to fail to test branch protection.
/// </summary>
public class FailingTest
{
    [Fact]
    public void ThisTestShouldFail()
    {
        // Intentionally fail
        Assert.True(false, "This test is designed to fail for testing branch protection");
    }
}
