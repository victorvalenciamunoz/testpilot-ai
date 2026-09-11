namespace TestPilot.TestGenerator;

public static class TestClassNaming
{
    // `dotnet test --filter FullyQualifiedName~X` compara por subcadena contra el nombre completo
    // real del test. El prompt genera la clase en el namespace `<original>.Tests`, que no coincide
    // con el de la clase objetivo, así que el filtro tiene que ser el nombre simple.
    public static string TestClassFilter(string targetClassFullName) =>
        targetClassFullName.Split('.')[^1] + "Tests";
}
