# Module 1 — The .NET AI Stack Layers · Summary

> **Branch:** `modul-01`  
> **Lab project:** `Lab01.Stack`  
> **Status:** ✅ Complete

---

## What This Module Was About

This module was about understanding and assembling the three layers of the .NET AI stack: LlamaSharp (local LLM provider), Microsoft.Extensions.AI (provider-agnostic abstraction), and Semantic Kernel (agent container). The goal was to establish a foundation that every subsequent module builds on — without provider lock-in, with DI-based configuration and a composable middleware pipeline.

---

## Learnings

### Concepts that clicked

- `IChatClient` (M.E.AI) really is like `ILogger` — swapping the provider is a one-line change, everything else stays untouched
- The `ChatClientBuilder` middleware pipeline is declarative and layered, exactly like the ASP.NET Core pipeline — runs from outside in
- SK and M.E.AI are not competitors: SK uses M.E.AI internally, they build on each other
- Passing a `List<ChatMessage>` history on every call is all short-term memory is — no magic, just state managed at the application layer

### Things that surprised me

- `LLamaSharpChatClient` does not exist — the module description references it incorrectly. The actual class is `LLamaSharpChatCompletion`, which implements SK's `IChatCompletionService`, and must be bridged to M.E.AI's `IChatClient` via `.AsChatClient()`
- Calling `.Build()` on the result of `AddKernel()` throws an `InvalidOperationException` — the Kernel must be resolved from the built service provider, not from the builder itself. The Step 5 code in the module description was wrong
- `IChatCompletionService` is not automatically registered in SK's DI when only an `IChatClient` is provided — a separate bridge would be needed, but using `IChatClient` directly with `List<ChatMessage>` is the simpler and cleaner path

### What I would have missed without implementing it

- The full LlamaSharp initialisation chain (`model` → `context` → `executor`) — the module description only referenced the `executor` variable with no explanation of how to construct it
- The need for a DI factory lambda: `chatClient` depends on `loggerFactory`, so registering it as an inline factory is necessary to avoid a dependency ordering problem
- `model` and `context` are `IDisposable` but are never disposed inside a singleton factory — in production this requires a hosted service wrapper or a similar lifetime management strategy

---

## Challenges

| Challenge | How I resolved it |
|-----------|------------------|
| `LLamaSharpChatClient` could not be resolved | Used `LLamaSharpChatCompletion` + `.AsChatClient()` bridge instead |
| `Kernel.CreateBuilder()...Build()` threw `InvalidOperationException` | Switched to `ServiceCollection` + `services.AddKernel()` + `provider.GetRequiredService<Kernel>()` |
| `kernel.GetRequiredService<IChatCompletionService>()` failed | Switched to `IChatClient` with `List<ChatMessage>` instead of SK's `ChatHistory` |
| Full LlamaSharp initialisation was missing from the module description | Wrote a dedicated `LLamaSharpChatClientFactory` static method covering the full chain |
| `loggerFactory` and `chatClient` had a DI ordering dependency | Registered `IChatClient` as a lambda factory so `loggerFactory` is resolved at build time |

---

## Decisions & Tradeoffs

### Decision: IChatClient directly, not IChatCompletionService

**What I chose:** `IChatClient` (M.E.AI) with `List<ChatMessage>` for the conversation loop  
**The alternative:** Registering an SK `IChatCompletionService` bridge and using `ChatHistory` (SK type)  
**Why:** `IChatClient` is already registered and the middleware pipeline runs on it — the bridge would only add complexity in Module 1  
**The cost:** SK-specific `ChatHistory` and related SK APIs are not available — but these come in naturally in Module 2

### Decision: Static factory method for LlamaSharp initialisation

**What I chose:** A `LLamaSharpChatClientFactory(config)` static method called from within the DI lambda  
**The alternative:** Inline initialisation inside the lambda  
**Why:** Readability and separation of concerns — model loading logic is isolated from DI configuration  
**The cost:** `model` and `context` disposal is not handled — in production a hosted service or `IDisposable` wrapper is needed

---

## Observations

- `using var` cannot be used inside the factory method because `model` and `context` must outlive the method scope — this is a common LlamaSharp gotcha
- `.UseFunctionInvocation()` is already wired into the middleware pipeline but has no effect until Module 2 — it is in the right place, just waiting
- `appsettings.json`-based configuration paid off immediately: model path and context size are changed in one place without touching code

---

## Looking Ahead

Module 1 laid the foundation: the local LLM is running, the abstraction layer is in place, and the DI container is assembled. Module 2 builds directly on this — `.UseFunctionInvocation()` gets its purpose, and the SK `[KernelFunction]` plugin system comes in. That is the point where an LLM becomes an actual agent.

---

*From .NET Dev to AI Engineer — Step by Step*
