using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiniBank.AI.Telemetry;
using MiniBank.AI.Workflows;

namespace MiniBank.AI.Agents;

/// <summary>
/// Classifies a user request as READ or WRITE. It never executes banking tools.
/// </summary>
public sealed class IntentAgent
{
    public IntentAgent(
        IChatClient? chatClient = null,
        ILoggerFactory? loggerFactory = null)
    {
        loggerFactory ??= NullLoggerFactory.Instance;
        var toolLogger = loggerFactory.CreateLogger<TracingAIFunction>();
        chatClient = MiniBankChat.Create(chatClient, loggerFactory);

        var routing = new IntentRouting();
        List<AITool> tools =
        [
            MiniBankChat.Tool(routing.ClassifyQuery, "classify_query", toolLogger),
            MiniBankChat.Tool(routing.ClassifyDeposit, "classify_deposit", toolLogger),
            MiniBankChat.Tool(routing.ClassifyWithdraw, "classify_withdraw", toolLogger),
            MiniBankChat.Tool(routing.ClassifyTransfer, "classify_transfer", toolLogger)
        ];

        var agent = chatClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "IntentAgent",
                Description = "Routes MiniBank requests to query or write handling.",
                ChatOptions = new ChatOptions
                {
                    Instructions =
                        """
                        You classify MiniBank requests. Call exactly one tool. Do not answer the banking question.

                        - classify_query: balances, accounts, history, totals, listings, "how many deposits".
                          Listing deposits already made is a query, not classify_deposit.
                        - classify_deposit: the user wants to put money into an account now.
                        - classify_withdraw: the user wants to take money out of an account now.
                        - classify_transfer: the user wants to move money between two accounts now.

                        Never invent account numbers or amounts. Only pass values the user supplied.
                        """,
                    Temperature = 0f,
                    Tools = tools
                }
            },
            loggerFactory: loggerFactory);

        Agent = MiniBankChat.Instrument(agent, loggerFactory);
    }

    public AIAgent Agent { get; }
}
