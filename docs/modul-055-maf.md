# Module 5.5 — Microsoft Agentic Framework (MAF)

> **Environment:** .NET 10 · Ollama + qwen2.5:7b (recommended) · MAF v1.0  
> **Prerequisite:** `modul-00-environment-setup.md` + Module 5 completed  
> **Estimated time:** ~120 minutes  

> ⚠️ **Provider note:** Ollama is recommended for this module.
> MAF requires an OpenAI-compatible API for reliable tool calling.
> Set `"Provider": "Ollama"` in `appsettings.json` before starting.

> ⚠️ **Emerging Framework Notice**
> MAF v1.0 was released April 2026. Verify package names at:
> https://learn.microsoft.com/en-us/agent-framework/overview/

---

## 5.5.1 Why MAF Exists — The Two Legacies Problem

```
AutoGen                       Semantic Kernel
──────────────────────────    ──────────────────────────────
✅ Multi-agent collaboration  ✅ Enterprise robustness
✅ Conversation-first model   ✅ Middleware, telemetry, MCP
✅ Simple Group Chat          ✅ Type-safety, DI, security
❌ Lacks enterprise hardening ❌ More verbose multi-agent setup

MAF = AutoGen patterns + SK enterprise features
→ The official Microsoft successor for .NET/Azure
```

---

## 5.5.2 MAF vs SK AgentGroupChat

| Aspect | SK `AgentGroupChat` | MAF `Workflow` |
|---|---|---|
| **Model** | Conversation loop | Graph-based execution |
| **State** | Implicit (ChatHistory) | Explicit, typed |
| **Routing** | LLM-driven | Type-based, deterministic |
| **Checkpointing** | ❌ | ✅ Built-in |
| **HITL** | Manual | ✅ Built-in |
| **Best for** | Prototyping | Production |

---

## 5.5.3 Project Setup

```bash
cd dotnet-ai-labs
dotnet new console -n Lab055.MAF --framework net10.0
dotnet sln add Lab055.MAF
cd Lab055.MAF
dotnet add reference ../AiLabs.Infrastructure

# Verify current package names at nuget.org
dotnet add package Microsoft.Agents.AI --prerelease
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
dotnet add package OllamaSharp
```

### appsettings.json

```json
{
  "Provider": "Ollama",
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ChatModel": "qwen2.5:7b"
  }
}
```

### Agent setup with Ollama

```csharp
using AiLabs.Infrastructure;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OllamaSharp;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json").Build();

// Ollama provides OpenAI-compatible IChatClient → MAF compatible
var chatClient = ProviderFactory.CreateChatClient(config);

// MAF ChatClientAgent wraps any IChatClient
var agent = new ChatClientAgent(
    chatClient,
    instructions: "You are a helpful .NET expert assistant.");
```

---

## 5.5.4 Typed State + Graph-Based Workflow

```csharp
// Typed state — the shared scratchpad
public record ContentCreationState
{
    public string Topic { get; init; } = string.Empty;
    public string? ResearchNotes { get; init; }
    public string? Draft { get; init; }
    public string? ReviewFeedback { get; init; }
    public bool IsApproved { get; init; }
    public int RevisionCount { get; init; }
}

// Build the workflow graph
var workflow = new AgentWorkflowBuilder<ContentCreationState>()

    .AddAgentNode("research", researcherAgent,
        inputMapper:  state => $"Research this topic: {state.Topic}",
        outputMapper: (state, r) => state with { ResearchNotes = r })

    .AddAgentNode("write", writerAgent,
        inputMapper:  state => $"Write based on:\n{state.ResearchNotes}",
        outputMapper: (state, r) => state with { Draft = r })

    .AddAgentNode("review", reviewerAgent,
        inputMapper:  state => $"Review this draft:\n{state.Draft}",
        outputMapper: (state, r) => state with
        {
            ReviewFeedback = r,
            IsApproved = r.Contains("APPROVED", StringComparison.OrdinalIgnoreCase),
            RevisionCount = state.RevisionCount + (r.Contains("APPROVED") ? 0 : 1)
        })

    // Deterministic edges — not LLM-driven
    .AddEdge("research", "write")
    .AddConditionalEdge("review",
        condition: state => state.IsApproved || state.RevisionCount >= 2,
        trueNode:  "__end__",
        falseNode: "write")

    .WithEntryPoint("research")
    .Build();

var finalState = await workflow.RunAsync(
    new ContentCreationState { Topic = "MAF graph-based workflows" });

Console.WriteLine($"Approved: {finalState.IsApproved}");
Console.WriteLine($"Draft:\n{finalState.Draft}");
```

