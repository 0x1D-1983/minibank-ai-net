using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace MiniBank.AI.Tests.Support;

/// <summary>
/// Returns scripted chat completions so workflow tests can run without Ollama.
/// </summary>
internal sealed class ScriptedChatClient : IChatClient
{
    private readonly Func<ChatOptions?, ChatResponse> _respond;

    public ScriptedChatClient(Func<ChatOptions?, ChatResponse> respond)
    {
        ArgumentNullException.ThrowIfNull(respond);
        _respond = respond;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_respond(options));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var message in response.Messages)
        {
            yield return new ChatResponseUpdate
            {
                Role = message.Role,
                Contents = message.Contents
            };
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceKey is not null)
            return null;
        if (serviceType == typeof(ChatClientMetadata))
            return new ChatClientMetadata("scripted");
        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }

    public static ChatResponse FunctionCall(string name, IDictionary<string, object?>? arguments = null)
        => new(new ChatMessage(
            ChatRole.Assistant,
            [new FunctionCallContent(Guid.NewGuid().ToString("N"), name, arguments)]));

    public static ChatResponse Text(string text)
        => new(new ChatMessage(ChatRole.Assistant, text));

    public static bool HasTool(ChatOptions? options, string name)
        => options?.Tools?.Any(tool => string.Equals(tool.Name, name, StringComparison.Ordinal)) == true;
}
