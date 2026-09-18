using System;

namespace MiniBank.Auth;

/// <summary>
/// Represents an authenticated customer. <see cref="Owner"/> matches the
/// account owner string used by seed data (for example "John Smith").
/// </summary>
public sealed class CustomerPrincipal
{
    public CustomerPrincipal(string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        Owner = owner;
    }

    /// <summary>
    /// The customer's name, matching <c>Account.Owner</c>.
    /// </summary>
    public string Owner { get; }
}
