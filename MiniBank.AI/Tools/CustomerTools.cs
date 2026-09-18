using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Banking.Services;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Models;
using MiniBank.Domain.Exceptions;
using MiniBank.Domain.Models;

namespace MiniBank.AI.Tools;

public sealed class CustomerTools
{
    private readonly AuthorizedBank _bank;
    private readonly ILogger<CustomerTools> _logger;

    public CustomerTools(AuthorizedBank bank, ILogger<CustomerTools> logger)
    {
        _bank = bank;
        _logger = logger;
    }

    public string CurrentOwner => _bank.CurrentOwner;

    [Description("Get the logged-in customer's name and their total balance across all of their accounts. The Owner field is whose money this is. Use only when they ask about their own balance. Do not use this if they named a different customer.")]
    public async Task<ToolResult> GetOwnerTotalBalanceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var total = await _bank.GetTotalBalanceAsync(cancellationToken);
            return new ToolResult(true, null, "SUCCESS", false, new OwnerTotal(_bank.CurrentOwner, total));
        }
        catch (BankDomainException ex)
        {
            return new ToolResult(false, ex.Message, ex.ErrorCode, ex.IsRetryable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected failure getting total balance for {Owner}", _bank.CurrentOwner);
            return new ToolResult(false,
                "The total balance could not be retrieved due to an internal error.",
                "INTERNAL_ERROR", Retryable: false);
        }
    }

    [Description("Count how many deposits the logged-in customer has made. The Owner field is whose deposits these are. Do not use this if they named a different customer.")]
    public async Task<ToolResult> CountDepositsByOwnerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var accounts = await _bank.GetAllAccountsAsync(cancellationToken);
            return new ToolResult(
                true,
                null,
                "SUCCESS",
                false,
                new OwnerDepositCount(_bank.CurrentOwner, accounts.Sum(CountDeposits)));
        }
        catch (BankDomainException ex)
        {
            return new ToolResult(false, ex.Message, ex.ErrorCode, ex.IsRetryable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected failure counting deposits for {Owner}", _bank.CurrentOwner);
            return new ToolResult(false,
                "The deposit count could not be retrieved due to an internal error.",
                "INTERNAL_ERROR", Retryable: false);
        }
    }

    private static int CountDeposits(Account account)
        => account.History.Count(entry =>
            entry.StartsWith($"{AccountAction.Deposit}:", StringComparison.OrdinalIgnoreCase));
}
