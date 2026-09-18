namespace MiniBank.AI.Models;

public sealed record OwnerTotal(string Owner, decimal Total);

public sealed record OwnerDepositCount(string Owner, int DepositCount);
