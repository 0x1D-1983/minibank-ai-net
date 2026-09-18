namespace MiniBank.AI.Models;

public record ToolResult(
    bool Success,
    string? UserFacingMessage,   // what the LLM can safely paraphrase/relay
    string? ErrorCode,
    bool Retryable,
    object? Data = null);