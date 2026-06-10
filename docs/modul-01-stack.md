# Modul 1 — A .NET AI Stack rétegei

> **Környezet:** .NET 10 · Qwen2.5-7B · LlamaSharp / Ollama · Microsoft.Extensions.AI · Semantic Kernel  
> **Előfeltétel:** `modul-00-environment-setup.md` teljesítve — futó `AiLabs.Infrastructure` projekt  
> **Időbecslés:** ~60 perc  

---

## 1.1 Koncepció: Miért három réteg?

```csharp
// ❌ Így szokták csinálni — vendor lock-in
var client = new OpenAIClient(apiKey);
// Ha Azure OpenAI-ra, Ollamára vagy másra váltasz → refactor mindenhol
```

**A megoldás — három felelősségi réteg:**

```
┌──────────────────────────────────────────────────────┐
│                  A TE KÓDOD                          │
├──────────────────────────────────────────────────────┤
│             SEMANTIC KERNEL                          │
│   Plugins, Memory, Planner, AgentGroupChat           │
├──────────────────────────────────────────────────────┤
│         MICROSOFT.EXTENSIONS.AI                      │
│   IChatClient — egységes interfész minden providerre │
├──────────────────────────────────────────────────────┤
│            LLM PROVIDER (cserélhető)                 │
│   LlamaSharp │ Ollama │ Azure OpenAI │ OpenAI │ ...  │
└──────────────────────────────────────────────────────┘
```

**Az analógia:** az `IChatClient` olyan mint az `ILogger` — nem érdekel, hogy fájlba,
konzolra vagy Seq-be megy a log. Ugyanígy az `IChatClient` mögött lehet bármi.

---

## 1.2 Microsoft.Extensions.AI — az absztrakciós réteg

A két legfontosabb interfész:

```csharp
IChatClient                                    // szöveges chat (esetek 95%-a)
IEmbeddingGenerator<string, Embedding<float>>  // embedding — RAG, Vector DB
```

### Provider váltás — 1 sor változás

```csharp
// LlamaSharp (in-process GGUF):
IChatClient client = new LLamaSharpChatClient(executor);

// Ollama (OpenAI-kompatibilis API):
IChatClient client = new OllamaApiClient(new Uri("http://localhost:11434"))
    .AsChatClient("qwen2.5:7b");

// Azure OpenAI:
IChatClient client = new AzureOpenAIClient(new Uri(endpoint), credential)
    .AsChatClient("gpt-4o");
```

Ettől a ponttól a kód **teljesen provider-független**.

---

## 1.3 M.E.AI Middleware pipeline

```csharp
// Production stack — dekorátor minta, kívülről befelé fut
IChatClient client = new ChatClientBuilder(innerClient)
    .UseLogging()            // minden kérés/válasz logolva
    .UseOpenTelemetry()      // trace-elés
    .UseFunctionInvocation() // auto tool call végrehajtás
    .Build();
```

> ⚠️ `.UseFunctionInvocation()` automatikusan végrehajtja a tool call-okat.
> Production-ban megfontoltan — Modul 7-ben (HITL) visszatérünk rá.

---

## 1.4 Semantic Kernel — az agent framework

| | M.E.AI | Semantic Kernel |
|---|---|---|
| **Felelősség** | LLM kommunikáció | Agent logika |
| **Szint** | Alacsony (primitív) | Magas (orkesztráció) |
| **Analógia** | `HttpClient` | ASP.NET Core |

Az SK **az M.E.AI-t használja** belsőleg — nem konkurensek.

### Mikor elég az M.E.AI, mikor kell az SK?

```
Csak M.E.AI:    egyszerű chat, saját agent loop, teljes kontroll
Semantic Kernel: plugin/tool rendszer, multi-agent, built-in memory, workflow
```

---

## 🧪 Hands-on Lab 1

### Projekt létrehozása

```bash
cd dotnet-ai-labs
dotnet new console -n Lab01.Stack --framework net10.0
dotnet sln add Lab01.Stack
cd Lab01.Stack
dotnet add reference ../AiLabs.Infrastructure
```

Másold be az `appsettings.json`-t a `modul-00-environment-setup.md` alapján,
és add hozzá a `.csproj`-hoz a `CopyToOutputDirectory` beállítást.

### Step 1 — Alap IChatClient hívás

```csharp
using AiLabs.Infrastructure;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

// Provider a config alapján (LlamaSharp VAGY Ollama)
var chatClient = ProviderFactory.CreateChatClient(config);

Console.WriteLine($"⏳ Sending first message...");

var response = await chatClient.CompleteAsync(
    "Mondj egy mondatot arról, mi az AI agent.");

Console.WriteLine($"🤖 {response.Message.Text}");
```

**Teszteld mindkét providerrel:**
```json
// appsettings.json — csak ezt változtatd
{ "Provider": "LlamaSharp" }   // → újrafuttatás
{ "Provider": "Ollama" }       // → újrafuttatás
```

### 🛑 Checkpoint 1
Mindkét provider válaszol, a kód nem változott. Az absztrakció működik.

---

### Step 2 — Middleware pipeline

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection()
    .AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug))
    .BuildServiceProvider();

var loggerFactory = services.GetRequiredService<ILoggerFactory>();

// Middleware pipeline az alap IChatClient fölé
var pipelineClient = new ChatClientBuilder(
        ProviderFactory.CreateChatClient(config))
    .UseLogging(loggerFactory)
    .UseFunctionInvocation()
    .Build();

var response = await pipelineClient.CompleteAsync(
    "Mi a különbség az IChatClient és a Semantic Kernel között?");

Console.WriteLine(response.Message.Text);
```

Futtasd — a konzolon látod a teljes request/response ciklust logolva.

---

### Step 3 — Semantic Kernel rákötése

```csharp
using AiLabs.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var services = new ServiceCollection()
    .AddLogging(b => b.AddConsole())
    .AddAiLabsInfrastructure(config)   // IChatClient + Kernel regisztrálva
    .BuildServiceProvider();

var kernel = services.GetRequiredService<Kernel>();

// SK prompt hívás — ugyanaz a provider, ugyanaz a middleware pipeline
var result = await kernel.InvokePromptAsync(
    "Sorolj fel 3 különbséget az AI agent és egy hagyományos script között.");

Console.WriteLine(result);
```

### 🛑 Checkpoint 2
Az SK ugyanazzal a lokális modellel dolgozik, middleware pipeline-on keresztül.
Az architektúra teljesen össze van kötve.

---

### 🎯 Mini kihívás — ChatHistory (STM előkészítés)

Implementálj többkörös beszélgetést `ChatHistory`-val.
Az előző válasz kerüljön be a következő kérésbe kontextusként.

```csharp
var history = new ChatHistory();
history.AddSystemMessage("You are a helpful .NET expert assistant.");

// Implementáld a chat loop-ot...
// Segítség: chatClient.CompleteAsync() fogad ChatHistory-t is
```

Ez az STM (Short-Term Memory) alapja — Modul 3-ban mélyítjük el.

---

## Modul 1 — Összefoglalás

```
M.E.AI    → IChatClient = provider-agnosztikus LLM kommunikáció
               + middleware pipeline (logging, telemetry, function invocation)
SK        → Kernel = agent konténer, Plugins, Memory, Orchestration
Rétegezés → provider csere = 1 sor az appsettings.json-ban
```

---

## Következő: Modul 2 — Tool Calling & Function Calling

> Lásd: `modul-02-toolcalling.md`
