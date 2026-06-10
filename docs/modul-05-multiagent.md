# Modul 5 — Multi-Agent Orchesztráció (Fogalmi alap)

> **Környezet:** .NET 10 · Qwen2.5-7B · LlamaSharp / Ollama · Semantic Kernel Agents  
> **Előfeltétel:** `modul-00-environment-setup.md` + Modul 2 + Modul 3  
> **Időbecslés:** ~75 perc  

> ℹ️ **Modul célja:** A multi-agent fogalmak és pattern-ek megértése SK-on keresztül,
> majd az SK korlátainak felismerése — ami **motiválja a MAF-ra váltást (Modul 5.5)**.
> Ez a modul tudatosan rövidebb: a production multi-agent tudás Modul 5.5-ben van.

---

## 5.1 Koncepció: Miért több agent?

```
Single agent korlátai:
  ❌ "Csinálj mindent" system prompt → konfúzus viselkedés
  ❌ Nincs belső ellenőrzés (ki reviewolja a saját outputját?)
  ❌ Context window hamar megtelik komplex workflow-nál
  ❌ Nem skálázható: új képesség = refactor mindenhol

Multi-agent előnyök:
  ✅ Specializáció: minden agent egy dologban kiváló
  ✅ Ellenőrzés: Critic/Reviewer agent reviewolja a másik outputját
  ✅ Párhuzamosság: több agent egyszerre futhat
  ✅ Skálázhatóság: új képesség = új agent
```

---

## 5.2 A három fő orchestration pattern

Ezek a pattern-ek **framework-független koncepciók** — SK-ban és MAF-ban is jelen vannak.

### Orchestration (központi vezérlő)

```
Router Agent
    ├──► Researcher Agent
    ├──► Coder Agent
    └──► Reviewer Agent

Mikor: ismert, fix lépések sorrendje, determinisztikus workflow
Pro:   könnyen debug-olható, tesztelhető
Con:   Router bottleneck, kevésbé adaptív
```

### Choreography (eseményvezérelt)

```
Agent A ──► Event Bus ──► Agent B ──► Event Bus ──► Agent C

Mikor: laza csatolás, aszinkron feldolgozás, skálázhatóság
Pro:   rugalmas, agent-ek cserélhetők
Con:   nehezebb trace-elni, debug-olni
```

### Handoff (specialista delegálás)

```
User ──► Router ──► [code]         ──► Coder Agent
                ──► [architecture] ──► Architect Agent
                ──► [debug]        ──► Debug Agent

Mikor: routing, triage, specialista hálózat
Pro:   egyszerű, intuitív
Con:   Router egyetlen hibapontja az egésznek
```

---

## 5.3 SK implementáció — `AgentGroupChat`

Az SK `AgentGroupChat` **referencia implementáció** — megmutatja hogyan valósítható
meg az orchestration pattern kódban, de Modul 5.5-ben látni fogjuk miért nem ez
az optimális production path.

```csharp
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;

// Agent definíciók
var researcherAgent = new ChatCompletionAgent
{
    Name = "Researcher",
    Instructions = "Research topics thoroughly. End with: RESEARCH COMPLETE",
    Kernel = kernel
};

var coderAgent = new ChatCompletionAgent
{
    Name = "Coder",
    Instructions = "Write production C# based on research. End with: CODE COMPLETE",
    Kernel = kernel
};

var reviewerAgent = new ChatCompletionAgent
{
    Name = "Reviewer",
    Instructions = "Review code quality. End with APPROVED or NEEDS REVISION: [reason]",
    Kernel = kernel
};

// Mikor álljon le a group chat?
var terminationStrategy = new KernelFunctionTerminationStrategy(
    kernel.CreateFunctionFromPrompt("""
        Is the task complete? Complete = Reviewer said APPROVED.
        Reply YES or NO only. History: {{$history}}
        """),
    kernel)
{
    Agents = [reviewerAgent],
    MaximumIterations = 8,
    ResultParser = r => r.GetValue<string>()
        ?.Contains("YES", StringComparison.OrdinalIgnoreCase) ?? false
};

// Ki következik?
var selectionStrategy = new KernelFunctionSelectionStrategy(
    kernel.CreateFunctionFromPrompt("""
        Select next: Researcher, Coder, Reviewer
        - No research → Researcher
        - Research done, no code → Coder
        - Code exists → Reviewer
        - NEEDS REVISION → Coder
        Reply ONLY the agent name. History: {{$history}}
        """),
    kernel);

var groupChat = new AgentGroupChat(researcherAgent, coderAgent, reviewerAgent)
{
    ExecutionSettings = new AgentGroupChatSettings
    {
        TerminationStrategy = terminationStrategy,
        SelectionStrategy = selectionStrategy
    }
};

groupChat.AddChatMessage(new ChatMessageContent(
    AuthorRole.User, "Create a production-ready rate limiter in C#"));

await foreach (var response in groupChat.InvokeAsync())
    Console.WriteLine($"[{response.AuthorName}]: {response.Content}\n");
```

---

## 5.4 Handoff pattern — Router agent

