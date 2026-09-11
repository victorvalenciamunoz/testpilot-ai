# TestPilot AI

Herramienta .NET que analiza una solución con Roslyn, detecta el código sin probar, genera tests
xUnit con un LLM, **los ejecuta y corrige sus propios errores de compilación** hasta que pasan.
Incluye además un revisor de código que trabaja sobre un `git diff`.

Se usa como API REST o como servidor MCP, sobre los mismos módulos.

---

## El problema

Subir la cobertura de un proyecto .NET existente es trabajo mecánico y tedioso: mucho boilerplate,
poca decisión interesante. El resultado habitual es cobertura baja, tests obsoletos y código que
nadie revisa a fondo.

Pedirle tests a un LLM en un chat tampoco resuelve gran cosa: devuelve código plausible que a menudo
no compila, inventa APIs que no existen y no tiene forma de saber si lo que escribió funciona.

## Qué hace

1. **Detecta** qué clases y métodos no tienen pruebas, con análisis semántico real (Roslyn) y,
   opcionalmente, con cobertura medida de verdad (coverlet).
2. **Genera** una clase de tests xUnit por cada clase sin cubrir, pasándole al modelo el código
   fuente completo, no solo las firmas.
3. **Ejecuta** los tests generados con `dotnet test`.
4. **Corrige**: lee los errores reales del compilador y se los devuelve al modelo para que arregle
   su propio código. Hasta 3 intentos.
5. **Revisa** cambios de código a partir de un `git diff`, devolviendo hallazgos estructurados
   (severidad, archivo, línea, categoría, descripción y sugerencia).

## Qué pretende

El paso 4 es el que distingue esto de un prompt bien escrito. **El compilador hace de juez**: el
modelo no entrega texto plausible, entrega código que ha tenido que compilar y pasar. Ese bucle
—analizar, generar, ejecutar, corregir— es el núcleo del proyecto.

TestPilot **no pretende sustituir los tests escritos por personas.** Pretende:

- dar un punto de partida sólido donde hoy no hay nada,
- señalar los huecos de cobertura reales,
- automatizar la parte mecánica,

para que quien desarrolla dedique su tiempo a los casos límite y al comportamiento de negocio, que
es donde un test aporta de verdad.

### Lo que deliberadamente deja fuera

- **Estilo y nomenclatura.** Eso lo comprueban `.editorconfig` y los analizadores de Roslyn, de
  forma exacta y en tiempo de compilación. Un LLM ahí solo añadiría ruido.
- **Reglas de arquitectura** del tipo "la capa X no debe referenciar la Y". Para eso están los tests
  de arquitectura (NetArchTest, ArchUnitNET), que son deterministas.

El revisor se centra en lo que **exige juicio** y ninguna herramienta comprueba: responsabilidades
de una clase, abstracciones que sobran, manejo de errores acordado, expresividad de los nombres.

---

## Arquitectura

```
Cliente HTTP                    Cliente MCP
     |                               |
     v                               v
TestPilot.API                 TestPilot.McpServer
(endpoints REST)              (stdio, 4 tools)
     +--------------+----------------+
                    |
                    v
   SourceAnalyzer  TestGenerator  TestRunner  CodeReviewer
      (Roslyn)        (LLM)     (dotnet test)    (LLM)
```

| Módulo | Responsabilidad |
|---|---|
| `TestPilot.Core` | Interfaces y modelos. Sin dependencias de implementación |
| `TestPilot.SourceAnalyzer` | Carga la solución con Roslyn y encuentra clases y métodos sin tests |
| `TestPilot.TestGenerator` | Genera el código de los tests y ejecuta el bucle corrector |
| `TestPilot.TestRunner` | Ejecuta `dotnet test`, parsea resultados y recoge cobertura |
| `TestPilot.ReportGenerator` | Agrega resultados en un informe |
| `TestPilot.CodeReviewer` | Revisa un `git diff` y devuelve hallazgos estructurados |
| `TestPilot.API` | Host HTTP. Orquesta los módulos y expone endpoints REST |
| `TestPilot.McpServer` | Host MCP sobre stdio. Expone los módulos como herramientas |

Cada módulo es una biblioteca independiente que **solo depende de `Core`**. Los dos hosts no
contienen lógica de negocio: validan entradas, orquestan y serializan.

