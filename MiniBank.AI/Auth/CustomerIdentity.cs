using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MiniBank.AI.Auth;

public interface ICustomerIdentityService
{
    Task<CustomerSignInResult?> SignInAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<CustomerPrincipal?> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryCustomerIdentityService : ICustomerIdentityService
{
    public static IReadOnlyList<DemoCustomer> DemoCustomers { get; } =
    [
        new("alice", "alice", "Alice Example"),
        new("john", "john", "John Smith"),
        new("jane", "jane", "Jane Doe")
    ];

    private readonly IReadOnlyDictionary<string, DemoCustomer> _customersByUsername;
    private readonly ConcurrentDictionary<string, CustomerPrincipal> _tokens = new();

    public InMemoryCustomerIdentityService()
        : this(DemoCustomers)
    {
    }

    public InMemoryCustomerIdentityService(IEnumerable<DemoCustomer> customers)
    {
        ArgumentNullException.ThrowIfNull(customers);

        _customersByUsername = customers.ToDictionary(
            customer => customer.Username,
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<CustomerSignInResult?> SignInAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Task.FromResult<CustomerSignInResult?>(null);

        if (!_customersByUsername.TryGetValue(username.Trim(), out var customer)
            || !string.Equals(customer.Password, password, StringComparison.Ordinal))
        {
            return Task.FromResult<CustomerSignInResult?>(null);
        }

        var principal = new CustomerPrincipal(customer.Owner);
        var token = IssueToken();
        _tokens[token] = principal;

        return Task.FromResult<CustomerSignInResult?>(new CustomerSignInResult(token, principal));
    }

    public Task<CustomerPrincipal?> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(token))
            return Task.FromResult<CustomerPrincipal?>(null);

        return Task.FromResult(
            _tokens.TryGetValue(token.Trim(), out var principal) ? principal : null);
    }

    private static string IssueToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}

public sealed record CustomerPrincipal(string Owner);

public sealed record CustomerSignInResult(string Token, CustomerPrincipal Principal);

public sealed record DemoCustomer(string Username, string Password, string Owner);
