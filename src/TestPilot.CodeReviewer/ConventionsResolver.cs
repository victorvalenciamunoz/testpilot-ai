namespace TestPilot.CodeReviewer;

public static class ConventionsResolver
{
    // Devuelve el texto de convenciones a aplicar, o null si no se indicó ninguna. Una ruta que no
    // existe se trata como error y no como "sin convenciones": devolver una revisión genérica en
    // silencio haría creer que se aplicaron las reglas del proyecto cuando no fue así.
    public static async Task<(string? Text, string? Error)> ResolveAsync(string? conventions, string? conventionsPath)
    {
        var hasText = !string.IsNullOrWhiteSpace(conventions);
        var hasPath = !string.IsNullOrWhiteSpace(conventionsPath);

        if (hasText && hasPath)
            return (null, "Indica 'conventions' (texto) o 'conventionsPath' (ruta), no ambos.");

        if (!hasPath)
            return (hasText ? conventions : null, null);

        if (!File.Exists(conventionsPath))
            return (null, $"No se encuentra el archivo de convenciones: {conventionsPath}");

        return (await File.ReadAllTextAsync(conventionsPath!), null);
    }
}
