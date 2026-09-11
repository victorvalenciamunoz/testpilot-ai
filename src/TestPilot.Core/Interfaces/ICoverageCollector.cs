using TestPilot.Core.Models;

namespace TestPilot.Core.Interfaces;

public interface ICoverageCollector
{
    // Devuelve error en vez de un informe vacío cuando no se puede medir: un informe vacío haría
    // que TODAS las clases parecieran sin cubrir y se generasen tests para la solución entera.
    Task<(CoverageReport? Report, string? Error)> CollectAsync(string testProjectPath);
}
