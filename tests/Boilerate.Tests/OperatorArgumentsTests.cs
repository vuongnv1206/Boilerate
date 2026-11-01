using Boilerate.Host.Configurations;
using Xunit;

namespace Boilerate.Tests;

public sealed class OperatorArgumentsTests
{
    [Fact]
    public void ScopedCommandsDoNotLeakOperatorFlagsOrValuesIntoHostConfiguration()
    {
        Assert.Equal(["--environment", "Development", "--urls", "http://localhost:5099"],
            OperatorArguments.ConfigurationArguments(["--environment", "Development", "--register-capabilities",
                "--function", "FinancialSummary", "--grant-admin-capabilities", "--urls", "http://localhost:5099"]));
        Assert.Equal(["--urls", "http://localhost:5099"],
            OperatorArguments.ConfigurationArguments(["--urls", "http://localhost:5099"]));
    }

    [Fact]
    public void MalformedOrUnscopedFunctionNeverFallsThroughToAnHttpHost()
    {
        foreach (var args in new[]
        {
            new[] { "--function", "FinancialSummary" },
            new[] { "--register-capabilities", "--function" },
            new[] { "--register-capabilities", "--function", "--urls", "http://localhost:5099" },
            new[] { "--register-capabilities", "--function", "Order", "--function", "FinancialSummary" },
            new[] { "--bootstrap-admin", "--function", "FinancialSummary" },
            new[] { "--preflight", "--function", "FinancialSummary" },
            new[] { "--seed-dev", "--function", "FinancialSummary" },
            new[] { "--seed-dev", "--register-capabilities" }
        })
            Assert.Throws<InvalidOperationException>(() => OperatorArguments.ConfigurationArguments(args));
    }
}
