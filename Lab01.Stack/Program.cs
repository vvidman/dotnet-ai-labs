using LLama;
using LLama.Common;
using LLamaSharp.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

// --- Loading configuration ---
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

// --- Creating DI container ---
var services = new ServiceCollection();

// --- Add logging ---
services.AddLogging(builder => builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug));

// Register IChatClient as factory
services.AddSingleton<IChatClient>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new ChatClientBuilder(LLamaSharpChatClientFactory(config))
        .UseLogging(loggerFactory)
        .UseFunctionInvocation()
        .Build();
});

// Register Semantic Kernel 
services.AddKernel();

// Build Service provider
var provider = services.BuildServiceProvider();

// mini challenge, iterate multi chat messages
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine("Chat started, to exit press: Ctrl+C\n");

// Resolve IChatClient instead
var chatClient = provider.GetRequiredService<IChatClient>();

var messages = new List<ChatMessage>
{
    new(ChatRole.System, "You are a helpful .NET expert assistant.")
};

while (!cts.Token.IsCancellationRequested)
{
    Console.Write("Next message: ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input)) continue;

    messages.Add(new ChatMessage(ChatRole.User, input));

    var result = await chatClient.GetResponseAsync(messages, cancellationToken: cts.Token);

    messages.Add(new ChatMessage(ChatRole.Assistant, result.Text));

    Console.WriteLine($"\nAnswer: {result.Text}\n");
}

Console.WriteLine("Completed. Press any key to complete.");
Console.ReadKey();

static IChatClient LLamaSharpChatClientFactory(IConfigurationRoot config)
{
    var modelPath = config["LlamaSharp:ModelPath"]!;
    var contextSize = uint.Parse(config["LlamaSharp:ContextSize"] ?? "4096");
    var gpuLayers = int.Parse(config["LlamaSharp:GpuLayerCount"] ?? "0");

    // --- Building LlamaSharp objects ---
    Console.WriteLine(" Loading model...");

    var modelParams = new ModelParams(modelPath)
    {
        ContextSize = contextSize,
        GpuLayerCount = gpuLayers
    };
    var model = LLamaWeights.LoadFromFile(modelParams);
    var context = model.CreateContext(modelParams);
    var executor = new InteractiveExecutor(context);

    Console.WriteLine(" Modell loaded.\n");

    // --- IChatClient (M.E.AI abstraction) ---
    var llamaChat = new LLamaSharpChatCompletion(executor);
    return llamaChat.AsChatClient();
}