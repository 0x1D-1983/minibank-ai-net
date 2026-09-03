using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MiniBank.AI.Telemetry;

internal sealed class TracingChatClient(IChatClient inner, ILogger logger, string modelName)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = MiniBankActivitySources.Agent.StartActivity("agent.llm.chat");
        activity?.SetTag("gen_ai.request.model", options?.ModelId ?? modelName);

        logger.LogDebug("LLM chat started");

        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);
            LogContents(response.Messages.SelectMany(message => message.Contents));
            return response;
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }

    private void LogContents(IEnumerable<AIContent> contents)
    {
        var toolCalls = contents.OfType<FunctionCallContent>().ToList();
        if (toolCalls.Count > 0)
        {
            logger.LogInformation(
                "LLM requested {Count} tool call(s): {Names}",
                toolCalls.Count,
                string.Join(", ", toolCalls.Select(call => call.Name)));
            return;
        }

        var text = string.Concat(contents.OfType<TextContent>().Select(content => content.Text));
        if (!string.IsNullOrWhiteSpace(text))
            logger.LogInformation("LLM response: {Text}", text);
    }
}
