# dotnet-ai-labs

**From .NET Dev to AI Engineer — Step by Step**

A hands-on companion repository to the Udemy course  
[Agentic AI Architectures with Patterns, Frameworks and MCP](https://www.udemy.com/course/agentic-ai-architectures-with-patterns-frameworks-and-mcp/)  
by [Mehmet Ozkaya](https://www.udemy.com/user/mehmet-ozkaya/).

---

## Why This Repo Exists

Mehmet's course gave me one of the strongest theoretical foundations I've found for Agentic AI. However — like many fellow students noted in the reviews — the practical, hands-on side felt missing.

This repository is my answer to that gap. It is **not a follow-along implementation** of the course, nor does it aim to cover the same theoretical ground. The course was the inspiration; the lab design, the architectural choices, and the implementation decisions here reflect my own thinking and preferences.

If you are looking for a structured theoretical foundation first, I recommend starting with the course. If you want to see one developer's hands-on journey building Agentic AI systems in .NET — you are in the right place.

---

## Strategic Direction: MAF-First

Microsoft has positioned **Microsoft Agent Framework (MAF)** as the official successor to both Semantic Kernel and AutoGen (released v1.0 April 2026). This shapes the entire curriculum:

```
M.E.AI  →  the non-negotiable foundation — MAF is built on IChatClient
MAF     →  primary agent and orchestration framework (Module 2 onwards)
SK      →  used only where MAF has no equivalent: RAG, Vector DB, Embeddings
```

---

## The Stack

```
┌─────────────────────────────────────────────────────┐
│                    YOUR CODE                        │
│          (business logic, agent workflows)          │
├───────────────────────────┬─────────────────────────┤
│           MAF             │      SK (where needed)  │
│  agent · orchestration    │  RAG · Memory · VDB     │
│  workflows · HITL         │                         │
├───────────────────────────┴─────────────────────────┤
│           MICROSOFT.EXTENSIONS.AI                   │
│   IChatClient — unified interface for all providers │
│   both MAF and SK build on this                     │
├─────────────────────────────────────────────────────┤
│           LLM PROVIDER (swappable)                  │
│   LlamaSharp │ Ollama │ Azure OpenAI │ OpenAI │ ... │
└─────────────────────────────────────────────────────┘
```

**Runtime:** .NET 10 · Qwen2.5-7B · LlamaSharp / Ollama · Microsoft.Extensions.AI · MAF · SK

---

## Modules

[From .NET Dev to AI Engineer — Why I Started This Series](https://vvidman.github.io/posts/dotnet-ai-stack-00.html)

| # | Module | Primary Framework | Key Technologies | Branch | Blog |
|---|--------|------------------|-----------------|--------|------|
| 0 | Lab Environment Setup | M.E.AI | LlamaSharp · Ollama · Qwen2.5 · ProviderFactory | [`modul-00`](../../tree/modul-00) | — |
| 1 | The .NET AI Stack | M.E.AI | IChatClient · Middleware pipeline · SK Kernel | [`modul-01`](../../tree/modul-01) | [Building the .NET AI Stack from First Principles](https://vvidman.github.io/posts/dotnet-ai-stack-01.html) |
| 2 | Tool Calling & Function Calling | M.E.AI + MAF | AIFunctionFactory · KernelFunction · tool loop | [`modul-02`](../../tree/modul-02) | — |
| 3 | Memory Architecture (STM/LTM) | SK | ChatHistory · Qdrant · nomic-embed-text | [`modul-03`](../../tree/modul-03) | — |
| 4 | RAG → Agentic RAG | SK | Query rewriting · Active retrieval · Qdrant | [`modul-04`](../../tree/modul-04) | — |
| 5 | Multi-Agent — Conceptual Foundation | SK → MAF | AgentGroupChat (context) · patterns · bridge to MAF | [`modul-05`](../../tree/modul-05) | — |
| 5.5 | Microsoft Agentic Framework | **MAF** ⭐ | Workflows · Checkpointing · HITL · Filters | [`modul-055`](../../tree/modul-055) | — |
| 6 | MCP (Model Context Protocol) | MAF + M.E.AI | MCP Server · MCP Client · REST vs MCP | [`modul-06`](../../tree/modul-06) | — |
| 7 | Security, HITL & Guardrails | MAF + M.E.AI | Least Privilege · HITL · Prompt injection · Audit log | [`modul-07`](../../tree/modul-07) | — |

> ⭐ Module 5.5 is the curriculum centrepiece — everything before it builds toward this.

Blog links will be added as each module is completed.

---

## How This Repo Is Structured

Each module lives in its own branch, built on top of the previous one — mirroring the actual learning progression:

```
main
 └── modul-00  (shared AiLabs.Infrastructure)
      └── modul-01
           └── modul-02
                └── modul-03
                     └── modul-04
                          └── modul-05
                               └── modul-055
                                    └── modul-06
                                         └── modul-07
```

Every branch contains a `MODUL.md` file with the module overview, how to run the lab, and honest notes on what I learned along the way.

**Module 0 is special:** it lives in its own branch and is the shared infrastructure that every subsequent lab references. It is never "completed" — it evolves as the stack evolves.

---

## Prerequisites

| Requirement | Details |
|-------------|---------|
| .NET | 10 SDK |
| IDE | Visual Studio / Rider / VS Code |
| LLM model | [Qwen2.5-7B-Instruct-Q4_K_M.gguf](https://huggingface.co/bartowski/Qwen2.5-7B-Instruct-GGUF) (~5 GB) |
| Embedding model | [nomic-embed-text-v1.5.Q4_K_M.gguf](https://huggingface.co/nomic-ai/nomic-embed-text-v1.5-GGUF) (~270 MB) — Module 3+ |
| Ollama | [ollama.ai/download](https://ollama.ai/download) — recommended for Module 5.5 (MAF) |
| RAM | 16 GB recommended (8 GB minimum) |
| GPU | Not required — CPU inference via LlamaSharp. CUDA 12 supported. |
| Docker | Required from Module 3 — for Qdrant vector DB |
| Qdrant | Module 3+ — [quick start](https://qdrant.tech/documentation/quick-start/) |

### LLM: two providers, one codebase

Every lab supports two interchangeable local providers. Switching between them is a single config change — no code changes required. This is the `IChatClient` abstraction in action.

**Provider A — LlamaSharp** (in-process GGUF inference)
```bash
# Download Qwen2.5-7B-Instruct-Q4_K_M.gguf to your models folder
# https://huggingface.co/bartowski/Qwen2.5-7B-Instruct-GGUF
```

**Provider B — Ollama** (OpenAI-compatible local API, required for MAF)
```bash
ollama pull qwen2.5:7b
ollama pull nomic-embed-text   # for Module 3+
```

Switch providers by changing one line in `appsettings.json`:
```json
{ "Provider": "LlamaSharp" }   // or "Ollama"
```

### Why Qwen2.5-7B?

| | Phi-3.5 Mini (old) | Qwen2.5-7B-Instruct |
|---|---|---|
| Tool calling | ❌ unreliable | ✅ native, best-in-class |
| Context window | 4k | 128k |
| Multilingual | limited | 100+ languages |
| Size | ~2.4 GB | ~5 GB |

Tool calling is the core primitive of every agent — a model that does it unreliably blocks every module from Module 2 onwards. Qwen2.5-7B is the right choice.

---

## Framework Decision Reference

| Task | Use |
|---|---|
| LLM communication, provider switching | **M.E.AI** `IChatClient` |
| Middleware (logging, telemetry, rate limiting) | **M.E.AI** `ChatClientBuilder` |
| Tool definition (simple, stateless) | **MAF** `AIFunctionFactory.Create()` |
| Tool definition (complex, DI-injected services) | **SK** `[KernelFunction]` |
| Single agent, chat loop | **MAF** `ChatClientAgent` |
| Multi-agent production workflow | **MAF** Workflow graph |
| RAG, Vector DB, Embeddings | **SK** (no MAF equivalent) |
| Long-running, checkpointed workflow | **MAF** + DurableTask |
| Human-in-the-Loop checkpoint | **MAF** built-in |
| External system integration | **MCP** + `ModelContextProtocol` |

---

## Blog Series

**From .NET Dev to AI Engineer — Step by Step**

Posts will be published as each module is completed. Links updated here.

---

## License

MIT
