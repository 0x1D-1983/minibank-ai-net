using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiniBank.AI.Telemetry;
using MiniBank.AI.Workflows;

namespace MiniBank.AI.Agents;

/// <summary>
/// Classifies a user request as READ or WRITE. It never executes banking tools
/// and it never asks the model to answer the question — one tool call is the result.
/// </summary>
public sealed class IntentAgent
{
    private readonly IChatClient _chatClient;
    private readonly ChatOptions _options;
    private readonly ILogger _logger;

    public IntentAgent(
        IChatClient? chatClient = null,
        ILoggerFactory? loggerFactory = null)
    {
        loggerFactory ??= NullLoggerFactory.Instance;
        var toolLogger = loggerFactory.CreateLogger<TracingAIFunction>();
        _logger = loggerFactory.CreateLogger<IntentAgent>();
        _chatClient = MiniBankChat.Create(chatClient, loggerFactory);

        var routing = new IntentRouting();
        _options = new ChatOptions
        {
            Instructions =
                """
                You classify MiniBank requests. Call exactly one tool. Do not answer the banking question.
                Do not invent balances or account numbers.

                - classify_query: balances, accounts, history, totals, listings, "how many deposits".
                  Listing deposits already made is a query, not classify_deposit.
                - classify_deposit: the user wants to put money into an account now.
                - classify_withdraw: the user wants to take money out of an account now.
                - classify_transfer: the user wants to move money between two accounts now.

                Only pass values the user supplied.
                """,
            Temperature = 0f,
            ToolMode = ChatToolMode.RequireAny,
            Tools =
            [
                MiniBankChat.Tool(routing.ClassifyQuery, "classify_query", toolLogger),
                MiniBankChat.Tool(routing.ClassifyDeposit, "classify_deposit", toolLogger),
                MiniBankChat.Tool(routing.ClassifyWithdraw, "classify_withdraw", toolLogger),
                MiniBankChat.Tool(routing.ClassifyTransfer, "classify_transfer", toolLogger)
            ]
        };
    }

    public async Task<BankingIntent> ClassifyAsync(string question, CancellationToken cancellationToken = default)
    {
        var response = await _chatClient.GetResponseAsync(question, _options, cancellationToken);
        var intent = IntentExecutor.Parse(response, question);
        _logger.LogInformation(
            "Intent classified as {Kind} {Operation}",
            intent.Kind,
            intent.Operation?.ToString() ?? "query");
        return intent;
    }
}
