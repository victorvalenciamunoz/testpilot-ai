using TestPilot.TestRunner;

namespace TestPilot.TestRunner.Tests;

public class BuildErrorParserTests
{
    [Fact]
    public void ParseCompilerErrors_WithSingleError_ReturnsOneMessage()
    {
        var output = "CalculatorTests.cs(12,9): error CS0103: The name 'foo' does not exist in the current context";

        var errors = BuildErrorParser.ParseCompilerErrors(output);

        var error = Assert.Single(errors);
        Assert.Contains("CS0103", error);
        Assert.Contains("foo", error);
    }

    [Fact]
    public void ParseCompilerErrors_IgnoresWarnings()
    {
        var output = "CalculatorTests.cs(5,1): warning CS0168: The variable 'x' is declared but never used";

        var errors = BuildErrorParser.ParseCompilerErrors(output);

        Assert.Empty(errors);
    }

    [Fact]
    public void ParseCompilerErrors_WithNoErrors_ReturnsEmpty()
    {
        var output = "Build succeeded.\n0 Warning(s)\n0 Error(s)";

        var errors = BuildErrorParser.ParseCompilerErrors(output);

        Assert.Empty(errors);
    }

    [Fact]
    public void ParseCompilerErrors_WithMultipleErrors_ReturnsAll()
    {
        var output =
            "CalculatorTests.cs(12,9): error CS0103: The name 'foo' does not exist in the current context\n" +
            "CalculatorTests.cs(20,5): error CS1002: ; expected";

        var errors = BuildErrorParser.ParseCompilerErrors(output);

        Assert.Equal(2, errors.Count);
    }
}
