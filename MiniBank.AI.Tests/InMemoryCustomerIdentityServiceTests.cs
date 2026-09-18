using System.Threading.Tasks;
using MiniBank.AI.Auth;

namespace MiniBank.AI.Tests;

public sealed class InMemoryCustomerIdentityServiceTests
{
    [Fact]
    public async Task SignInIssuesTokenForDemoCustomer()
    {
        var identity = new InMemoryCustomerIdentityService();

        var result = await identity.SignInAsync("john", "john");

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal("John Smith", result.Principal.Owner);
        Assert.Equal(result.Principal, await identity.ValidateTokenAsync(result.Token));
    }

    [Theory]
    [InlineData("john", "wrong")]
    [InlineData("unknown", "john")]
    [InlineData("", "john")]
    public async Task SignInRejectsInvalidCredentials(string username, string password)
    {
        var identity = new InMemoryCustomerIdentityService();

        var result = await identity.SignInAsync(username, password);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing")]
    public async Task ValidateTokenRejectsMissingToken(string token)
    {
        var identity = new InMemoryCustomerIdentityService();

        var principal = await identity.ValidateTokenAsync(token);

        Assert.Null(principal);
    }
}
