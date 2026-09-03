using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace MiniBank.AI.Tests.Support;

/// <summary>
/// Forwards to a real chat client (Ollama) while recording tool calls and results.
/// </summary>
internal sealed class RecordingChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public List<FunctionCallContent> InvokedCalls { get; } = [];
    public List<FunctionResultContent> ToolResults { get; } = [];

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        RecordIncoming(messages);
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        RecordOutgoing(response.Messages);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        RecordIncoming(messages);

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            InvokedCalls.AddRange(update.Contents.OfType<FunctionCallContent>());
            yield return update;
        }
    }

    private void RecordIncoming(IEnumerable<ChatMessage> messages)
    {
        var results = messages
            .SelectMany(message => message.Contents.OfType<FunctionResultContent>())
            .ToList();

        if (results.Count == 0)
            return;

        ToolResults.Clear();
        ToolResults.AddRange(results);
    }

    private void RecordOutgoing(IEnumerable<ChatMessage> messages)
    {
        foreach (var message in messages)
            InvokedCalls.AddRange(message.Contents.OfType<FunctionCallContent>());
    }
}
