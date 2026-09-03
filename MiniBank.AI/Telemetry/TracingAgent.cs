using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MiniBank.AI.Telemetry;

internal sealed class TracingAgent(AIAgent inner, ILogger logger, string modelName)
    : DelegatingAIAgent(inner)
{
    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        CancellationToken cancellationToken)
    {
        using var activity = MiniBankActivitySources.Agent.StartActivity("agent.run");
        activity?.SetTag("gen_ai.request.model", modelName);

        var query = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        logger.LogInformation("Agent query started: Query: {Query}", query);

        try
        {
            var response = await base.RunCoreAsync(messages, session, options, cancellationToken);
            logger.LogInformation("Agent completed: {Text}", response.Text);
            return response;
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }
}
