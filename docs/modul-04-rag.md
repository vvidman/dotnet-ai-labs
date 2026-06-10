# Modul 4 — RAG → Agentic RAG

> **Környezet:** .NET 10 · Qwen2.5-7B · LlamaSharp / Ollama · Semantic Kernel · Qdrant  
> **Előfeltétel:** `modul-00-environment-setup.md` + Modul 3 teljesítve  
> **Időbecslés:** ~90 perc  

---

## 4.1 Koncepció: Miért törik el a hagyományos RAG?

```
Traditional RAG:
  User → Embed(kérdés) → Top-K chunk → Inject → LLM → Válasz  (1 lépés)

Problémák komplex kérdéseknél:
  ❌ Multi-hop: több különböző dokumentumból kell info
  ❌ Az első retrieval eredménye nem elég
  ❌ "Összehasonlítsd X-et és Y-t" → csak X-ről kap chunk-ot
```

```
Agentic RAG:
  User → Agent Loop:
    ├── Query elemzés + rewriting
    ├── Retrieval (akár többször, különböző query-kkel)
    ├── Elégséges az info? → ha nem: újabb retrieval
    └── Szintézis → Válasz
```

---

## 4.2 Query Rewriter

```csharp
public class QueryRewriter
{
    private readonly IChatClient _chatClient;
    public QueryRewriter(IChatClient chatClient) => _chatClient = chatClient;

    public async Task<List<string>> RewriteAsync(string userQuestion)
    {
        var prompt = $"""
            Generate 2-3 specific search queries to retrieve information
            needed to answer this question. Return ONLY a JSON array of strings.
            Example: ["query 1", "query 2"]

            Question: {userQuestion}
            """;

        var response = await _chatClient.CompleteAsync(prompt);
        var json = response.Message.Text?.Trim() ?? "[]";

        try { return JsonSerializer.Deserialize<List<string>>(json) ?? [userQuestion]; }
        catch { return [userQuestion]; }
    }
}
```

---

## 4.3 Active Retrieval Loop

```csharp
public class AgenticRagAgent
{
    private readonly ISemanticTextMemory _memory;
    private readonly IChatClient _chatClient;
    private readonly QueryRewriter _queryRewriter;
    private const string Collection = "knowledge-base";
    private const int MaxIterations = 3;

    public AgenticRagAgent(ISemanticTextMemory memory, IChatClient chatClient)
    {
        _memory = memory;
        _chatClient = chatClient;
        _queryRewriter = new QueryRewriter(chatClient);
    }

    public async Task<string> AnswerAsync(string userQuestion)
    {
        var collectedContext = new List<string>();

        for (int iteration = 1; iteration <= MaxIterations; iteration++)
        {
            Console.WriteLine($"\n📍 Iteration {iteration}/{MaxIterations}");

            var queries = iteration == 1
                ? await _queryRewriter.RewriteAsync(userQuestion)
                : await _queryRewriter.RewriteAsync(
                    await IdentifyGapsAsync(userQuestion, collectedContext));

            foreach (var query in queries)
            {
                Console.WriteLine($"  🔍 {query}");
                await foreach (var r in _memory.SearchAsync(
                    Collection, query, limit: 2, minRelevanceScore: 0.65))
                {
                    if (!collectedContext.Contains(r.Metadata.Text))
                    {
                        collectedContext.Add(r.Metadata.Text);
                        Console.WriteLine($"  ✅ [{r.Relevance:F2}] found");
                    }
                }
            }

            if (await IsSufficientAsync(userQuestion, collectedContext)) break;
        }

        return await SynthesizeAsync(userQuestion, collectedContext);
    }

    private async Task<bool> IsSufficientAsync(string question, List<string> context)
    {
        if (context.Count == 0) return false;
        var prompt = $"""
            Question: "{question}"
            Context: {string.Join("\n---\n", context)}
            Is there sufficient information to answer completely? Reply YES or NO only.
            """;
        var r = await _chatClient.CompleteAsync(prompt);
        return r.Message.Text?.Trim().StartsWith("YES", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private async Task<string> IdentifyGapsAsync(string question, List<string> context)
    {
        var prompt = $"""
            Question: "{question}"
            Collected so far: {string.Join("\n---\n", context)}
            What specific information is STILL MISSING? Write a search query only.
            """;
        var r = await _chatClient.CompleteAsync(prompt);
        return r.Message.Text?.Trim() ?? question;
    }

    private async Task<string> SynthesizeAsync(string question, List<string> context)
    {
        Console.WriteLine($"\n✨ Synthesizing from {context.Count} chunks...");
        var prompt = $"""
            Using ONLY the following context, answer the question.
            Context: {string.Join("\n---\n", context)}
            Question: {question}
            """;
        var r = await _chatClient.CompleteAsync(prompt);
        return r.Message.Text ?? "Unable to generate answer.";
    }
}
```

---

## 🧪 Hands-on Lab 4

### Projekt létrehozása

```bash
cd dotnet-ai-labs
dotnet new console -n Lab04.AgenticRAG --framework net10.0
dotnet sln add Lab04.AgenticRAG
cd Lab04.AgenticRAG
dotnet add reference ../AiLabs.Infrastructure
dotnet add package Microsoft.SemanticKernel.Connectors.Qdrant
dotnet add package Microsoft.SemanticKernel.Plugins.Memory
```

### Lab feladatok

**Feladat 1 — Tudásbázis feltöltése**
Tölts fel 20+ dokumentumot Qdrant-ba valós tartalommal.

**Feladat 2 — Query rewriter tesztelése**
3 komplex kérdésből milyen sub-query-ket generál?

**Feladat 3 — Iteráció megfigyelése**
Multi-hop kérdés: hány iteráció kellett, mik voltak a query-k?

**Feladat 4 — Traditional vs Agentic (kihívás)**
Találj kérdést ahol Traditional RAG rossz, Agentic RAG jó választ ad.

### 🛑 Checkpoint
Az agent több iterációban gyűjt kontextust,
szintézis koherens választ ad.

---

## Modul 4 — Összefoglalás

```
Traditional RAG  →  1 query → 1 retrieval → 1 LLM hívás
Agentic RAG      →  Query rewriting → iteratív retrieval → gap detection → szintézis
```

---

## Következő: Modul 5 — Multi-Agent Orchesztráció

> Lásd: `modul-05-multiagent.md`
