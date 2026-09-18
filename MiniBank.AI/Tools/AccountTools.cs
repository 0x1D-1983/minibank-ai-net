using System.ComponentModel;
using MiniBank.Domain.Models;
using Banking.Services;
using MiniBank.AI.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MiniBank.AI.Tools;

public sealed class AccountTools
{
    private readonly AuthorizedBank _bank;

    public AccountTools(AuthorizedBank bank)
    {
        _bank = bank;
    }

    [Description("List all accounts owned by the logged-in customer, including each account number and balance. Use this for any balance question, including a specific account: pick the matching account from the list. Never invent an account number.")]
    public async Task<List<AccountBalance>> FindAccountsByOwnerAsync()
    {
        var accounts = await _bank.GetAllAccountsAsync();
        return await ToBalancesAsync(accounts);
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
