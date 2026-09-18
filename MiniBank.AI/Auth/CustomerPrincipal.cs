using System;

namespace MiniBank.AI.Auth;

/// <summary>
/// Represents an authenticated customer. The <see cref="Owner"/> property
/// matches the <see cref="MiniBank.Domain.Models.Account.Owner"/> string
/// used by seed data (e.g. "John Smith").
/// </summary>
public sealed class CustomerPrincipal
{
    public CustomerPrincipal(string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        Owner = owner;
    }

    /// <summary>
    /// The customer's name, matching Account.Owner.
    /// </summary>
    public string Owner { get; }
}
