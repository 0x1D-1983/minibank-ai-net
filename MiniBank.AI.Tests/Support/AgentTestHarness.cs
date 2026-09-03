using Banking.Domain.Models;
using Banking.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MiniBank.AI.Agents;
using MiniBank.AI.Tools;
using OllamaSharp;

namespace MiniBank.AI.Tests.Support;

internal sealed class AgentTestHarness
{
    private const string Endpoint = "http://localhost:11434";
    private const string ModelName = "qwen2.5:1.5b-instruct";

    public RecordingAccountRepository Repository { get; }
    public RecordingChatClient Chat { get; }
    public AIAgent Agent { get; }

    private AgentTestHarness(
        RecordingAccountRepository repository,
        RecordingChatClient chat,
        AIAgent agent)
    {
        Repository = repository;
        Chat = chat;
        Agent = agent;
    }

    public static async Task<AgentTestHarness> CreateAsync()
    {
        await EnsureOllamaAsync();

        var repository = new RecordingAccountRepository();
        var bank = new Bank(repository, new NoOpAuditLogger());
        await SeedAsync(bank);
        repository.ClearRecordings();

        IChatClient ollama = new OllamaApiClient(new Uri(Endpoint), ModelName);
        var chat = new RecordingChatClient(ollama);

        var agent = new BankingAgent(
            new AccountTools(bank),
            new CustomerTools(bank),
            new TransactionTools(bank),
            chatClient: chat).Agent;

        return new AgentTestHarness(repository, chat, agent);
    }

    public async Task<string> AskAsync(string question)
    {
        var response = await Agent.RunAsync(question);
        return response.Text;
    }

    private static async Task EnsureOllamaAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var response = await http.GetAsync(new Uri($"{Endpoint}/api/tags"));
            if (!response.IsSuccessStatusCode)
            {
                Assert.Fail(
                    $"Ollama responded {response.StatusCode} at {Endpoint}. " +
                    $"Start Ollama and pull {ModelName}.");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Assert.Fail(
                $"Ollama is not reachable at {Endpoint}. " +
                $"Start Ollama and pull {ModelName}. {ex.Message}");
        }
    }

    private static async Task SeedAsync(Bank bank)
    {
        await bank.AddAccountAsync(new CurrentAccount("Alice Example", 1234567890, overdraftLimit: 250m));
        await bank.DepositAsync(1234567890, 2_450.00m);

        await bank.AddAccountAsync(new CurrentAccount("John Smith", 10001, overdraftLimit: 500m));
        await bank.DepositAsync(10001, 1_532.42m);

        await bank.AddAccountAsync(new SavingsAccount("John Smith", 10002, interestRate: 0.02m));
        await bank.DepositAsync(10002, 800.00m);

        await bank.AddAccountAsync(new CurrentAccount("Jane Doe", 20001, overdraftLimit: 0m));
        await bank.DepositAsync(20001, 5_000.00m);
    }
}
