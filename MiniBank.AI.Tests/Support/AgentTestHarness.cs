using MiniBank.Domain.Models;
using Banking.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MiniBank.AI.Agents;
using MiniBank.AI.Tools;
using MiniBank.AI.Workflows;
using OllamaSharp;
using System.Threading.Tasks;
using System.Net.Http;
using System;

namespace MiniBank.AI.Tests.Support;

internal sealed class AgentTestHarness
{
    private const string Endpoint = "http://localhost:11434";
    private const string ModelName = "qwen2.5:1.5b-instruct";

    public RecordingAccountRepository Repository { get; }
    public RecordingChatClient Chat { get; }
    public Bank Bank { get; }
    public AIAgent Agent { get; }
    public BankingWorkflow? Workflow { get; }
    public RecordingWriteApprover? Approver { get; }

    private AgentTestHarness(
        RecordingAccountRepository repository,
        RecordingChatClient chat,
        Bank bank,
        AIAgent agent,
        BankingWorkflow? workflow,
        RecordingWriteApprover? approver)
    {
        Repository = repository;
        Chat = chat;
        Bank = bank;
        Agent = agent;
        Workflow = workflow;
        Approver = approver;
    }

    public static Task<AgentTestHarness> CreateAsync()
        => CreateCoreAsync(includeWorkflow: false);

    public static Task<AgentTestHarness> CreateWorkflowAsync(bool approveWrites = true)
        => CreateCoreAsync(includeWorkflow: true, approveWrites);

    public async Task<string> AskAsync(string question)
    {
        if (Workflow is not null)
            return await Workflow.RunAsync(question);

        var response = await Agent.RunAsync(question);
        return response.Text;
    }

    public Task<WorkflowRunResult> AskDetailedAsync(string question)
    {
        Assert.NotNull(Workflow);
        return Workflow.RunDetailedAsync(question);
    }

    private static async Task<AgentTestHarness> CreateCoreAsync(bool includeWorkflow, bool approveWrites = true)
    {
        await EnsureOllamaAsync();

        var repository = new RecordingAccountRepository();
        var bank = new Bank(repository, new NoOpAuditLogger());
        await SeedAsync(bank);
        repository.ClearRecordings();

        IChatClient ollama = new OllamaApiClient(new Uri(Endpoint), ModelName);
        var chat = new RecordingChatClient(ollama);

        var accountTools = new AccountTools(bank);
        var customerTools = new CustomerTools(bank);
        var transactionTools = new TransactionTools(bank);
        var agent = new BankingAgent(accountTools, customerTools, transactionTools, chatClient: chat).Agent;

        BankingWorkflow? workflow = null;
        RecordingWriteApprover? approver = null;
        if (includeWorkflow)
        {
            approver = new RecordingWriteApprover(approveWrites);
            workflow = BankingWorkflow.Create(
                accountTools,
                customerTools,
                transactionTools,
                new OperationTools(bank),
                chatClient: chat,
                approver: approver);
        }

        return new AgentTestHarness(repository, chat, bank, agent, workflow, approver);
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
