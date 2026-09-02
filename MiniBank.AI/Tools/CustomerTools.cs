using System.ComponentModel;
using Banking.Domain.Models;
using Banking.Services;

namespace MiniBank.AI.Tools;

public sealed class CustomerTools
{
    private readonly Bank _bank;

    public CustomerTools(Bank bank)
    {
        _bank = bank;
    }

    [Description("Get how much money a customer has in total across all of their accounts.")]
    public async Task<decimal> GetOwnerTotalBalanceAsync(
        [Description("The customer's full name.")] string owner)
    {
        var accounts = await _bank.GetAccountsByOwnerAsync(owner);
        var balances = await Task.WhenAll(accounts.Select(account => account.GetBalanceAsync()));
        return balances.Sum();
    }

    [Description("Count how many deposits a customer has made across all of their accounts.")]
    public async Task<int> CountDepositsByOwnerAsync(
        [Description("The customer's full name.")] string owner)
    {
        var accounts = await _bank.GetAccountsByOwnerAsync(owner);
        return accounts.Sum(CountDeposits);
    }

    private static int CountDeposits(Account account)
        => account.History.Count(entry =>
            entry.StartsWith($"{AccountAction.Deposit}:", StringComparison.OrdinalIgnoreCase));
}
