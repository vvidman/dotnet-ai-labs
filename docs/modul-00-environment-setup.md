# Module 0 — Lab Environment Setup

> **Purpose:** Shared prerequisite project for all labs (Module 1–7 + Module 5.5)  
> **Setup time:** ~30 minutes (first time only)  
> **Result:** A running `dotnet-ai-labs` solution with two interchangeable local LLM providers  

> 📝 **Implementation note:** The class names and initialisation patterns in this module
> reflect verified, working code — not the theoretical API surface. Discrepancies found
> during Module 1 lab work are documented in `MODULE-01-SUMMARY.md`.

---

## Why a Shared Environment?

The core lesson of Module 1 is that `IChatClient` makes providers swappable.
This environment proves it: every lab runs on **both providers without code changes**
— only the single provider registration line differs.

```
Provider A — LlamaSharp:  in-process GGUF inference, no daemon, full .NET control
Provider B — Ollama:      OpenAI-compatible local API, reliable tool calling, MAF-ready
```

---

## Prerequisites

```bash
# 1. .NET SDK
dotnet --version    # must be 10.0.x

# 2. Docker (for Qdrant — needed from Module 3 onwards)
docker --version

# 3. Ollama
# Windows/macOS: https://ollama.ai/download
# Linux:
curl -fsSL https://ollama.ai/install.sh | sh
ollama --version
```

---

## Solution Structure

```bash
mkdir dotnet-ai-labs && cd dotnet-ai-labs
dotnet new sln -n DotnetAiLabs

dotnet new classlib -n AiLabs.Infrastructure --framework net10.0
dotnet sln add AiLabs.Infrastructure
```

---

## The Model: Qwen2.5-7B-Instruct

**Qwen2.5-7B-Instruct** is the recommended model for all labs.

| Property | Value |
|---|---|
| Tool calling | ✅ Native, best-in-class for 7B |
| Context window | 128k tokens |
| Size (Q4_K_M) | ~5 GB |
| Embedding | Separate model required (see below) |

### Download for LlamaSharp (GGUF)

```bash
# https://huggingface.co/bartowski/Qwen2.5-7B-Instruct-GGUF
# Recommended quantisation: Q4_K_M (~5 GB)
# Save to a dedicated models folder:
#   Windows:     C:\models\
#   Linux/macOS: ~/models/
```

### Download for Ollama

```bash
ollama pull qwen2.5:7b
ollama list   # verify: qwen2.5:7b should appear
```

### Embedding Model (Module 3+)

```bash
# LlamaSharp: nomic-embed-text-v1.5.Q4_K_M.gguf (~270 MB)
# https://huggingface.co/nomic-ai/nomic-embed-text-v1.5-GGUF

# Ollama:
ollama pull nomic-embed-text
```

---

## NuGet Packages (AiLabs.Infrastructure)

```bash
cd AiLabs.Infrastructure

# Abstraction layer
dotnet add package Microsoft.Extensions.AI
dotnet add package Microsoft.Extensions.AI.Abstractions

# LlamaSharp — core + M.E.AI bridge
dotnet add package LLamaSharp
dotnet add package LLamaSharp.Backend.Cpu        # CPU inference
# GPU: dotnet add package LLamaSharp.Backend.Cuda12   (NVIDIA CUDA 12)
# GPU: dotnet add package LLamaSharp.Backend.Vulkan    (AMD / Intel Arc)
dotnet add package LLamaSharp.SemanticKernel      # IChatCompletionService bridge

# Ollama
dotnet add package OllamaSharp

# Semantic Kernel (for Modules 3–5, Memory, RAG)
dotnet add package Microsoft.SemanticKernel

# DI / hosting / config
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Logging.Console
```

---

## Provider A — LlamaSharp

### ⚠️ The correct initialisation chain

The LlamaSharp API has a specific chain that must be followed exactly.
`LLamaSharpChatClient` **does not exist** — this is a common mistake from outdated docs.
The correct path is:

