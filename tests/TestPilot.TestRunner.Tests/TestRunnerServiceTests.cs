using Microsoft.Extensions.Logging.Abstractions;
using TestPilot.TestRunner;

namespace TestPilot.TestRunner.Tests;

// Integración: crea un proyecto xUnit temporal real en disco y ejecuta `dotnet test` sobre él.
[Trait("Category", "Integration")]
public class TestRunnerServiceTests : IDisposable
{
    private readonly string _tempProjectDir;

    public TestRunnerServiceTests()
    {
        _tempProjectDir = Path.Combine(Path.GetTempPath(), $"testpilot-runner-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempProjectDir);

        File.WriteAllText(Path.Combine(_tempProjectDir, "SampleProject.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <IsPackable>false</IsPackable>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
                <PackageReference Include="xunit" Version="2.9.3" />
                <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
              </ItemGroup>

              <ItemGroup>
                <Using Include="Xunit" />
              </ItemGroup>

            </Project>
            """);

        File.WriteAllText(Path.Combine(_tempProjectDir, "SampleTests.cs"), """
            namespace SampleProject;

            public class SampleTests
            {
                [Fact]
                public void Test_That_Passes()
                {
                    Assert.Equal(2, 1 + 1);
                }

                [Fact]
                public void Test_That_Fails()
                {
                    Assert.Equal(3, 1 + 1);
                }
            }
            """);
    }

    [Fact]
    public async Task RunAsync_WithOnePassingAndOneFailingTest_ReportsBoth()
    {
        var runner = new TestRunnerService(NullLogger<TestRunnerService>.Instance);

        var result = await runner.RunAsync(_tempProjectDir);

        Assert.False(result.Success);
        Assert.Equal(1, result.Passed);
        Assert.Equal(1, result.Failed);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempProjectDir, recursive: true); }
        catch { }
    }
}
