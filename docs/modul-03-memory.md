# Modul 3 — Memory Architektúra (STM / LTM)

> **Környezet:** .NET 10 · Qwen2.5-7B · LlamaSharp / Ollama · Semantic Kernel · Qdrant  
> **Előfeltétel:** `modul-00-environment-setup.md` + Modul 2 teljesítve · Qdrant fut (`docker ps`)  
> **Időbecslés:** ~90 perc  

---

## 3.1 Koncepció: Kétféle memória

```
STM (Short-Term Memory)  →  ChatHistory
  "Amit most éppen beszéltünk" — gyors, korlátolt, session-specifikus

LTM (Long-Term Memory)   →  Vector Database (Qdrant)
  "Amit korábban megtanultunk" — lassabb keresés, korlátlan, perzisztens
```

```
┌──────────────────────────────────────────────┐
│                  AGENT                        │
│  User input                                  │
│      │                                       │
│      ▼                                       │
│  ┌─────────┐    ┌──────────────────────┐    │
│  │   STM   │    │         LTM          │    │
│  │ChatHist.│    │  Qdrant Vector DB    │    │
│  │ ~4096   │    │  Embed→Search→Inject │    │
│  │ tokens  │    └──────────┬───────────┘    │
│  └────┬────┘               │                │
│       └──────────┬─────────┘                │
│                  ▼                          │
│              LLM hívás                      │
└──────────────────────────────────────────────┘
```

---

## 3.2 STM — ChatHistory és context window management

```csharp
var history = new ChatHistory();
history.AddSystemMessage("You are a helpful .NET expert.");

while (true)
{
    Console.Write("\nTe: ");
    var input = Console.ReadLine();
    if (string.IsNullOrEmpty(input)) break;

    history.AddUserMessage(input);
    var response = await chatClient.CompleteAsync(history);
    history.AddAssistantMessage(response.Message.Text!);

    Console.WriteLine($"🤖 {response.Message.Text}");
}
```

### Context window management stratégiák

```csharp
public static class ChatHistoryExtensions
{
    // Stratégia 1: Sliding window
    public static void TrimToTokenLimit(
        this ChatHistory history,
        int maxTokens = 3000,
        int reserveForResponse = 500)
    {
        var limit = maxTokens - reserveForResponse;
        var systemMessages = history.Where(m => m.Role == ChatRole.System).ToList();
        var conversationMessages = history.Where(m => m.Role != ChatRole.System).ToList();

        int EstimateTokens(ChatMessage m) =>
            (m.Content?.Length ?? 0) / 4;  // ~4 char = 1 token

        int systemTokens = systemMessages.Sum(EstimateTokens);
        int budget = limit - systemTokens;
        int used = 0;

        var kept = conversationMessages
            .AsEnumerable().Reverse()
            .TakeWhile(m => (used += EstimateTokens(m)) <= budget)
            .Reverse().ToList();

        history.Clear();
        foreach (var m in systemMessages.Concat(kept)) history.Add(m);
    }
}
```

---

## 3.3 LTM — Qdrant + Embedding

### Qdrant setup (lásd modul-00)

```bash
docker run -d --name qdrant -p 6333:6333 qdrant/qdrant
```

### NuGet (lab projekthez)

```bash
dotnet add package Microsoft.SemanticKernel.Connectors.Qdrant
dotnet add package Microsoft.SemanticKernel.Plugins.Memory
```

### Embedding + memória feltöltése

