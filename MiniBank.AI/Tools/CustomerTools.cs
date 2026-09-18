using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Banking.Services;
using MiniBank.Domain.Models;

namespace MiniBank.AI.Tools;

public sealed class CustomerTools
{
    private readonly AuthorizedBank _bank;

    public CustomerTools(AuthorizedBank bank)
    {
        _bank = bank;
    }

    public string CurrentOwner => _bank.CurrentOwner;

    [Description("Get how much money the logged-in customer has in total across all of their accounts. Use this only when they ask about their own balance (including 'my' or their own name) without an account number. Do not use this if they named a different customer.")]
    public Task<decimal> GetOwnerTotalBalanceAsync()
        => _bank.GetTotalBalanceAsync();

    [Description("Count how many deposits the logged-in customer has made across all of their accounts. Do not use this if they named a different customer.")]
    public async Task<int> CountDepositsByOwnerAsync()
    {
        var accounts = await _bank.GetAllAccountsAsync();
        return accounts.Sum(CountDeposits);
    }

    private static int CountDeposits(Account account)
        => account.History.Count(entry =>
            entry.StartsWith($"{AccountAction.Deposit}:", StringComparison.OrdinalIgnoreCase));
}
