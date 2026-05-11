# Modul 2 — Tool Calling & Function Calling

> **Környezet:** .NET 10 · Qwen2.5-7B · LlamaSharp / Ollama · M.E.AI · SK · MAF  
> **Előfeltétel:** `modul-00-environment-setup.md` + Modul 1 teljesítve  
> **Időbecslés:** ~90 perc  

> ⚠️ **Provider note:** Qwen2.5-7B-Instruct szükséges — tool calling megbízható
> működéséhez. Mindkét provider (LlamaSharp + Ollama) megfelelő.

---

## 2.1 Koncepció: Mi a tool calling?

```
1. Elküldjük a promptot + az elérhető tool-ok JSON schema leírását
2. Az LLM dönt: saját tudásból válaszol VAGY tool-t hív
3. Ha tool-t hív → strukturált JSON visszatér (tool neve + paraméterek)
4. A TE kódod végrehajtja a tool-t
5. Az eredményt visszaküldöd az LLM-nek
6. Az LLM szintetizálja a végső választ
```

---

## 2.2 Két szintaxis — melyik mikor?

Ez a modul **mindkét megközelítést** bemutatja, mert a .NET ökoszisztémában
mindkettővel találkozni fogsz:

```
SK  [KernelFunction]          →  plugin osztályok, DI-barát, komplex rendszerekhez
MAF AIFunctionFactory.Create() →  lightweight, M.E.AI natív, MAF agent-ekhez
```

A döntési logika:

```
Ha SK Kernel-t, RAG-ot, memory-t is használsz  →  SK [KernelFunction]
Ha MAF ChatClientAgent-et használsz             →  AIFunctionFactory.Create()
Ha mindkettő jelen van a projektben             →  mindkettő, rétegek szerint
```

---

## 2.3 SK megközelítés — `[KernelFunction]`

### Plugin definíció

```csharp
using Microsoft.SemanticKernel;
using System.ComponentModel;

public class WeatherPlugin
{
    // [KernelFunction] → tool-ként elérhető az LLM-nek
    // [Description]    → ez kerül a JSON schema-ba — legyen pontos!
    [KernelFunction]
    [Description("Gets the current weather for a given city. " +
                 "Use when the user asks about weather conditions.")]
    public string GetWeather(
        [Description("The city name, e.g. Budapest")] string city)
    {
        return city.ToLower() switch
        {
            "budapest" => "18°C, partly cloudy",
            "london"   => "12°C, rainy",
            _          => $"No weather data for {city}"
        };
    }
}
```

### Regisztrálás és hívás

```csharp
// DI-vel (ASP.NET Core / production)
builder.Services.AddScoped<WeatherPlugin>();
builder.Services.AddKernel().Plugins.AddFromType<WeatherPlugin>();

// Közvetlen (console / lab)
kernel.Plugins.AddFromType<WeatherPlugin>();

var result = await kernel.InvokePromptAsync(
    "Milyen az idő Budapesten?",
    new KernelArguments(new PromptExecutionSettings
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
    }));

Console.WriteLine(result);
```

### Production plugin — DI + hibakezelés

```csharp
public class OrderPlugin
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrderPlugin> _logger;

    // ✅ DI-vel kap függőségeket
    public OrderPlugin(IOrderService orderService, ILogger<OrderPlugin> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    [KernelFunction]
    [Description("Retrieves order details. Use when user asks about a specific order.")]
    public async Task<string> GetOrderAsync(
        [Description("Order ID in format ORD-XXXXX")] string orderId,
        CancellationToken cancellationToken = default)
    {
        // ✅ Input validáció
        if (!orderId.StartsWith("ORD-"))
            return "Invalid order ID format. Expected: ORD-XXXXX";

        try
        {
            _logger.LogInformation("Tool: GetOrder({OrderId})", orderId);
            var order = await _orderService.GetByIdAsync(orderId, cancellationToken);
            return order is null
                ? $"Order {orderId} not found."
                : $"Order {order.Id}: Status={order.Status}, Total={order.Total:C}";
        }
        catch (Exception ex)
        {
            // ✅ Soha ne dobjon kivételt — LLM-barát hiba string
            _logger.LogError(ex, "GetOrder failed for {OrderId}", orderId);
            return $"Unable to retrieve order {orderId}. Please try again.";
        }
    }
}
```

