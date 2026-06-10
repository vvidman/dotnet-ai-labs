# .NET Agentic AI — Technológiai Curriculum

> **Cél:** Technológia-centrikus, hordozható tudás .NET környezetben.  
> **Megközelítés:** Minden modulban — Koncepció → Architektúra → Production kód  
> **Környezet:** .NET 10 · Qwen2.5-7B · LlamaSharp / Ollama · M.E.AI · MAF · SK  

---

## Stratégiai irány: MAF-first

```
Microsoft hivatalos pozíció (2026):
  MAF = SK + AutoGen utódja — ez az új befektetés iránya
  SK  = fenntartott, de új feature fejlesztés MAF-ban történik
  MAF = M.E.AI-ra épül közvetlenül (nem SK-ra)
```

**A curriculum ezt tükrözi:**

- **M.E.AI** — alap, nem megkerülhető. MAF erre épül.
- **MAF** — elsődleges agent és orchestration framework (Modul 2-től)
- **SK** — csak ahol MAF-nak nincs alternatívája: RAG, Vector DB, Embedding (Modul 3–4)
- **SK AgentGroupChat** — fogalmi kontextus, de nem production path

```
┌─────────────────────────────────────────────┐
│      A TE KÓDOD                             │
├───────────────────────┬─────────────────────┤
│         MAF           │    SK (ahol kell)   │
│  agent orchestration  │  RAG · Memory · VDB │
├───────────────────────┴─────────────────────┤
│         MICROSOFT.EXTENSIONS.AI             │
│    IChatClient — mindkettő erre épül        │
├─────────────────────────────────────────────┤
│         LLM PROVIDER (cserélhető)           │
│  LlamaSharp │ Ollama │ Azure OpenAI │ ...   │
└─────────────────────────────────────────────┘
```

---

## Modul áttekintés

| # | Modul | Fő framework | Kulcstechnológiák | Státusz |
|---|-------|-------------|-------------------|---------|
| 0 | Lab Environment Setup | M.E.AI | LlamaSharp · Ollama · Qwen2.5 · ProviderFactory | 🏗️ Előfeltétel |
| 1 | A .NET AI Stack rétegei | **M.E.AI** | IChatClient · Middleware pipeline · SK Kernel | ✅ Megkezdve |
| 2 | Tool Calling & Function Calling | **M.E.AI + MAF** | AIFunctionFactory · [KernelFunction] · Auto/Manual loop | 📋 Tervezett |
| 3 | Memory Architektúra (STM/LTM) | **SK** | ChatHistory · Qdrant · Embedding | 📋 Tervezett |
| 4 | RAG → Agentic RAG | **SK** | Query rewriting · Active retrieval loop | 📋 Tervezett |
| 5 | Multi-Agent — Fogalmi alap | **SK → MAF** | AgentGroupChat (kontextus) · átvezetés MAF-ra | 📋 Tervezett |
| 5.5 | Microsoft Agentic Framework | **MAF** ⭐ | Workflows · Checkpointing · HITL · Filters | 📋 Tervezett |
| 6 | MCP (Model Context Protocol) | **MAF + M.E.AI** | MCP Server · MCP Client · SK integráció | 📋 Tervezett |
| 7 | Biztonság, HITL, Guardrails | **MAF + M.E.AI** | Least Privilege · HITL · Prompt injection · Audit | 📋 Tervezett |

> ⭐ **Modul 5.5 a curriculum csúcspontja** — minden korábbi modul ide vezet.

---

## Miért ez a sorrend?

```
Modul 0–1:  M.E.AI primitívek — IChatClient, middleware, provider-agnosztikus kód
             → MAF erre épül, megkerülhetetlen alap

Modul 2:    Tool calling — SK [KernelFunction] ÉS MAF AIFunctionFactory
             → mindkét szintaxis ismerős kell legyen

Modul 3–4:  SK Memory + RAG — SK-ban van, MAF-ban nincs
             → tudás hordozható, de SK-specifikus

Modul 5:    SK AgentGroupChat — fogalmi megértés, manuális orchestration
             → nem production path, de a "miért jobb a MAF?" kérdés itt válik érthetővé

Modul 5.5:  MAF — production agent rendszerek
             → graph workflow, checkpointing, built-in HITL, A2A, MCP

Modul 6–7:  MCP + Biztonság — MAF-kompatibilis, M.E.AI middleware alapon
```

---

## Modul 0 — Lab Environment Setup

**Fájl:** `modul-00-environment-setup.md` | **Projekt:** `AiLabs.Infrastructure`

- LlamaSharp + Ollama dual-provider, Qwen2.5-7B, nomic-embed-text
- `ProviderFactory`, `ServiceCollectionExtensions`, `appsettings.json` sablon
- Provider váltás = 1 sor konfig változás

---

## Modul 1 — A .NET AI Stack rétegei

**Fájl:** `modul-01-stack.md` | **Lab:** `Lab01.Stack`  
**Fő framework: M.E.AI**

### Tanulási célok
- `IChatClient` és `IEmbeddingGenerator` — provider lock-in elkerülése
- M.E.AI middleware pipeline (logging, telemetry, function invocation)
- Semantic Kernel Kernel mint agent konténer
- LlamaSharp ↔ Ollama váltás demonstrálása

---

## Modul 2 — Tool Calling & Function Calling

**Fájl:** `modul-02-toolcalling.md` | **Lab:** `Lab02.ToolCalling`  
**Fő framework: M.E.AI + MAF**

