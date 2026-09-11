using System.Text.RegularExpressions;

namespace TestPilot.Core.Text;

public static partial class CompilerErrorFilter
{
    // Se queda con los errores atribuibles a un archivo concreto. Los que no tienen forma de error
    // del compilador (fallos de test en ejecución) se consideran propios: la ejecución ya viene
    // acotada a la clase generada, así que no pueden proceder de otra.
    public static IReadOnlyList<string> ErrorsFrom(IEnumerable<string> errors, string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);

        return errors
            .Where(e => !CompilerErrorRegex().IsMatch(e)
                     || e.StartsWith(fullPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    [GeneratedRegex(@"^.+\(\d+,\d+\): CS\d+: ")]
    private static partial Regex CompilerErrorRegex();
}