---

## 2.4 MAF megközelítés — `AIFunctionFactory.Create()`

Ez a MAF natív módszer — nincs szükség SK `Kernel`-re, csak `IChatClient`-re.

### Egyszerű function tool

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using System.ComponentModel;

// Sima C# metódus — description attribútummal
[Description("Gets the current weather for a city.")]
string GetWeather(
    [Description("City name, e.g. Budapest")] string city)
    => city.ToLower() switch
    {
        "budapest" => "18°C, partly cloudy",
        "london"   => "12°C, rainy",
        _          => $"No weather data for {city}"
    };

// Metódusból tool létrehozása
var weatherTool = AIFunctionFactory.Create(GetWeather);
```

### MAF ChatClientAgent + tool

```csharp
using AiLabs.Infrastructure;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json").Build();

var chatClient = ProviderFactory.CreateChatClient(config);

// Tool definíciók
var weatherTool = AIFunctionFactory.Create(
    ([Description("City name")] string city) =>
        city.ToLower() == "budapest" ? "18°C, partly cloudy" : "12°C, variable",
    "GetWeather",
    "Gets current weather for a city.");

var timeTool = AIFunctionFactory.Create(
    () => DateTime.Now.ToString("HH:mm, dddd"),
    "GetCurrentTime",
    "Returns the current local time and day of week.");

// MAF agent — Kernel nélkül, csak IChatClient
var agent = new ChatClientAgent(
    chatClient,
    instructions: "You are a helpful assistant with weather and time tools.",
    tools: [weatherTool, timeTool]);

// Hívás
var result = await agent.RunAsync(
    "Milyen az idő Budapesten és hány óra van?");

Console.WriteLine(result.Text);
```

### Async tool MAF-ban

```csharp
// Async metódus ugyanúgy működik
var orderTool = AIFunctionFactory.Create(
    async ([Description("Order ID, format ORD-XXXXX")] string orderId) =>
    {
        if (!orderId.StartsWith("ORD-"))
            return "Invalid order ID format.";

        // Szimulált async adatbázis hívás
        await Task.Delay(10);
        return $"Order {orderId}: Status=Shipped, Estimated delivery: tomorrow";
    },
    "GetOrderStatus",
    "Gets order status and delivery estimate.");
```

---

## 2.5 Auto vs. Manuális tool loop

Ez a döntés mindkét szintaxisra vonatkozik.

| | Auto | Manuális |
|---|---|---|
| **Fejlesztés / prototípus** | ✅ Gyors | ❌ Verbose |
| **Production** | ⚠️ Csak low-risk tool-nál | ✅ Ajánlott |
| **HITL checkpoint** | ❌ | ✅ Természetes pont |
| **Audit logging** | ❌ Nehéz | ✅ Teljes kontroll |

### Manuális loop — M.E.AI szinten (provider-agnosztikus)

```csharp
// Ez SK-val és MAF-fal is működik — tisztán M.E.AI szint
var history = new List<ChatMessage>
{
    new(ChatRole.System, "You are a helpful assistant."),
    new(ChatRole.User, "Milyen az idő Budapesten és Londonban?")
};

var tools = new List<AITool> { weatherTool, timeTool };

while (true)
{
    var response = await chatClient.GetResponseAsync(
        history, new ChatOptions { Tools = tools });

    history.Add(response.Message);

    if (response.FinishReason == ChatFinishReason.Stop)
    {
        Console.WriteLine(response.Text);
        break;
    }

    if (response.FinishReason == ChatFinishReason.ToolCalls)
    {
        foreach (var call in response.Message.Contents
            .OfType<FunctionCallContent>())
        {
            // ← KONTROLL PONT: itt tudsz logolni, validálni, HITL-t beiktatni
            Console.WriteLine($"🔧 Tool: {call.Name} | Args: {call.Arguments}");

            FunctionResultContent result;
            try
            {
                // Meghívjuk a megfelelő tool-t
                var tool = tools.OfType<AIFunction>()
                    .First(t => t.Name == call.Name);
                var output = await tool.InvokeAsync(call.Arguments ?? {});
                result = new FunctionResultContent(call.CallId, output);
            }
            catch (Exception ex)
            {
                result = new FunctionResultContent(call.CallId, $"Error: {ex.Message}");
            }

            history.Add(new ChatMessage(ChatRole.Tool, [result]));
        }
    }
}
```

---

## 2.6 SK vs MAF tool — összehasonlítás

```csharp
// ── SK megközelítés ──────────────────────────────────────────
public class CalculatorPlugin
{
    [KernelFunction]
    [Description("Adds two numbers together.")]
    public double Add(
        [Description("First number")] double a,
        [Description("Second number")] double b) => a + b;
}
kernel.Plugins.AddFromType<CalculatorPlugin>();