### Tanulási célok
- Tool calling mechanizmus LLM szinten
- **SK `[KernelFunction]`** — production plugin rendszer, DI-barát
- **MAF `AIFunctionFactory.Create()`** — lightweight, M.E.AI natív
- Auto vs. manuális tool loop
- Mikor melyik szintaxis (SK plugin vs MAF function)

> ⚠️ Qwen2.5-7B szükséges tool callinghoz.

---

## Modul 3 — Memory Architektúra (STM / LTM)

**Fájl:** `modul-03-memory.md` | **Lab:** `Lab03.Memory`  
**Fő framework: SK** *(MAF-ban nincs built-in alternatíva)*

### Tanulási célok
- STM: `ChatHistory`, sliding window, összefoglalás
- LTM: Qdrant + nomic-embed-text, semantic search
- STM + LTM együttes pipeline

---

## Modul 4 — RAG → Agentic RAG

**Fájl:** `modul-04-rag.md` | **Lab:** `Lab04.AgenticRAG`  
**Fő framework: SK** *(MAF-ban nincs built-in alternatíva)*

### Tanulási célok
- Traditional RAG korlátai
- Agentic RAG: query rewriting, iteratív retrieval, gap detection, szintézis

---

## Modul 5 — Multi-Agent Orchesztráció (Fogalmi alap)

**Fájl:** `modul-05-multiagent.md` | **Lab:** `Lab05.MultiAgent`  
**Fő framework: SK → átvezetés MAF-ra**

### Tanulási célok
- Orchestration, Choreography, Handoff pattern — fogalmi megértés
- SK `ChatCompletionAgent` + `AgentGroupChat` — **mint referencia implementáció**
- Mi hiányzik belőle? → motiválja a MAF-ra váltást
- Átvezetés: SK fogalmak ↔ MAF megfelelők

> ℹ️ Ez a modul tudatosan rövidebb — a multi-agent production tudás **Modul 5.5-ben** van.

---

## Modul 5.5 — Microsoft Agentic Framework (MAF) ⭐

**Fájl:** `modul-055-maf.md` | **Lab:** `Lab055.MAF`  
**Fő framework: MAF**

### Tanulási célok
- MAF = SK + AutoGen utódja — miért és hogyan
- `ChatClientAgent` — M.E.AI natív, Kernel nélkül
- `AIFunctionFactory.Create()` — tool definíció MAF-ban
- Graph-based Workflow: typed State + Nodes + Edges
- Type-based routing (determinisztikus)
- Checkpointing: pause/resume
- Built-in HITL: workflow suspend + approval + resume
- Agent Filters — middleware MAF-ban
- Nested workflows

> ⚠️ Ollama ajánlott (OpenAI-kompatibilis API szükséges).

---

## Modul 6 — MCP (Model Context Protocol)

**Fájl:** `modul-06-mcp.md` | **Lab:** `Lab06.MCP`  
**Fő framework: MAF + M.E.AI**

### Tanulási célok
- MCP Server + Client .NET-ben
- MAF agent + MCP integráció
- Multi-MCP, MCP vs REST

---

## Modul 7 — Biztonság, HITL, Guardrails

**Fájl:** `modul-07-security.md` | **Lab:** `Lab07.Security`  
**Fő framework: MAF + M.E.AI middleware**

### Tanulási célok
- Least Privilege, Prompt injection, Output redaction
- HITL — MAF built-in vs. manuális M.E.AI middleware
- Audit logging pipeline

---

## Ajánlott haladási sorrend

```
Modul 0 → 1 → 2 → 3 → 4 → 5 → 5.5 → 6 → 7
(setup)   ↑   ↑   ↑   ↑   ↑    ↑
          │   │   SK  SK  SK   MAF ← production csúcspont
          │   M.E.AI + MAF
          M.E.AI alap
```

---

## Framework döntési táblázat (referencia)

| Feladat | Használj |
|---|---|
| LLM kommunikáció, provider váltás | **M.E.AI** |
| Middleware (logging, telemetry, rate limit) | **M.E.AI** |
| Tool/function definíció (egyszerű) | **MAF** `AIFunctionFactory.Create()` |
| Tool/function definíció (komplex, DI-val) | **SK** `[KernelFunction]` |
| Single agent, chat loop | **MAF** `ChatClientAgent` |
| Multi-agent, production workflow | **MAF** Workflow graph |
| RAG, Vector DB, Embedding | **SK** (MAF-ban nincs) |
| Long-running, checkpointed workflow | **MAF** |
| HITL checkpoint | **MAF** built-in |
| MCP integráció | **MAF** + `ModelContextProtocol` |

---

## Környezet referencia

| Komponens | Értékek |
|-----------|---------|
| .NET | 10.0 |
| Chat modell | Qwen2.5-7B-Instruct-Q4_K_M |
| Embedding modell | nomic-embed-text-v1.5 |
| Provider A | LlamaSharp (in-process) |
| Provider B | Ollama (OpenAI-compat API) — MAF-hoz ajánlott |
| Primary agent framework | **Microsoft Agent Framework (MAF) v1.0** |
| Memory / RAG framework | Microsoft Semantic Kernel |
| Vector DB | Qdrant (lokális Docker) |
| MCP SDK | ModelContextProtocol (.NET) |
| DI / Host | Microsoft.Extensions.Hosting |