```csharp
public class RouterAgent
{
    private readonly IChatClient _chatClient;
    private readonly Dictionary<string, ChatCompletionAgent> _specialists;

    public RouterAgent(IChatClient chatClient, Kernel kernel)
    {
        _chatClient = chatClient;
        _specialists = new()
        {
            ["code"]         = new ChatCompletionAgent
                { Name = "Coder",    Instructions = "Expert C# developer.",  Kernel = kernel },
            ["architecture"] = new ChatCompletionAgent
                { Name = "Architect", Instructions = "Software architect.",  Kernel = kernel },
            ["debug"]        = new ChatCompletionAgent
                { Name = "Debugger", Instructions = "Bug diagnostician.",    Kernel = kernel }
        };
    }

    public async Task<string> RouteAsync(string userInput)
    {
        var category = (await _chatClient.GetResponseAsync(
            $"Classify into exactly one: code, architecture, debug.\n\"{userInput}\"\nReply ONLY the category."))
            .Text?.Trim().ToLower() ?? "code";

        Console.WriteLine($"🔀 → {category}");

        var specialist = _specialists.GetValueOrDefault(category) ?? _specialists["code"];
        var sb = new System.Text.StringBuilder();
        await foreach (var msg in specialist.InvokeAsync(userInput, new AgentThread()))
            sb.AppendLine(msg.Content);
        return sb.ToString();
    }
}
```

---

## 5.5 Az SK AgentGroupChat korlátai — átvezetés MAF-ra

Miután kipróbáltad a labban, vizsgáljuk meg mi hiányzik:

```
SK AgentGroupChat:
  ❌ State implicit — csak a ChatHistory, nincs typed state object
  ❌ Routing LLM-vezérelt → nem determinisztikus, nehéz tesztelni
  ❌ Nincs checkpointing → ha megáll a process, elvész minden
  ❌ HITL = manuális implementáció szükséges
  ❌ Nincs beépített workflow vizualizáció / observability

MAF Workflow (Modul 5.5):
  ✅ Explicit typed state — minden agent ugyanazt az objektumot olvas/írja
  ✅ Type-based routing — determinisztikus, tesztelhető
  ✅ Checkpointing — built-in pause/resume
  ✅ HITL — built-in workflow suspend + approval
  ✅ DurableTask integráció — Azure Functions hosting
```

### SK → MAF fogalmi megfelelések

| SK fogalom | MAF megfelelő |
|---|---|
| `ChatCompletionAgent` | `ChatClientAgent` (M.E.AI natív) |
| `AgentGroupChat` | `AgentWorkflow` (graph-based) |
| `SelectionStrategy` | `AddEdge` / `AddConditionalEdge` |
| `TerminationStrategy` | graph `__end__` node |
| `ChatHistory` (implicit state) | typed `State` record (explicit) |
| Filters | Agent Filters / IChatClient middleware |

---

## 🧪 Hands-on Lab 5

### Projekt létrehozása

```bash
cd dotnet-ai-labs
dotnet new console -n Lab05.MultiAgent --framework net10.0
dotnet sln add Lab05.MultiAgent
cd Lab05.MultiAgent
dotnet add reference ../AiLabs.Infrastructure
dotnet add package Microsoft.SemanticKernel.Agents.Core
```

### Lab feladatok

**Feladat 1 — Két agent párbeszéde**
Writer és Editor agent — Writer ír egy paragrafust, Editor javítja.
`AgentGroupChat` 3 iterációval. Figyeld meg az iteráció menetét.

**Feladat 2 — Router implementáció**
Implementáld a `RouterAgent`-et 3 specialistával.
5 különböző kérdés — mindegyik a helyes specialistához kerül?

**Feladat 3 — Termination strategy vizsgálat**
Módosítsd a termination strategy-t: álljon le ha a Reviewer "APPROVED"-ot mond,
VAGY elérte a 6 iterációt. Teszteld mindkét ágat.

**Feladat 4 — Reflekció (nem kód)**
Futtasd a Feladat 1-es lab-ot háromszor ugyanazzal a kérdéssel.
Kapsz-e minden futásban ugyanolyan eredményt? Miért igen/nem?
Ez a megfigyelés fogja megmagyarázni a MAF type-based routing előnyét.

### 🛑 Checkpoint
`AgentGroupChat` lefut és helyes terminálással áll meg.
Router agent megfelelő specialistának delegál.
A reflekciós feladatból világos miért nem deterministikus az SK selection.

---

## Modul 5 — Összefoglalás

```
Orchestration, Choreography, Handoff  →  framework-független fogalmak
SK AgentGroupChat                     →  referencia implementáció
                                          jó prototípushoz, emergent behavior-hoz

SK korlátai production szempontból:
  - implicit state, LLM-vezérelt routing, nincs checkpointing, nincs built-in HITL

→ Ezek a korlátok motiválják a MAF-ra váltást
```

---

## Következő: Modul 5.5 — Microsoft Agentic Framework (MAF) ⭐

> Lásd: `modul-055-maf.md`

Most, hogy érted a multi-agent fogalmakat és az SK korlátait,
a MAF-ban minden korábbi probléma megoldott formában jelenik meg.