// ── MAF megközelítés ─────────────────────────────────────────
var addTool = AIFunctionFactory.Create(
    (double a, double b) => a + b,
    "Add",
    "Adds two numbers together.");
```

**Mikor SK `[KernelFunction]`:**
- A plugin osztály más service-eket is kap DI-ből (pl. `IOrderService`)
- Ugyanazt a plugin-t SK RAG/Memory pipeline-ban is használod
- Nagy, sok tool-t tartalmazó plugin rendszer

**Mikor MAF `AIFunctionFactory.Create()`:**
- MAF `ChatClientAgent`-et használsz
- Egyszerű, stateless tool-ok
- Nem kell DI a tool implementációhoz
- Prototípus, kis projekt

---

## 🧪 Hands-on Lab 2

### Projekt létrehozása

```bash
cd dotnet-ai-labs
dotnet new console -n Lab02.ToolCalling --framework net10.0
dotnet sln add Lab02.ToolCalling
cd Lab02.ToolCalling
dotnet add reference ../AiLabs.Infrastructure
dotnet add package Microsoft.SemanticKernel
dotnet add package Microsoft.Agents.AI --prerelease
```

Másold be az `appsettings.json`-t (lásd `modul-00-environment-setup.md`).

### Lab feladatok

**Feladat 1 — SK plugin**
Implementálj `CalculatorPlugin`-t `[KernelFunction]`-nel (4 alapművelet).
Teszteld SK `InvokePromptAsync`-cal: "Mennyi 347 osztva 13-mal?"

**Feladat 2 — MAF function tool**
Ugyanazt a kalkulátort implementáld `AIFunctionFactory.Create()`-tel.
Teszteld MAF `ChatClientAgent`-tel ugyanazzal a kérdéssel.
Hasonlítsd össze a két implementáció kódmennyiségét.

**Feladat 3 — Manuális tool loop**
Implementáld a manuális loop-ot M.E.AI szinten (2.5 fejezet).
Logold minden tool hívás előtt: `"⏳ [tool neve] - [paraméterek]"`

**Feladat 4 — Hibakezelés**
Nullával való osztásnál ne dobjon kivételt — adj vissza LLM-barát hibaüzenetet.
Teszteld: "Mennyi 10 osztva nullával?"

**Feladat 5 — Döntési gyakorlat (kihívás)**
Tervezz egy `OrderPlugin`-t ami `IOrderService`-t kap DI-ből.
Miért kell itt SK `[KernelFunction]` és miért nem elég `AIFunctionFactory.Create()`?
Írd le 3 mondatban az indoklást.

### 🛑 Checkpoint
Mindkét szintaxissal fut tool calling.
A manuális loop minden call-t logol.
A hibakezelés LLM-barát üzenetet ad vissza.

---

## Modul 2 — Összefoglalás

```
Tool calling alapja:  LLM + JSON schema → tool call döntés → végrehajtás → szintézis

SK [KernelFunction]:
  + DI-barát plugin osztályok
  + komplex rendszerekbe illik (RAG, Memory-val együtt)
  - több boilerplate

MAF AIFunctionFactory.Create():
  + lightweight, M.E.AI natív
  + ChatClientAgent-hez természetes
  - nem DI-barát stateful service-eknél

Manuális tool loop:  HITL, audit, production kontroll
Auto invocation:     prototípus, low-risk tool-ok
```

---

## Következő: Modul 3 — Memory Architektúra (STM / LTM)

> Lásd: `modul-03-memory.md`
