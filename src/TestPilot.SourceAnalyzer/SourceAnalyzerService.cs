using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using TestPilot.Core.Interfaces;
using TestPilot.Core.Models;

namespace TestPilot.SourceAnalyzer;

public sealed class SourceAnalyzerService : ISourceAnalyzer
{
    private static readonly object _initLock = new();
    private static bool _msBuildRegistered;

    public async Task<IReadOnlyList<ClassInfo>> AnalyzeAsync(string solutionPath, bool includeInternal = false, CoverageReport? coverage = null)
    {
        EnsureMsBuildRegistered();

        using var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionPath);

        var testClassNames = coverage is null ? await CollectTestClassNamesAsync(solution) : [];
        var results = new List<ClassInfo>();

        foreach (var project in solution.Projects)
        {
            if (IsTestProject(project.Name))
                continue;

            var compilation = await project.GetCompilationAsync();
            if (compilation is null)
                continue;

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = await syntaxTree.GetRootAsync();

                foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    if (semanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol)
                        continue;

                    // TypeDeclarationSyntax cubre class, record, struct y record struct. Las
                    // interfaces se descartan por TypeKind: no tienen implementación que testear.
                    if (symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct))
                        continue;

                    var isAnalyzable = symbol.DeclaredAccessibility == Accessibility.Public
                        || (includeInternal && symbol.DeclaredAccessibility == Accessibility.Internal);
                    // Una `static class` se compila como abstract sealed: no se instancia, pero sus
                    // métodos sí se pueden testear. Solo se descartan las abstractas de verdad.
                    if (!isAnalyzable || (symbol.IsAbstract && !symbol.IsStatic))
                        continue;

                    // Con cobertura real el dato es por método; sin ella se cae a la heurística de
                    // nombres, que solo puede decir algo a nivel de clase.
                    var classHasTests = coverage is null && testClassNames.Any(name =>
                        name.Contains(symbol.Name, StringComparison.OrdinalIgnoreCase) &&
                        (name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
                         name.EndsWith("Test", StringComparison.OrdinalIgnoreCase)));

                    var fullName = symbol.ToDisplayString();

                    var methods = symbol.GetMembers()
                        .OfType<IMethodSymbol>()
                        .Where(m => m.DeclaredAccessibility == Accessibility.Public
                                 && m.MethodKind == MethodKind.Ordinary
                                 && !m.IsImplicitlyDeclared)
                        .Select(m => new MethodInfo(
                            m.Name,
                            BuildSignature(m),
                            coverage?.IsCovered(fullName, m.Name) ?? classHasTests))
                        .ToList();

                    results.Add(new ClassInfo(fullName, syntaxTree.FilePath, methods));
                }
            }
        }

        return results;
    }

    private static void EnsureMsBuildRegistered()
    {
        if (_msBuildRegistered) return;
        lock (_initLock)
        {
            if (!_msBuildRegistered)
            {
                MSBuildLocator.RegisterDefaults();
                _msBuildRegistered = true;
            }
        }
    }

    // Camino por defecto: detectar tests por nombre de clase. Si se pasa un CoverageReport, el dato
    // sale de la cobertura real medida con coverlet y este heurístico no se usa.
    // El filtro por nombre evita analizar los propios tests como código de producción.
    private static bool IsTestProject(string name) =>
        name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("Test", StringComparison.OrdinalIgnoreCase);

    private static async Task<List<string>> CollectTestClassNamesAsync(Solution solution)
    {
        var names = new List<string>();
        foreach (var project in solution.Projects)
        {
            if (!IsTestProject(project.Name)) continue;
            foreach (var document in project.Documents)
            {
                var root = await document.GetSyntaxRootAsync();
                if (root is null) continue;
                names.AddRange(root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Select(c => c.Identifier.ValueText));
            }
        }
        return names;
    }

    private static string BuildSignature(IMethodSymbol method)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
        var modifier = method.IsStatic ? "static " : string.Empty;
        return $"{modifier}{method.ReturnType.ToDisplayString()} {method.Name}({parameters})";
    }
}
