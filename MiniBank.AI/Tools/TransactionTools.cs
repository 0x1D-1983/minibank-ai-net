using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Banking.Services;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Models;
using MiniBank.Domain.Exceptions;
using MiniBank.Domain.Models;

namespace MiniBank.AI.Tools;

public sealed class TransactionTools
{
    private readonly AuthorizedBank _bank;
    private readonly ILogger<TransactionTools> _logger;

    public TransactionTools(AuthorizedBank bank, ILogger<TransactionTools> logger)
    {
        _bank = bank;
        _logger = logger;
    }

    [Description("List only the deposits made to a specific account. Do not use this when the user asks for full history or every transaction.")]
    public async Task<ToolResult> GetDepositsAsync(
        [Description("The account number whose deposits should be listed.")] long accountNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var account = await RequireAccountAsync(accountNumber, cancellationToken);
            var deposits = GetHistory(account)
                .Where(entry => entry.Action.Equals(nameof(AccountAction.Deposit), StringComparison.OrdinalIgnoreCase))
                .ToList();
            return new ToolResult(true, null, "SUCCESS", false, deposits);
        }
        catch (BankDomainException ex)
        {
            return new ToolResult(false, ex.Message, ex.ErrorCode, ex.IsRetryable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected failure listing deposits for {AccountNumber}", accountNumber);
            return new ToolResult(false,
                "The deposits could not be listed due to an internal error.",
                "INTERNAL_ERROR", Retryable: false);
        }
    }

    [Description("List every transaction on a specific account, including deposits and other actions. Use this when the user asks for history, everything that happened, or all transactions.")]
    public async Task<ToolResult> GetAccountHistoryAsync(
        [Description("The account number whose history should be listed.")] long accountNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var account = await RequireAccountAsync(accountNumber, cancellationToken);
            return new ToolResult(true, null, "SUCCESS", false, GetHistory(account));
        }
        catch (BankDomainException ex)
        {
            return new ToolResult(false, ex.Message, ex.ErrorCode, ex.IsRetryable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected failure listing history for {AccountNumber}", accountNumber);
            return new ToolResult(false,
                "The account history could not be listed due to an internal error.",
                "INTERNAL_ERROR", Retryable: false);
        }
    }

    private async Task<Account> RequireAccountAsync(long accountNumber, CancellationToken cancellationToken)
    {
        var account = await _bank.FindAccountAsync(accountNumber, cancellationToken);
        if (account is null)
            throw new AccountNotFoundException($"Account {accountNumber} doesn't exist.");

        return account;
    }

    private static List<TransactionSummary> GetHistory(Account account)
        => account.History.Select(entry => Parse(account, entry)).ToList();

    private static TransactionSummary Parse(Account account, string entry)
    {
        var separator = entry.IndexOf(':');
        var action = separator >= 0 ? entry[..separator].Trim() : entry;
        var amountText = separator >= 0 ? entry[(separator + 1)..].Trim() : "0";
        decimal.TryParse(
            amountText,
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var amount);

        return new TransactionSummary(
            account.AccountNumber,
            account.Owner,
            action,
            amount,
            entry);
    }
}