```
ModelParams
    └── LLamaWeights.LoadFromFile()   ← IDisposable — must outlive the app
            └── model.CreateContext()  ← IDisposable — must outlive the app
                    └── new InteractiveExecutor(context)
                            └── new LLamaSharpChatCompletion(executor)
                                    └── .AsChatClient()  ← IChatClient ✅
```

### LlamaSharpProvider.cs

```csharp
// AiLabs.Infrastructure/Providers/LlamaSharpProvider.cs
using LLama;
using LLama.Common;
using LLamaSharp.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace AiLabs.Infrastructure.Providers;

/// <summary>
/// Wraps LlamaSharp model lifetime and exposes IChatClient.
/// Implements IDisposable — register as a singleton and dispose on app shutdown.
/// </summary>
public sealed class LlamaSharpProvider : IDisposable
{
    // Both are IDisposable and must outlive the application.
    // using var cannot be used — they must not be disposed inside the constructor.
    private readonly LLamaWeights _model;
    private readonly LLamaContext _context;

    public IChatClient ChatClient { get; }
    public IEmbeddingGenerator<string, Embedding<float>>? EmbeddingGenerator { get; }

    public LlamaSharpProvider(IConfiguration config)
    {
        var modelPath   = config["LlamaSharp:ModelPath"]
            ?? throw new InvalidOperationException("LlamaSharp:ModelPath not configured");
        var contextSize = uint.Parse(config["LlamaSharp:ContextSize"] ?? "4096");
        var gpuLayers   = int.Parse(config["LlamaSharp:GpuLayerCount"] ?? "0");

        Console.WriteLine($"⏳ Loading model: {Path.GetFileName(modelPath)}");

        var modelParams = new ModelParams(modelPath)
        {
            ContextSize   = contextSize,
            GpuLayerCount = gpuLayers
        };

        // Full initialisation chain
        _model   = LLamaWeights.LoadFromFile(modelParams);
        _context = _model.CreateContext(modelParams);
        var executor = new InteractiveExecutor(_context);

        // LLamaSharpChatCompletion implements SK's IChatCompletionService.
        // .AsChatClient() bridges it to M.E.AI's IChatClient.
        var chatCompletion = new LLamaSharpChatCompletion(executor);
        ChatClient = chatCompletion.AsChatClient();

        Console.WriteLine("✅ Model loaded.");

        // Embedding model — only if configured
        var embeddingPath = config["LlamaSharp:EmbeddingModelPath"];
        if (!string.IsNullOrEmpty(embeddingPath))
        {
            var embParams  = new ModelParams(embeddingPath);
            var embModel   = LLamaWeights.LoadFromFile(embParams);
            var embedder   = embModel.CreateEmbedder(embParams);
            EmbeddingGenerator = embedder.AsEmbeddingGenerator();
            Console.WriteLine("✅ Embedding model loaded.");
        }
    }

    public void Dispose()
    {
        _context.Dispose();
        _model.Dispose();
    }
}
```

### LlamaSharpHostedService.cs — lifetime management

`LlamaSharpProvider` is `IDisposable`. Register it via a hosted service to ensure
disposal on application shutdown. Inline `using var` inside a factory lambda
**does not work** — it disposes the model before it is ever used.

```csharp
// AiLabs.Infrastructure/Providers/LlamaSharpHostedService.cs
using Microsoft.Extensions.Hosting;

namespace AiLabs.Infrastructure.Providers;

/// <summary>
/// Manages LlamaSharpProvider lifetime as a hosted singleton.
/// Ensures model and context are disposed on application shutdown.
/// </summary>
public sealed class LlamaSharpHostedService : IHostedService, IDisposable
{
    private LlamaSharpProvider? _provider;
    private readonly IConfiguration _config;

    public IChatClient ChatClient
        => _provider?.ChatClient
           ?? throw new InvalidOperationException("LlamaSharp not started.");

    public IEmbeddingGenerator<string, Embedding<float>>? EmbeddingGenerator
        => _provider?.EmbeddingGenerator;

    public LlamaSharpHostedService(IConfiguration config) => _config = config;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _provider = new LlamaSharpProvider(_config);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => _provider?.Dispose();
}
```

