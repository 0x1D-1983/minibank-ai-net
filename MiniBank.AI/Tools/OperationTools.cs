using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Banking.Services;
using MiniBank.AI.Models;
using Microsoft.Extensions.Logging;
using MiniBank.Domain.Exceptions;
using System;

namespace MiniBank.AI.Tools;

/// <summary>
/// Write operations. These change balances and must only run after approval.
/// </summary>
public sealed class OperationTools
{
    private readonly AuthorizedBank _bank;
    private readonly ILogger<OperationTools> _logger;

    public OperationTools(AuthorizedBank bank, ILogger<OperationTools> logger)
    {
        _bank = bank;
        _logger = logger;
    }

    [Description("Deposit money into an account. This changes the balance.")]
    public async Task<ToolResult> DepositAsync(
        [Description("The account that receives the deposit.")] long accountNumber,
        [Description("The amount to deposit, in GBP.")] decimal amount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _bank.DepositAsync(accountNumber, amount, cancellationToken);
            return new ToolResult(true, $"Deposited {Format(amount)} into account {accountNumber}.", "SUCCESS", false);
        }
        catch (BankDomainException ex)
        {
            return new ToolResult(false, ex.Message, ex.ErrorCode, ex.IsRetryable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected failure depositing into {AccountNumber}", accountNumber);
            return new ToolResult(false,
                "The deposit could not be completed due to an internal error.",
                "INTERNAL_ERROR", Retryable: false);
        }
    }

    [Description("Withdraw money from an account. This changes the balance.")]
    public async Task<ToolResult> WithdrawAsync(
        [Description("The account to withdraw from.")] long accountNumber,
        [Description("The amount to withdraw, in GBP.")] decimal amount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _bank.WithdrawAsync(accountNumber, amount, cancellationToken);
            return new ToolResult(true, $"Withdrew {Format(amount)} from account {accountNumber}.", "SUCCESS", false);
        }
        catch (BankDomainException ex)
        {
            return new ToolResult(false, ex.Message, ex.ErrorCode, ex.IsRetryable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected failure withdrawing from {AccountNumber}", accountNumber);
            return new ToolResult(false,
                "The withdrawal could not be completed due to an internal error.",
                "INTERNAL_ERROR", Retryable: false);
        }
    }

    [Description("Transfer money from one account to another. This changes both balances.")]
    public async Task<ToolResult> TransferAsync(
        [Description("The account to send money from.")] long fromAccountNumber,
        [Description("The account to send money to.")] long toAccountNumber,
        [Description("The amount to transfer, in GBP.")] decimal amount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _bank.TransferAsync(fromAccountNumber, toAccountNumber, amount, cancellationToken);
            return new ToolResult(
                true,
                $"Transferred {Format(amount)} from account {fromAccountNumber} to account {toAccountNumber}.",
                "SUCCESS",
                false);
        }
        catch (BankDomainException ex)
        {
            return new ToolResult(false, ex.Message, ex.ErrorCode, ex.IsRetryable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Unexpected failure transferring from {FromAccountNumber} to {ToAccountNumber}",
                fromAccountNumber,
                toAccountNumber);
            return new ToolResult(false,
                "The transfer could not be completed due to an internal error.",
                "INTERNAL_ERROR", Retryable: false);
        }
    }

    private static string Format(decimal amount)
        => amount.ToString("C", CultureInfo.GetCultureInfo("en-GB"));
}
