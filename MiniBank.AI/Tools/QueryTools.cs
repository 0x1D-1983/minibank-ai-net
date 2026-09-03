using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Agents;

namespace MiniBank.AI.Tools;

/// <summary>
/// Read-only banking tools. These never change balances.
/// </summary>
public static class QueryTools
{
    public static List<AITool> Create(
        AccountTools accountTools,
        CustomerTools customerTools,
        TransactionTools transactionTools,
        ILogger toolLogger)
        =>
        [
            MiniBankChat.Tool(accountTools.GetBalanceAsync, "get_balance", toolLogger),
            MiniBankChat.Tool(accountTools.FindAccountsByOwnerAsync, "find_accounts_by_owner", toolLogger),
            MiniBankChat.Tool(accountTools.GetTotalValueAsync, "get_total_value", toolLogger),
            MiniBankChat.Tool(accountTools.GetHighestBalanceAccountAsync, "get_highest_balance_account", toolLogger),
            MiniBankChat.Tool(customerTools.GetOwnerTotalBalanceAsync, "get_owner_total_balance", toolLogger),
            MiniBankChat.Tool(customerTools.CountDepositsByOwnerAsync, "count_deposits_by_owner", toolLogger),
            MiniBankChat.Tool(transactionTools.GetDepositsAsync, "get_deposits", toolLogger),
            MiniBankChat.Tool(transactionTools.GetAccountHistoryAsync, "get_account_history", toolLogger)
        ];
}
