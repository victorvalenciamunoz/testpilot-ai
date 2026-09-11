namespace TestPilot.Core.Models;

// Cobertura real medida con coverlet, por clase y método. Sustituye a la heurística de nombres,
// que daba falsos positivos (`Order` se daba por testeada si existía `OrderServiceTests`).
public sealed record CoverageReport(IReadOnlyDictionary<string, IReadOnlySet<string>> CoveredMethodsByClass)
{
    public bool IsCovered(string classFullName, string methodName) =>
        CoveredMethodsByClass.TryGetValue(classFullName, out var methods) && methods.Contains(methodName);
}
