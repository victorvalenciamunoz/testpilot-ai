namespace TestPilot.TestGenerator;

internal static class MockingPolicy
{
    public static string Describe(string? detectedLibrary) => detectedLibrary is null
        ? "el proyecto de tests no referencia ninguna librería de mocking, así que NO uses Moq, NSubstitute ni similares; escribe fakes a mano implementando las interfaces."
        : $"el proyecto de tests referencia {detectedLibrary}. Escribe fakes a mano para las interfaces propias del proyecto, que es lo preferible, y recurre a {detectedLibrary} SOLO cuando la dependencia sea una clase concreta de un framework externo que no puedas construir ni heredar de forma razonable.";
}