---

## 5.5.5 Checkpointing

```csharp
var checkpointStore = new InMemoryWorkflowCheckpointStore();
// Production: CosmosDbWorkflowCheckpointStore / SqlWorkflowCheckpointStore

var workflow = new AgentWorkflowBuilder<ContentCreationState>()
    // ... nodes and edges ...
    .WithCheckpointing(checkpointStore)
    .Build();

var threadId = $"job-{Guid.NewGuid()}";

// Run — workflow state is checkpointed at every node
var finalState = await workflow.RunAsync(initialState, threadId: threadId);

// Resume after crash — continues from last checkpoint
var resumedState = await workflow.ResumeAsync(threadId);
```

---

## 5.5.6 Built-in HITL

```csharp
var workflow = new AgentWorkflowBuilder<ContentCreationState>()

    .AddAgentNode("research", researcherAgent,
        inputMapper:  state => $"Research: {state.Topic}",
        outputMapper: (state, r) => state with { ResearchNotes = r })

    // Workflow PAUSES here and waits for human approval
    .AddHumanApprovalNode("approve-research",
        prompt: state =>
            $"Research complete:\n{state.ResearchNotes}\n\nApprove? (yes/no)",
        onApproved: state => state,
        onRejected: state => state with
        {
            Topic = state.Topic + " [REJECTED — be more thorough]",
            ResearchNotes = null
        })

    .AddAgentNode("write", writerAgent,
        inputMapper:  state => $"Write based on:\n{state.ResearchNotes}",
        outputMapper: (state, r) => state with { Draft = r })

    .AddEdge("research", "approve-research")
    .AddConditionalEdge("approve-research",
        condition:  state => state.ResearchNotes != null,
        trueNode:  "write",
        falseNode: "research")

    .WithCheckpointing(checkpointStore)
    .Build();

// Start — suspends at HITL checkpoint
var thread = await workflow.StartAsync(initialState, threadId: "job-001");
Console.WriteLine($"Status: {thread.Status}");
// → WaitingForHumanApproval

// Later: submit human approval
await workflow.SubmitApprovalAsync("job-001", "approve-research", approved: true);
// Workflow resumes
```

---

## 🧪 Hands-on Lab 5.5

### Lab Tasks

**Task 1 — Define typed state**
Create `CodeReviewState`: `OriginalCode`, `ReviewFeedback`, `RevisedCode`,
`IsApproved`, `RevisionCount`.

**Task 2 — Sequential workflow**
3-node workflow: `CodeReviewer` → `CodeRefactorer` → `TestWriter`.
Run it with a sample C# snippet.

**Task 3 — Conditional edge**
After `CodeReviewer`: if no issues → skip to `TestWriter`, else → `CodeRefactorer`.

**Task 4 — Audit Filter**
Implement an agent invocation filter: log agent name, input length, execution time.

**Task 5 — Checkpointing (challenge)**
Add in-memory checkpointing. Simulate a crash (random exception in `CodeRefactorer`).
Verify the workflow resumes from the correct checkpoint.

### 🛑 Checkpoint
Workflow runs end-to-end with typed state.
Conditional edge routes correctly to both paths.
Audit filter fires for every node.
Checkpointing resumes after simulated failure.

---

## Module 5.5 — Summary

```
MAF = AutoGen (collaboration) + SK (enterprise) → official Microsoft successor

Core Concepts:
  ChatClientAgent   →  wraps any IChatClient (including Ollama)
  Graph Workflow    →  typed State + Nodes + deterministic Edges
  Checkpointing     →  pause/resume long-running workflows
  Built-in HITL     →  workflow suspends, waits for approval, resumes
  Filters           →  middleware for logging, auth, safety

SK GroupChat vs MAF Workflow:
  GroupChat  →  prototyping, emergent, conversation-first
  Workflow   →  production, deterministic, checkpointable

Provider: Ollama required for reliable tool calling in MAF
```

---

## Next: Module 6 — MCP (Model Context Protocol)

> See: `modul-06-mcp.md`
