# Modul 6 — MCP (Model Context Protocol)

> **Környezet:** .NET 10 · Qwen2.5-7B · LlamaSharp / Ollama · ASP.NET Core · ModelContextProtocol  
> **Előfeltétel:** `modul-00-environment-setup.md` + Modul 2 (Tool Calling)  
> **Időbecslés:** ~120 perc  

---

## 6.1 Koncepció: MCP vs. hard-coded integráció

```
Hagyományos: N agent × M service = N×M egyedi integráció ❌

MCP:         N agent + M server = N+M integráció ✅
             Egy agent bármely MCP server-hez csatlakozhat
             Egy MCP server bármely agent-nek elérhető
```

```
Agent (SK/MAF)
  └── MCP Client
        └── JSON-RPC / SSE
              └── MCP Server (ASP.NET Core)
                    ├── Tool: GetWeather()
                    ├── Tool: SearchDatabase()
                    └── Resource: SystemLogs
```

### MCP vs REST

| | REST | MCP |
|---|---|---|
| **Célközönség** | Ember és gép | LLM agent |
| **Discovery** | Statikus OpenAPI | Dinamikus (`tools/list`) |
| **Kontextus** | Nincs | Agent frissíti |
| **Mikor** | Service-to-service | Agent-to-service |

---

## 6.2 MCP Server — ASP.NET Core

### Projekt + NuGet

```bash
cd dotnet-ai-labs
dotnet new webapi -n Lab06.McpServer --framework net10.0
dotnet sln add Lab06.McpServer
cd Lab06.McpServer
dotnet add package ModelContextProtocol.AspNetCore
```

### Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<WeatherTools>()
    .WithTools<CalculatorTools>()
    .WithResources<SystemResources>();

var app = builder.Build();
app.MapMcp();   // /mcp endpoint

await app.RunAsync();
```

### Tool osztály

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;

[McpServerToolType]
public class WeatherTools
{
    [McpServerTool]
    [Description("Gets current weather for a city. " +
                 "Use when user asks about weather conditions.")]
    public string GetCurrentWeather(
        [Description("City name, e.g. Budapest")] string city)
    {
        return city.ToLower() switch
        {
            "budapest" => "18°C, partly cloudy",
            "london"   => "12°C, rainy",
            _          => $"No weather data for {city}"
        };
    }

    [McpServerTool]
    [Description("Gets weather forecast for the next N days.")]
    public string GetForecast(
        [Description("City name")] string city,
        [Description("Number of days (1-7)")] int days = 3)
    {
        return $"Forecast for {city}: {days} days of variable weather. " +
               $"Temperatures 15-22°C.";
    }
}

[McpServerResourceType]
public class SystemResources
{
    [McpServerResource(UriTemplate = "system://health")]
    [Description("Current system health status")]
    public string GetSystemHealth() =>
        $"Status: Healthy | Timestamp: {DateTime.UtcNow:O}";
}
```

---

## 6.3 MCP Client — SK integráció

### Projekt + NuGet

```bash
cd dotnet-ai-labs
dotnet new console -n Lab06.McpClient --framework net10.0
dotnet sln add Lab06.McpClient
cd Lab06.McpClient
dotnet add reference ../AiLabs.Infrastructure
dotnet add package ModelContextProtocol
dotnet add package Microsoft.SemanticKernel
```

### Client kód

```csharp
using AiLabs.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json").Build();

// MCP Client — csatlakozás az MCP Server-hez
var mcpClient = await McpClientFactory.CreateAsync(
    new SseClientTransport(new SseClientTransportOptions
    {
        Endpoint = new Uri("http://localhost:5000/mcp")
    }));

// Tool discovery — dinamikus, nem statikus!
var tools = await mcpClient.ListToolsAsync();
Console.WriteLine("📋 Available MCP Tools:");
foreach (var tool in tools)
    Console.WriteLine($"  - {tool.Name}: {tool.Description}");

// SK Kernel + MCP tool-ok
var kernel = Kernel.CreateBuilder()
    .Services
        .AddSingleton(ProviderFactory.CreateChatClient(config))
    .AddKernel()
    .Build()
    .GetRequiredService<Kernel>();

// MCP tool-ok automatikusan SK plugin-ná konvertálódnak
kernel.Plugins.AddMcpTools(tools, mcpClient);

// Agent hívás — a tool-okat az MCP Server-en hajtja végre
var result = await kernel.InvokePromptAsync(
    "Milyen az idő Budapesten és Londonban a következő 3 napban?",
    new KernelArguments(new PromptExecutionSettings
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
    }));

Console.WriteLine(result);
```

---

## 6.4 Multi-MCP — több server egy agent-nek

```csharp
var weatherMcp = await McpClientFactory.CreateAsync(
    new SseClientTransport(new SseClientTransportOptions
    { Endpoint = new Uri("http://localhost:5000/mcp") }));

var ordersMcp = await McpClientFactory.CreateAsync(
    new SseClientTransport(new SseClientTransportOptions
    { Endpoint = new Uri("http://localhost:5001/mcp") }));

// Mindkét server tool-jait betöltjük
var weatherTools = await weatherMcp.ListToolsAsync();
var orderTools   = await ordersMcp.ListToolsAsync();

kernel.Plugins.AddMcpTools(weatherTools, weatherMcp);
kernel.Plugins.AddMcpTools(orderTools,   ordersMcp);

// Az agent mindkét tool készletet egyszerre használja
var result = await kernel.InvokePromptAsync(
    "Milyen az idő és hol tart az ORD-12345 rendelés?");
```

---

## 🧪 Hands-on Lab 6

### Lab struktúra

```
Lab06.McpServer/    (ASP.NET Core MCP Server)
Lab06.McpClient/    (Console Agent)
```

### Lab feladatok

**Feladat 1 — MCP Server felépítése**
Implementálj `WeatherTools`-t 2 tool-lal: `GetCurrentWeather` és `GetForecast`.

**Feladat 2 — MCP Client + SK integráció**
Csatlakoztasd az agent-et. Teszteld: "Milyen lesz az idő Bp-en 3 napig?"

**Feladat 3 — Tool discovery**
Induláskor írd ki az összes elérhető MCP tool-t névvel és leírással.

**Feladat 4 — Két MCP Server (kihívás)**
Második `CalculatorTools` MCP Server.
Az agent mindkét tool készletet egyszerre használja.

### 🛑 Checkpoint
MCP Server fut, agent dinamikusan fedezi fel a tool-okat,
két server esetén mindkét tool készletet használja.

---

## Modul 6 — Összefoglalás

```
MCP Protocol    →  JSON-RPC, tool/resource/prompt primitívek
Transport       →  SSE (web) | stdio (process)
[McpServerTool] →  tool definíció
AddMcpTools()   →  MCP tool-ok → SK plugin (automatikusan)
Multi-MCP       →  egy agent + több server = N+M integráció
```

---

## Következő: Modul 7 — Biztonság, HITL, Guardrails

> Lásd: `modul-07-security.md`