---

## Provider B — Ollama

```csharp
// AiLabs.Infrastructure/Providers/OllamaProvider.cs
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OllamaSharp;

namespace AiLabs.Infrastructure.Providers;

public sealed class OllamaProvider
{
    public IChatClient ChatClient { get; }
    public IEmbeddingGenerator<string, Embedding<float>> EmbeddingGenerator { get; }

    public OllamaProvider(IConfiguration config)
    {
        var endpoint   = config["Ollama:Endpoint"] ?? "http://localhost:11434";
        var chatModel  = config["Ollama:ChatModel"] ?? "qwen2.5:7b";
        var embedModel = config["Ollama:EmbeddingModel"] ?? "nomic-embed-text";

        var client = new OllamaApiClient(new Uri(endpoint));

        ChatClient         = client.AsChatClient(chatModel);
        EmbeddingGenerator = client.AsEmbeddingGenerator(embedModel);

        Console.WriteLine($"✅ Ollama: {endpoint} | {chatModel}");
    }
}
```

---

## Shared Configuration

### appsettings.json

```json
{
  "Provider": "Ollama",

  "LlamaSharp": {
    "ModelPath": "C:/models/Qwen2.5-7B-Instruct-Q4_K_M.gguf",
    "EmbeddingModelPath": "C:/models/nomic-embed-text-v1.5.Q4_K_M.gguf",
    "ContextSize": 4096,
    "GpuLayerCount": 0
  },

  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ChatModel": "qwen2.5:7b",
    "EmbeddingModel": "nomic-embed-text"
  }
}
```

> Switch providers by changing `"Provider"` to `"LlamaSharp"` or `"Ollama"`.
> No code changes required.

### .csproj — copy appsettings

```xml
<ItemGroup>
  <Content Include="appsettings.json">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

---

## ProviderFactory.cs

```csharp
// AiLabs.Infrastructure/ProviderFactory.cs
using AiLabs.Infrastructure.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace AiLabs.Infrastructure;

public static class ProviderFactory
{
    public static IChatClient CreateChatClient(IConfiguration config)
    {
        var provider = config["Provider"] ?? "Ollama";
        return provider.ToLowerInvariant() switch
        {
            "llamasharp" => new LlamaSharpProvider(config).ChatClient,
            "ollama"     => new OllamaProvider(config).ChatClient,
            _ => throw new InvalidOperationException(
                $"Unknown provider: '{provider}'. Use 'LlamaSharp' or 'Ollama'.")
        };
    }

