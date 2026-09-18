using MiniBank.AI.Auth;
using System;
using System.Threading.Tasks;

namespace MiniBank.AI.Tests;

/// <summary>
/// Non-Ollama tests for the authentication service.
/// </summary>
public sealed class AuthenticationTests
{
    [Fact]
    public async Task Authenticate_ValidCredentials_ReturnsToken()
    {
        var authService = new InMemoryAuthenticationService();

        var token = await authService.AuthenticateAsync("john", "password");

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task Authenticate_InvalidUsername_ReturnsNull()
    {
        var authService = new InMemoryAuthenticationService();

        var token = await authService.AuthenticateAsync("unknown", "password");

        Assert.Null(token);
    }

    [Fact]
    public async Task Authenticate_InvalidPassword_ReturnsNull()
    {
        var authService = new InMemoryAuthenticationService();

        var token = await authService.AuthenticateAsync("john", "wrongpassword");

        Assert.Null(token);
    }

    [Fact]
    public async Task Authenticate_EmptyUsername_ReturnsNull()
    {
        var authService = new InMemoryAuthenticationService();

        var token = await authService.AuthenticateAsync("", "password");

        Assert.Null(token);
    }

    [Fact]
    public async Task Authenticate_EmptyPassword_ReturnsNull()
    {
        var authService = new InMemoryAuthenticationService();

        var token = await authService.AuthenticateAsync("john", "");

        Assert.Null(token);
    }

    [Fact]
    public async Task ValidateToken_ValidToken_ReturnsPrincipal()
    {
        var authService = new InMemoryAuthenticationService();
        var token = await authService.AuthenticateAsync("john", "password");

        var principal = await authService.ValidateTokenAsync(token!);

        Assert.NotNull(principal);
        Assert.Equal("John Smith", principal.Owner);
    }

    [Fact]
    public async Task ValidateToken_InvalidToken_ReturnsNull()
    {
        var authService = new InMemoryAuthenticationService();

        var principal = await authService.ValidateTokenAsync("invalid-token");

        Assert.Null(principal);
    }

    [Fact]
    public async Task ValidateToken_EmptyToken_ReturnsNull()
    {
        var authService = new InMemoryAuthenticationService();

        var principal = await authService.ValidateTokenAsync("");

        Assert.Null(principal);
    }

    [Theory]
    [InlineData("alice", "Alice Example")]
    [InlineData("john", "John Smith")]
    [InlineData("jane", "Jane Doe")]
    public async Task Authenticate_DemoUsers_ReturnCorrectOwners(string username, string expectedOwner)
    {
        var authService = new InMemoryAuthenticationService();

        var token = await authService.AuthenticateAsync(username, "password");
        var principal = await authService.ValidateTokenAsync(token!);

        Assert.NotNull(principal);
        Assert.Equal(expectedOwner, principal.Owner);
    }

    [Fact]
    public async Task Authenticate_CaseInsensitiveUsername()
    {
        var authService = new InMemoryAuthenticationService();

        var token1 = await authService.AuthenticateAsync("JOHN", "password");
        var token2 = await authService.AuthenticateAsync("John", "password");
        var token3 = await authService.AuthenticateAsync("john", "password");

        Assert.NotNull(token1);
        Assert.NotNull(token2);
        Assert.NotNull(token3);
    }

    [Fact]
    public void CustomerPrincipal_RequiresNonEmptyOwner()
    {
        Assert.Throws<ArgumentException>(() => new CustomerPrincipal(""));
        Assert.Throws<ArgumentException>(() => new CustomerPrincipal("   "));
        Assert.Throws<ArgumentNullException>(() => new CustomerPrincipal(null!));
    }
}
