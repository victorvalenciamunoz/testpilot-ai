using TestPilot.TestRunner;

namespace TestPilot.TestRunner.Tests;

public class CoberturaParserTests
{
    // Recorte del formato real que emite coverlet: la misma clase aparece en dos elementos <class>
    // (uno por archivo fuente, incluido el generado por el compilador) y hay tipos sintetizados.
    private const string SampleXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="0.45" version="1.9">
          <packages>
            <package name="MyApp">
              <classes>
                <class name="MyApp.Calculator" filename="MyApp\Calculator.cs" line-rate="0.5">
                  <methods>
                    <method name="Add" signature="(System.Int32,System.Int32)" line-rate="1">
                      <lines><line number="5" hits="3" /></lines>
                    </method>
                    <method name="Divide" signature="(System.Int32,System.Int32)" line-rate="0">
                      <lines><line number="6" hits="0" /></lines>
                    </method>
                  </methods>
                </class>
                <class name="MyApp.Calculator" filename="MyApp\obj\Generated.g.cs" line-rate="1">
                  <methods>
                    <method name="Helper" signature="()" line-rate="1">
                      <lines><line number="9" hits="2" /></lines>
                    </method>
                  </methods>
                </class>
                <class name="MyApp.Outer/Inner" filename="MyApp\Outer.cs" line-rate="1">
                  <methods>
                    <method name="Nested" signature="()" line-rate="1">
                      <lines><line number="3" hits="1" /></lines>
                    </method>
                  </methods>
                </class>
                <class name="MyApp.Service/&lt;&gt;c__DisplayClass3_0" filename="MyApp\Service.cs" line-rate="1">
                  <methods>
                    <method name="&lt;Run&gt;b__0" signature="()" line-rate="1">
                      <lines><line number="7" hits="1" /></lines>
                    </method>
                  </methods>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;

    [Fact]
    public void Parse_MetodoConLineasEjecutadas_LoMarcaCubierto()
    {
        var report = CoberturaParser.Parse(SampleXml);

        Assert.True(report.IsCovered("MyApp.Calculator", "Add"));
    }

    [Fact]
    public void Parse_MetodoSinLineasEjecutadas_NoLoMarcaCubierto()
    {
        var report = CoberturaParser.Parse(SampleXml);

        Assert.False(report.IsCovered("MyApp.Calculator", "Divide"));
    }

    [Fact]
    public void Parse_ClaseRepetidaEnVariosElementos_AcumulaSusMetodos()
    {
        var report = CoberturaParser.Parse(SampleXml);

        Assert.True(report.IsCovered("MyApp.Calculator", "Add"));
        Assert.True(report.IsCovered("MyApp.Calculator", "Helper"));
    }

    [Fact]
    public void Parse_TipoAnidado_NormalizaLaBarraAPunto()
    {
        var report = CoberturaParser.Parse(SampleXml);

        Assert.True(report.IsCovered("MyApp.Outer.Inner", "Nested"));
    }

    [Fact]
    public void Parse_TiposYMetodosGeneradosPorElCompilador_LosDescarta()
    {
        var report = CoberturaParser.Parse(SampleXml);

        Assert.DoesNotContain(report.CoveredMethodsByClass.Keys, k => k.Contains("DisplayClass"));
    }

    [Fact]
    public void Parse_ClaseDesconocida_NoSeConsideraCubierta()
    {
        var report = CoberturaParser.Parse(SampleXml);

        Assert.False(report.IsCovered("MyApp.NoExiste", "Cualquiera"));
    }
}
