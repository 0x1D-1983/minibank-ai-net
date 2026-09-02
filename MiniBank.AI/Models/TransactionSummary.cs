namespace MiniBank.AI.Models;

public sealed record TransactionSummary(
    long AccountNumber,
    string Owner,
    string Action,
    decimal Amount,
    string Description);
