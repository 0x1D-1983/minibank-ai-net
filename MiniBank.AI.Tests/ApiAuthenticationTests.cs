using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MiniBank.AI.Tests;

public sealed class ApiAuthenticationTests
{
    [Fact]
    public async Task HealthIsAnonymous()
    {
        using var factory = new MiniBankApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChatWithoutBearerTokenReturnsUnauthorized()
    {
        using var factory = new MiniBankApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/chat",
            new { question = "What is the balance of account 10001?" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginReturnsTokenForDemoCustomer()
    {
        using var factory = new MiniBankApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/login",
            new { username = "john", password = "john" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("John Smith", body.RootElement.GetProperty("customer").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task LoginRejectsInvalidCredentials()
    {
        using var factory = new MiniBankApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/login",
            new { username = "john", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class MiniBankApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tracing:Enabled"] = "false"
                });
            });
        }
    }
}
