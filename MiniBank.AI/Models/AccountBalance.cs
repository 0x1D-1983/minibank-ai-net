namespace MiniBank.AI.Models;

public sealed record AccountBalance(
    long AccountNumber,
    string Owner,
    decimal Balance);