El proveedor de IA está aislado detrás de `IChatClient` (Microsoft.Extensions.AI): los módulos no
saben qué modelo hay detrás ni cómo se habla con él.

---

## Requisitos

- **.NET 10 SDK**
- Una clave de API de un proveedor compatible con la API de OpenAI (por defecto, Groq)
- Para medir cobertura real, el proyecto de tests analizado debe referenciar `coverlet.collector`

## Puesta en marcha

```bash
git clone <url-del-repo>
cd testpilot
dotnet build src/TestPilot.slnx
```

Configura la clave (no se guarda en el repositorio):

```bash
dotnet user-secrets set "AI:ApiKey" "<tu-clave>" --project src/TestPilot.API
```

Arranca la API:

```bash
dotnet run --project src/TestPilot.API
```

Escucha en `http://localhost:5105`. En desarrollo, la especificación OpenAPI está en
`/openapi/v1.json`. El archivo `src/TestPilot.API/TestPilot.API.http` trae peticiones de ejemplo
listas para lanzar desde el IDE.

---

## API REST

| Endpoint | Qué hace |
|---|---|
| `POST /api/analyze` | Analiza una solución y devuelve clases, métodos y cuáles tienen tests |
| `POST /api/generate` | Genera tests para las clases sin cubrir; opcionalmente los ejecuta y corrige |
| `POST /api/review` | Lee el `git diff` de un repositorio y lo revisa |
| `POST /api/review/diff` | Revisa un diff que se le pasa directamente |

**Analizar:**

```jsonc
{
  "solutionPath": "C:\\ruta\\MiSolucion.sln",
  "includeInternal": false,        // incluir clases internal (útil en CQRS con handlers internal)
  "testProjectPath": null          // si se indica, mide cobertura real en vez de usar la heurística
}
```

**Generar, con bucle corrector:**

```jsonc
{
  "solutionPath": "C:\\ruta\\MiSolucion.sln",
  "outputPath": "C:\\ruta\\tests\\MiProyecto.Tests",   // debe estar dentro del proyecto de tests
  "testProjectPath": "C:\\ruta\\tests\\MiProyecto.Tests",
  "classNames": ["MyApp.Services.OrderService"],       // opcional: solo estas clases
  "useCoverage": true                                  // decidir los huecos por cobertura real
}
```

Sin `testProjectPath` el test se genera y se escribe, pero **no se ejecuta ni se verifica**.

**Revisar, con las convenciones del proyecto:**

```jsonc
{
  "repoPath": "C:\\ruta\\mi-repo",
  "baseBranch": "main",
  "conventionsPath": "C:\\ruta\\mi-repo\\CONTRIBUTING.md"
}
```

Las convenciones también pueden pasarse como texto en `conventions`. Son excluyentes entre sí, y una
ruta que no existe devuelve error en vez de revisar sin ellas.

---

## Servidor MCP

El mismo motor, expuesto como herramientas para clientes MCP.

```bash
dotnet build src/TestPilot.McpServer
```

Regístralo en tu cliente MCP con transporte **stdio**, apuntando al `.dll` compilado:

```jsonc
{
  "mcpServers": {
    "testpilot": {
      "command": "dotnet",
      "args": ["<ruta-absoluta>/src/TestPilot.McpServer/bin/Debug/net10.0/TestPilot.McpServer.dll"],
      "env": { "AI__ApiKey": "<tu-clave>" }
    }
  }
}
```

| Herramienta | Qué hace |
|---|---|
| `analyze_solution` | Analiza la solución; con `testProjectPath` usa cobertura real |
| `generate_tests` | Genera el test de una clase concreta, con bucle corrector opcional |
| `run_tests` | Ejecuta `dotnet test` sobre un proyecto |
| `review_code` | Revisa un diff, opcionalmente con las convenciones del proyecto |

Se apunta al `.dll` y no a `dotnet run` a propósito: `dotnet run` escribe salida de compilación en
stdout, que es donde vive el protocolo JSON-RPC.

El servidor comparte `UserSecretsId` con la API, así que una sola clave sirve para ambos. Las
variables de entorno (`AI__ApiKey`, `AI__ModelId`, `AI__Endpoint`) la sobrescriben.

---

## Configuración del proveedor de IA

