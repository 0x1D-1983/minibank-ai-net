using System.Threading;
using System.Threading.Tasks;

namespace MiniBank.Auth;

/// <summary>
/// Replaceable seam for authentication. A later issue can swap the in-memory
/// mock for a real identity/OAuth/OIDC server without rewriting Bank
/// authorization, tools, or workflow.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a user with the given credentials and returns a token.
    /// Returns null if the credentials are invalid.
    /// </summary>
    Task<string?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a token and returns the customer principal if valid.
    /// Returns null if the token is invalid or expired.
    /// </summary>
    Task<CustomerPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}
