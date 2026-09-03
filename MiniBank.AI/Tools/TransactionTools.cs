using System.ComponentModel;
using System.Globalization;
using Banking.Domain.Exceptions;
using Banking.Domain.Models;
using Banking.Services;
using MiniBank.AI.Models;

namespace MiniBank.AI.Tools;

public sealed class TransactionTools
{
    private readonly Bank _bank;

    public TransactionTools(Bank bank)
    {
        _bank = bank;
    }

    [Description("List only the deposits made to a specific account. Do not use this when the user asks for full history or every transaction.")]
    public async Task<List<TransactionSummary>> GetDepositsAsync(
        [Description("The account number whose deposits should be listed.")] long accountNumber)
    {
        var account = await RequireAccountAsync(accountNumber);
        return GetHistory(account)
            .Where(entry => entry.Action.Equals(nameof(AccountAction.Deposit), StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    [Description("List every transaction on a specific account, including deposits and other actions. Use this when the user asks for history, everything that happened, or all transactions.")]
    public async Task<List<TransactionSummary>> GetAccountHistoryAsync(
        [Description("The account number whose history should be listed.")] long accountNumber)
    {
        var account = await RequireAccountAsync(accountNumber);
        return GetHistory(account);
    }

    private async Task<Account> RequireAccountAsync(long accountNumber)
    {
        var account = await _bank.FindAccountAsync(accountNumber);
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