    public static IEmbeddingGenerator<string, Embedding<float>>
        CreateEmbeddingGenerator(IConfiguration config)
    {
        var provider = config["Provider"] ?? "Ollama";
        return provider.ToLowerInvariant() switch
        {
            "llamasharp" => new LlamaSharpProvider(config).EmbeddingGenerator
                ?? throw new InvalidOperationException(
                    "Embedding model not configured. Set LlamaSharp:EmbeddingModelPath."),
            "ollama" => new OllamaProvider(config).EmbeddingGenerator,
            _ => throw new InvalidOperationException($"Unknown provider: '{provider}'")
        };
    }
}
```

---

## ServiceCollectionExtensions.cs

```csharp
// AiLabs.Infrastructure/ServiceCollectionExtensions.cs
using AiLabs.Infrastructure.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace AiLabs.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiLabsInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        var provider = config["Provider"] ?? "Ollama";

        if (provider.Equals("LlamaSharp", StringComparison.OrdinalIgnoreCase))
        {
            // LlamaSharp requires hosted lifetime management
            services.AddSingleton<LlamaSharpHostedService>();
            services.AddHostedService(sp => sp.GetRequiredService<LlamaSharpHostedService>());

            // Resolve IChatClient from the hosted service — ensures model is loaded
            services.AddChatClient(sp =>
                sp.GetRequiredService<LlamaSharpHostedService>().ChatClient)
                .UseLogging()
                .UseFunctionInvocation();
        }
        else
        {
            // Ollama: stateless, no lifecycle management needed
            services.AddChatClient(sp =>
                new OllamaProvider(config).ChatClient)
                .UseLogging()
                .UseFunctionInvocation();
        }

        // Semantic Kernel — resolves IChatClient from DI automatically
        services.AddKernel();

        return services;
    }

    /// <summary>Extended registration including embeddings (Module 3+)</summary>
    public static IServiceCollection AddAiLabsWithEmbeddings(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddAiLabsInfrastructure(config);

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
            sp => ProviderFactory.CreateEmbeddingGenerator(config));

        return services;
    }
}
```

---

## Standard Lab Bootstrap

Every lab `Program.cs` starts like this:

```csharp
using AiLabs.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var host = Host.CreateDefaultBuilder()
    .ConfigureServices(services =>
    {
        services.AddSingleton(config);
        services.AddAiLabsInfrastructure(config); // or AddAiLabsWithEmbeddings for Module 3+
    })
    .Build();

await host.StartAsync();   // triggers LlamaSharpHostedService.StartAsync if applicable

var kernel = host.Services.GetRequiredService<Kernel>();

// Lab-specific code from here
```

> **Why `Host.CreateDefaultBuilder()`?**
> The hosted service pattern ensures `LlamaSharpProvider` (with its `IDisposable` model
> and context) is started before use and disposed cleanly on shutdown.
> Resolving `Kernel` directly from a bare `ServiceCollection` also works —
> but `model` and `context` would never be disposed.

---

## Per-Module Provider Guidance

| Module | LlamaSharp | Ollama | Notes |
|---|---|---|---|
| 0 — Infrastructure | ✅ | ✅ | Build and verify both |
| 1 — Stack | ✅ | ✅ | Either works |
| 2 — Tool Calling | ✅ Qwen2.5 only | ✅ | Phi-3.5 Mini not suitable |
| 3 — Memory | ✅ | ✅ | Embedding model required |
| 4 — Agentic RAG | ✅ | ✅ | Embedding model required |
| 5 — Multi-Agent | ✅ | ✅ | Either works |
| 5.5 — MAF | ⚠️ limited | ✅ recommended | MAF needs OpenAI-compatible API |
| 6 — MCP | ✅ | ✅ | Either works |
| 7 — Security | ✅ | ✅ | Either works |

---

## Qdrant Setup (Module 3+)

```bash
docker run -d \
  --name qdrant \
  -p 6333:6333 \
  -v qdrant_storage:/qdrant/storage \
  qdrant/qdrant

curl http://localhost:6333/healthz   # should return: {"status":"ok"}
```

NuGet (add to Module 3+ lab projects):

```bash
dotnet add package Microsoft.SemanticKernel.Connectors.Qdrant
dotnet add package Microsoft.SemanticKernel.Plugins.Memory
```

---

## Environment Reference

| Component | Value |
|---|---|
| .NET | 10.0 |
| Chat model | Qwen2.5-7B-Instruct-Q4_K_M |
| Embedding model | nomic-embed-text-v1.5 |
| Provider A | LlamaSharp — `LLamaSharpChatCompletion` + `.AsChatClient()` |
| Provider B | Ollama — `OllamaApiClient` + `.AsChatClient()` |
| Agent Framework | Microsoft Semantic Kernel (Memory/RAG) |
| MAF | Microsoft Agent Framework v1.0 (Module 5.5+) |
| Vector DB | Qdrant (local Docker) |
| MCP SDK | ModelContextProtocol (.NET) |