Tres valores, todos en configuración:

| Clave | Por defecto |
|---|---|
| `AI:Endpoint` | `https://api.groq.com/openai/v1/` |
| `AI:ModelId` | `openai/gpt-oss-120b` |
| `AI:ApiKey` | (vacío; ponla en user-secrets) |

Cambiar de proveedor **compatible con OpenAI** es cambiar la URL, sin tocar código:

| Proveedor | `AI:Endpoint` | `AI:ModelId` |
|---|---|---|
| Groq | `https://api.groq.com/openai/v1/` | `openai/gpt-oss-120b` |
| Ollama (local) | `http://localhost:11434/v1/` | `qwen2.5-coder:14b` |
| LM Studio (local) | `http://localhost:1234/v1/` | el que tengas cargado |

Los proveedores locales ignoran la clave. Para un proveedor con **otro protocolo** (Bedrock, Gemini
nativo, Azure AI Inference) hay que cambiar el paquete adaptador que construye el `IChatClient`; los
módulos no se enteran.

Con modelos locales, ten en cuenta que los prompts exigen JSON estricto (revisión) y C# compilable
(generación). Los modelos pequeños fallan en ambas cosas a menudo y agotan el bucle corrector;
conviene un modelo de código de 14B o más.

---

## Cómo decide qué está sin probar

Hay dos caminos, y la diferencia importa:

**Por nombre (por defecto).** Instantáneo, solo análisis estático. Pero es una heurística con falsos
positivos conocidos: una clase `Order` se da por testeada si existe `OrderServiceTests`, porque ese
nombre contiene "Order". Y solo puede afirmar algo por clase, no por método.

**Por cobertura real (recomendado).** Indicando `testProjectPath`, ejecuta los tests con coverlet y
lee el informe Cobertura. Es exacto y **por método**. A cambio hay que compilar y ejecutar, así que
es opt-in y nunca automático.

Ten presente que la cobertura mide **ejecución, no comprobación**: un método puede figurar como
cubierto porque otro test pasó por él sin afirmar nada sobre su resultado. Es muy fiable para decir
"esto seguro que NO está probado", que es justo para lo que se usa aquí.

## Cómo trata las dependencias

Los tests generados usan objetos reales y fakes escritos a mano. Cuando una dependencia es una clase
concreta de un framework externo imposible de construir (por ejemplo `UserManager<T>` de ASP.NET
Identity), hay tres salidas:

- Si el método bajo prueba **no usa** esa dependencia, se pasa `null!` al constructor.
- Si la necesita y el proyecto de tests **ya referencia** Moq, NSubstitute o FakeItEasy, se usa esa
  librería solo para ese caso. La detección se hace leyendo el proyecto: nunca se propone un paquete
  que no esté disponible, porque el test no compilaría.
- Si no hay ninguna disponible, el test se emite como `[Fact(Skip = "motivo")]`, con el motivo
  escrito. Un archivo que compila y declara qué no pudo probarse vale más que uno que no compila.

---

## Tests del propio proyecto

```bash
dotnet test src/TestPilot.slnx                                   # todo
dotnet test src/TestPilot.slnx --filter "Category!=Integration"  # solo unitarios, sin red
```

Los tests marcados como `Integration` levantan proyectos .NET temporales en disco y ejecutan
`dotnet test` de verdad sobre ellos. Los de `TestPilot.TestGenerator.Tests` requieren además una
clave de API configurada; sin ella se omiten solos.

No se usan librerías de mocking: se emplean implementaciones reales.

## Limitaciones conocidas

- La cobertura se une por nombre de clase y método, así que **las sobrecargas se confunden**: si una
  de ellas está cubierta, todas cuentan como cubiertas.
- La heurística por nombre da falsos positivos y negativos. Usa cobertura real cuando importe.
- El bucle corrector se detiene a los 3 intentos; el último resultado se conserva marcado con
  `HasErrors`.
- El análisis carga la solución completa con MSBuild, lo que tarda unos segundos en soluciones
  grandes.
- Sin persistencia: todo ocurre en memoria, petición a petición.

## Stack

.NET 10 · ASP.NET Core · Roslyn (`Microsoft.CodeAnalysis`) · Microsoft.Extensions.AI · xUnit ·
coverlet · Model Context Protocol
