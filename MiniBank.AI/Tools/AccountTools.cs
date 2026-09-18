using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Banking.Services;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Models;
using MiniBank.Domain.Exceptions;
using MiniBank.Domain.Models;

namespace MiniBank.AI.Tools;

public sealed class AccountTools
{
    private readonly AuthorizedBank _bank;
    private readonly ILogger<AccountTools> _logger;

    public AccountTools(AuthorizedBank bank, ILogger<AccountTools> logger)
    {
        _bank = bank;
        _logger = logger;
    }

    [Description("List all accounts owned by the logged-in customer, including each account number and balance. Use this for any balance question, including a specific account: pick the matching account from the list. Never invent an account number.")]
    public async Task<ToolResult> FindAccountsByOwnerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var accounts = await _bank.GetAllAccountsAsync(cancellationToken);
            return new ToolResult(true, null, "SUCCESS", false, await ToBalancesAsync(accounts));
        }
        catch (BankDomainException ex)
        {
            return new ToolResult(false, ex.Message, ex.ErrorCode, ex.IsRetryable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected failure listing accounts for {Owner}", _bank.CurrentOwner);
            return new ToolResult(false,
                "The accounts could not be listed due to an internal error.",
                "INTERNAL_ERROR", Retryable: false);
        }
    }

    private static async Task<List<AccountBalance>> ToBalancesAsync(IEnumerable<Account> accounts)
    {
        var balances = new List<AccountBalance>();
        foreach (var account in accounts)
            balances.Add(await ToBalanceAsync(account));

        return balances;
    }

    private static async Task<AccountBalance> ToBalanceAsync(Account account)
        => new(account.AccountNumber, account.Owner, await account.GetBalanceAsync());
}
