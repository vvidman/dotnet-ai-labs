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

## The Stack

```
┌──────────────────────────────────────────────────────┐
│                   YOUR CODE                          │
│         (business logic, agent workflows)            │
├──────────────────────────────────────────────────────┤
│             SEMANTIC KERNEL                          │
│   "What should the agent do?"                        │
│   Plugins · Memory · Planner · AgentGroupChat        │
├──────────────────────────────────────────────────────┤
│         MICROSOFT.EXTENSIONS.AI                      │
│   "How to talk to any LLM?"                          │
│   IChatClient — unified interface for all providers  │
├──────────────────────────────────────────────────────┤
│            LLM PROVIDER (swappable)                  │
│   LlamaSharp │ Azure OpenAI │ OpenAI │ Ollama │ ...  │
└──────────────────────────────────────────────────────┘
```

**Runtime:** .NET 10 · LlamaSharp · Microsoft.Extensions.AI · Semantic Kernel · MCP

---

## Modules

[From .NET Dev to AI Engineer — Why I Started This Series](https://vvidman.github.io/posts/dotnet-ai-stack-00.html)

| # | Module | Key Technologies | Branch | Blog |
|---|--------|-----------------|--------|------|
| 1 | The .NET AI Stack | M.E.AI · IChatClient · LlamaSharp · SK Kernel | [`modul-01`](../../tree/modul-01) | [Building the .NET AI Stack from First Principles](https://vvidman.github.io/posts/dotnet-ai-stack-01.html) |
| 2 | Tool Calling & Function Calling | KernelFunction · Auto/Manual tool loop | [`modul-02`](../../tree/modul-02) | — |
| 3 | Memory Architecture (STM/LTM) | ChatHistory · Vector DB · Embeddings | [`modul-03`](../../tree/modul-03) | — |
| 4 | RAG → Agentic RAG | Passive pipeline → Active retrieval · Qdrant | [`modul-04`](../../tree/modul-04) | — |
| 5 | Multi-Agent Orchestration | AgentGroupChat · Orchestration vs Choreography | [`modul-05`](../../tree/modul-05) | — |
| 6 | MCP (Model Context Protocol) | MCP Server · MCP Client · REST vs MCP | [`modul-06`](../../tree/modul-06) | — |
| 7 | Security, HITL & Guardrails | Least Privilege · HITL · Prompt injection · Audit log | [`modul-07`](../../tree/modul-07) | — |

Blog links will be added as each module is completed.

---

## How This Repo Is Structured

Each module lives in its own branch, built on top of the previous one — mirroring the actual learning progression:

```
main
 └── modul-01
      └── modul-02
           └── modul-03
                └── ...
                     └── modul-07
```

Every branch contains a `MODUL.md` file with the module overview, how to run the lab, and honest notes on what I learned along the way.

---

## Prerequisites

| Requirement | Details |
|-------------|---------|
| .NET | 10 SDK |
| IDE | Visual Studio / VS Code |
| LLM Model | [Phi-3-mini-4k-instruct-q4.gguf](https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-gguf) (~2.3 GB) |
| RAM | 8 GB minimum |
| GPU | Not required — CPU inference via LlamaSharp |
| Qdrant | Required from Module 3 — [docker setup](https://qdrant.tech/documentation/quick-start/) |

### Download the model

```bash
# Using huggingface-cli
huggingface-cli download microsoft/Phi-3-mini-4k-instruct-gguf \
  Phi-3-mini-4k-instruct-q4.gguf \
  --local-dir ./models
```

Set the model path in each lab's configuration before running.

---

## Blog Series

**From .NET Dev to AI Engineer — Step by Step**

Posts will be published as each module is completed. Links updated here.

---

## License

MIT
