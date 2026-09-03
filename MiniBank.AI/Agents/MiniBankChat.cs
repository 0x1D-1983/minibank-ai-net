using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Telemetry;
using OllamaSharp;

namespace MiniBank.AI.Agents;

internal static class MiniBankChat
{
    public const string Endpoint = "http://localhost:11434";
    public const string ModelName = "qwen2.5:1.5b-instruct";

    public static IChatClient Create(IChatClient? chatClient, ILoggerFactory loggerFactory)
    {
        chatClient ??= new OllamaApiClient(new Uri(Endpoint), ModelName);

        return chatClient
            .AsBuilder()
            .Use(inner => new TracingChatClient(
                inner,
                loggerFactory.CreateLogger<TracingChatClient>(),
                ModelName))
            .UseLogging(loggerFactory)
            .Build();
    }

    public static AIFunction Tool(Delegate method, string name, ILogger logger)
        => new TracingAIFunction(AIFunctionFactory.Create(method, name: name), logger);

    public static AIAgent Instrument(AIAgent agent, ILoggerFactory loggerFactory)
        => new TracingAgent(
            agent.AsBuilder()
                .UseOpenTelemetry(
                    MiniBankActivitySources.Agent.Name,
                    otel => otel.EnableSensitiveData = true)
                .Build(),
            loggerFactory.CreateLogger<TracingAgent>(),
            ModelName);
}
