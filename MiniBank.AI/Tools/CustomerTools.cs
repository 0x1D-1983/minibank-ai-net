using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Banking.Services;
using MiniBank.AI.Models;
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

    [Description("Get the logged-in customer's name and their total balance across all of their accounts. The Owner field is whose money this is. Use only when they ask about their own balance. Do not use this if they named a different customer.")]
    public async Task<OwnerTotal> GetOwnerTotalBalanceAsync()
    {
        var total = await _bank.GetTotalBalanceAsync();
        return new OwnerTotal(_bank.CurrentOwner, total);
    }

    [Description("Count how many deposits the logged-in customer has made. The Owner field is whose deposits these are. Do not use this if they named a different customer.")]
    public async Task<OwnerDepositCount> CountDepositsByOwnerAsync()
    {
        var accounts = await _bank.GetAllAccountsAsync();
        return new OwnerDepositCount(_bank.CurrentOwner, accounts.Sum(CountDeposits));
    }

    private static int CountDeposits(Account account)
        => account.History.Count(entry =>
            entry.StartsWith($"{AccountAction.Deposit}:", StringComparison.OrdinalIgnoreCase));
}
