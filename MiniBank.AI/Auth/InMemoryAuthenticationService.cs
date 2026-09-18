using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MiniBank.AI.Auth;

/// <summary>
/// In-memory mock identity server for demo purposes. Holds three demo users
/// matching the seeded accounts. A later issue can swap this for a real
/// identity server (Entra, IdentityServer, Keycloak) without changing the
/// interface or callers.
/// </summary>
public sealed class InMemoryAuthenticationService : IAuthenticationService
{
    private readonly Dictionary<string, DemoUser> _users;
    private readonly ConcurrentDictionary<string, CustomerPrincipal> _tokens = new();

    public InMemoryAuthenticationService()
    {
        _users = new Dictionary<string, DemoUser>(StringComparer.OrdinalIgnoreCase)
        {
            ["alice"] = new DemoUser("alice", "password", "Alice Example"),
            ["john"] = new DemoUser("john", "password", "John Smith"),
            ["jane"] = new DemoUser("jane", "password", "Jane Doe")
        };
    }

    public Task<string?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Task.FromResult<string?>(null);

        if (!_users.TryGetValue(username, out var user))
            return Task.FromResult<string?>(null);

        if (!user.Password.Equals(password, StringComparison.Ordinal))
            return Task.FromResult<string?>(null);

        var token = GenerateToken();
        var principal = new CustomerPrincipal(user.Owner);
        _tokens[token] = principal;

        return Task.FromResult<string?>(token);
    }

    public Task<CustomerPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Task.FromResult<CustomerPrincipal?>(null);

        _tokens.TryGetValue(token, out var principal);
        return Task.FromResult(principal);
    }

    private static string GenerateToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private sealed record DemoUser(string Username, string Password, string Owner);
}
