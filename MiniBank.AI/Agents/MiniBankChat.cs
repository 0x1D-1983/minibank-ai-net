using System;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Telemetry;
using OllamaSharp;

namespace MiniBank.AI.Agents;

internal static class MiniBankChat
{
    public static IChatClient Create(IChatClient? chatClient, ILoggerFactory loggerFactory, OllamaOptions ollama)
    {
        ArgumentNullException.ThrowIfNull(ollama);

        chatClient ??= new OllamaApiClient(new Uri(ollama.Endpoint), ollama.Model);

        return chatClient
            .AsBuilder()
            .Use(inner => new TracingChatClient(
                inner,
                loggerFactory.CreateLogger<TracingChatClient>(),
                ollama.Model))
            .UseLogging(loggerFactory)
            .Build();
    }

    public static AIFunction Tool(Delegate method, string name, ILogger logger)
        => new TracingAIFunction(AIFunctionFactory.Create(method, name: name), logger);

    public static AIAgent Instrument(AIAgent agent, ILoggerFactory loggerFactory, OllamaOptions ollama)
    {
        ArgumentNullException.ThrowIfNull(ollama);
        return new TracingAgent(
            agent.AsBuilder()
                .UseOpenTelemetry(
                    MiniBankActivitySources.Agent.Name,
                    otel => otel.EnableSensitiveData = true)
                .Build(),
            loggerFactory.CreateLogger<TracingAgent>(),
            ollama.Model);
    }
}
