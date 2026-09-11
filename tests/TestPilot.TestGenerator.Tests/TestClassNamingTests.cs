using TestPilot.TestGenerator;

namespace TestPilot.TestGenerator.Tests;

public class TestClassNamingTests
{
    [Theory]
    [InlineData("MyApp.Services.OrderService", "OrderServiceTests")]
    [InlineData("Calculator", "CalculatorTests")]
    [InlineData("A.B.C.D.Thing", "ThingTests")]
    public void TestClassFilter_DevuelveElNombreSimpleConSufijoTests(string fullName, string expected)
    {
        Assert.Equal(expected, TestClassNaming.TestClassFilter(fullName));
    }
}
