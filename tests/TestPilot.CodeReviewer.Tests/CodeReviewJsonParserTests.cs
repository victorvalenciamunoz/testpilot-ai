using TestPilot.CodeReviewer;
using TestPilot.Core.Models;

namespace TestPilot.CodeReviewer.Tests;

public class CodeReviewJsonParserTests
{
    private const string SampleJson = """
        {
          "summary": "Se detectó un posible null reference.",
          "findings": [
            {
              "severity": "Error",
              "file": "src/Foo.cs",
              "line": 42,
              "category": "NullReference",
              "description": "El parámetro 'user' no se valida antes de usarse.",
              "suggestion": "Añade Guard.Against.Null(user)."
            }
          ]
        }
        """;

    [Fact]
    public void Parse_WithRawJson_DeserializesCorrectly()
    {
        var result = CodeReviewJsonParser.Parse(SampleJson);

        Assert.Equal("Se detectó un posible null reference.", result.Summary);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(ReviewSeverity.Error, finding.Severity);
        Assert.Equal("src/Foo.cs", finding.File);
        Assert.Equal(42, finding.Line);
        Assert.Equal("NullReference", finding.Category);
    }

    [Fact]
    public void Parse_WrappedInJsonMarkdownFence_StripsFenceAndDeserializes()
    {
        var wrapped = $"```json\n{SampleJson}\n```";

        var result = CodeReviewJsonParser.Parse(wrapped);

        Assert.Single(result.Findings);
    }

    [Fact]
    public void Parse_WrappedInPlainMarkdownFence_StripsFenceAndDeserializes()
    {
        var wrapped = $"```\n{SampleJson}\n```";

        var result = CodeReviewJsonParser.Parse(wrapped);

        Assert.Single(result.Findings);
    }

    [Fact]
    public void Parse_WithEmptyFindings_ReturnsEmptyList()
    {
        const string json = """{ "summary": "Sin problemas.", "findings": [] }""";

        var result = CodeReviewJsonParser.Parse(json);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Parse_WithoutOptionalFields_LeavesThemNull()
    {
        const string json = """
            {
              "summary": "ok",
              "findings": [
                { "severity": "Warning", "file": "src/Bar.cs", "category": "Design", "description": "desc" }
              ]
            }
            """;

        var result = CodeReviewJsonParser.Parse(json);

        var finding = Assert.Single(result.Findings);
        Assert.Null(finding.Line);
        Assert.Null(finding.Suggestion);
    }
}
