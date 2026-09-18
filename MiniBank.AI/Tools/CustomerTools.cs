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

    [Description("Get how much money the current customer has in total across all of their accounts. Use this when the user asks for a balance without giving an account number.")]
    public Task<decimal> GetOwnerTotalBalanceAsync()
        => _bank.GetTotalBalanceAsync();

    [Description("Count how many deposits the current customer has made across all of their accounts.")]
    public async Task<int> CountDepositsByOwnerAsync()
    {
        var accounts = await _bank.GetAllAccountsAsync();
        return accounts.Sum(CountDeposits);
    }

    private static int CountDeposits(Account account)
        => account.History.Count(entry =>
            entry.StartsWith($"{AccountAction.Deposit}:", StringComparison.OrdinalIgnoreCase));
}