```csharp
using AiLabs.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;

var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
var embeddingGenerator = ProviderFactory.CreateEmbeddingGenerator(config);

var memoryStore = new QdrantMemoryStore("http://localhost", 6333, vectorSize: 768);
var memory = new SemanticTextMemory(memoryStore, embeddingGenerator);

const string Collection = "dotnet-knowledge";

var items = new[]
{
    ("sk-001", "Semantic Kernel",
     "Semantic Kernel is an open-source .NET framework for building AI agents."),
    ("sk-002", "KernelFunction",
     "KernelFunction marks methods as AI-callable tools in Semantic Kernel."),
    ("sk-003", "ChatHistory",
     "ChatHistory represents the conversation context (STM) in Semantic Kernel."),
};

foreach (var (id, title, content) in items)
{
    await memory.SaveInformationAsync(Collection, content, id, title);
    Console.WriteLine($"✅ Saved: {title}");
}

// Keresés
var query = "Hogyan jelölöm meg egy metódust tool-ként?";
var results = memory.SearchAsync(Collection, query, limit: 2, minRelevanceScore: 0.7);

await foreach (var r in results)
    Console.WriteLine($"[{r.Relevance:F2}] {r.Metadata.Description}");
```

---

## 3.4 STM + LTM együtt — a teljes memory pipeline

```csharp
public class AgentWithMemory
{
    private readonly IChatClient _chatClient;
    private readonly ISemanticTextMemory _memory;
    private readonly ChatHistory _history;
    private const string Collection = "agent-knowledge";

    public AgentWithMemory(IChatClient chatClient, ISemanticTextMemory memory)
    {
        _chatClient = chatClient;
        _memory = memory;
        _history = new ChatHistory();
        _history.AddSystemMessage(
            "You are a helpful .NET expert. Use the provided context to answer questions.");
    }

    public async Task<string> ChatAsync(string userInput)
    {
        // 1. LTM: keresünk a tudásbázisban
        var relevantMemories = new List<string>();
        await foreach (var r in _memory.SearchAsync(Collection, userInput,
            limit: 3, minRelevanceScore: 0.6))
            relevantMemories.Add(r.Metadata.Text);

        // 2. Releváns kontextus injektálása
        var augmentedInput = relevantMemories.Count > 0
            ? $"Context:\n{string.Join("\n---\n", relevantMemories)}\n\nQuestion: {userInput}"
            : userInput;

        // 3. STM frissítés
        _history.AddUserMessage(augmentedInput);
        _history.TrimToTokenLimit(maxTokens: 3500);

        // 4. LLM hívás
        var response = await _chatClient.CompleteAsync(_history);
        _history.AddAssistantMessage(response.Message.Text!);

        return response.Message.Text!;
    }
}
```

---

## 🧪 Hands-on Lab 3

### Projekt létrehozása

```bash
cd dotnet-ai-labs
dotnet new console -n Lab03.Memory --framework net10.0
dotnet sln add Lab03.Memory
cd Lab03.Memory
dotnet add reference ../AiLabs.Infrastructure
dotnet add package Microsoft.SemanticKernel.Connectors.Qdrant
dotnet add package Microsoft.SemanticKernel.Plugins.Memory
```

### Lab feladatok

**Feladat 1 — Sliding window**
Implementáld a `TrimToTokenLimit` extension metódust.
Teszteld 20+ körös beszélgetéssel.

**Feladat 2 — Qdrant feltöltés**
Tölts fel 10 saját tartalmú knowledge item-et
(pl. .NET fogalmak, projekt dokumentáció).

**Feladat 3 — Semantic search**
Írj 5 különböző kérdést — figyeld a relevance score-okat.
Mikor 0.9 feletti? Mikor 0.5 alatti?

**Feladat 4 — Teljes pipeline (kihívás)**
Implementáld az `AgentWithMemory` osztályt.
Kérdezz olyat, ami csak a Qdrant-ban van, az LLM tudásában nem.

### 🛑 Checkpoint
Az agent Qdrant-ból kiegészített kontextussal válaszol,
a ChatHistory nem nő korlátlanul.

---

## Modul 3 — Összefoglalás

```
STM  →  ChatHistory: session memória, context window korláttal
         Kezelés: sliding window VAGY összefoglalás
LTM  →  Qdrant: perzisztens, szemantikus keresés
         Pipeline: szöveg → embedding → tárolás → keresés → inject
```

---

## Következő: Modul 4 — RAG → Agentic RAG

> Lásd: `modul-04-rag.md`
