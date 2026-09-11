using System.Globalization;
using System.Xml.Linq;
using TestPilot.Core.Models;

namespace TestPilot.TestRunner;

public static class CoberturaParser
{
    public static CoverageReport Parse(string coberturaXml)
    {
        var covered = new Dictionary<string, HashSet<string>>();

        foreach (var classElement in XDocument.Parse(coberturaXml).Descendants("class"))
        {
            var rawName = (string?)classElement.Attribute("name");
            if (rawName is null || IsCompilerGenerated(rawName))
                continue;

            // Cobertura usa '/' para tipos anidados; Roslyn los muestra con '.'.
            var className = rawName.Replace('/', '.');

            foreach (var method in classElement.Descendants("method"))
            {
                var methodName = (string?)method.Attribute("name");
                if (methodName is null || IsCompilerGenerated(methodName) || !HasHits(method))
                    continue;

                if (!covered.TryGetValue(className, out var methods))
                    covered[className] = methods = [];

                methods.Add(methodName);
            }
        }

        // Una misma clase aparece en varios elementos <class>, uno por archivo fuente (incluidos
        // los generados por el compilador), así que los métodos se acumulan en vez de sustituirse.
        return new CoverageReport(
            covered.ToDictionary(e => e.Key, e => (IReadOnlySet<string>)e.Value));
    }

    private static bool HasHits(XElement method) =>
        double.TryParse((string?)method.Attribute("line-rate"), NumberStyles.Float, CultureInfo.InvariantCulture, out var rate)
            ? rate > 0
            : method.Descendants("line").Any(l => (int?)l.Attribute("hits") > 0);

    private static bool IsCompilerGenerated(string name) =>
        name.Contains('<') || name.Contains('>');
}
