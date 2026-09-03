using System.ComponentModel;
using Banking.Domain.Exceptions;
using Banking.Domain.Models;
using Banking.Services;
using MiniBank.AI.Models;

namespace MiniBank.AI.Tools;

public sealed class AccountTools
{
    private readonly Bank _bank;

    public AccountTools(Bank bank)
    {
        _bank = bank;
    }

    [Description("Get the current balance of one account. Call only when the user supplied that account number. Do not invent an account number. If the user named a customer instead, use get_owner_total_balance.")]
    public async Task<decimal> GetBalanceAsync(
        [Description("The account number supplied by the user.")] long accountNumber)
    {
        var account = await RequireAccountAsync(accountNumber);
        return await account.GetBalanceAsync();
    }

    [Description("List all accounts owned by a customer, including each account number and balance.")]
    public async Task<List<AccountBalance>> FindAccountsByOwnerAsync(
        [Description("The customer's full name.")] string owner)
    {
        var accounts = await OwnerResolver.ResolveAsync(_bank, owner);
        return await ToBalancesAsync(accounts);
    }

    [Description("Get the total value of every account in the bank (sum of all balances).")]
    public Task<decimal> GetTotalValueAsync()
        => _bank.GetTotalBalanceAsync();

    [Description("Find the account that currently has the highest balance.")]
    public async Task<AccountBalance?> GetHighestBalanceAccountAsync()
    {
        var accounts = await _bank.GetAllAccountsAsync();
        if (accounts.Count == 0)
            return null;

        AccountBalance? highest = null;
        foreach (var account in accounts)
        {
            var snapshot = await ToBalanceAsync(account);
            if (highest is null || snapshot.Balance > highest.Balance)
                highest = snapshot;
        }

        return highest;
    }

    private async Task<Account> RequireAccountAsync(long accountNumber)
    {
        var account = await _bank.FindAccountAsync(accountNumber);
        if (account is null)
            throw new AccountNotFoundException($"Account {accountNumber} doesn't exist.");

        return account;
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
