using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Banking.Repositories;
using MiniBank.Domain.Exceptions;
using MiniBank.Domain.Models;

namespace Banking.Services;

/// <summary>
/// A customer-scoped wrapper around <see cref="Bank"/> that enforces ownership
/// rules. All lookups and mutations are restricted to accounts owned by the
/// current customer. Unauthorized access is reported as "account not found"
/// to avoid leaking information about other customers.
/// </summary>
public sealed class AuthorizedBank
{
    private readonly Bank _bank;
    private readonly string _currentOwner;

    public AuthorizedBank(Bank bank, string currentOwner)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentOwner);
        _bank = bank;
        _currentOwner = currentOwner;
    }

    /// <summary>
    /// The name of the current authenticated customer.
    /// </summary>
    public string CurrentOwner => _currentOwner;

    /// <summary>
    /// Finds an account if it belongs to the current customer.
    /// Returns null (not found) for accounts owned by others.
    /// </summary>
    public async Task<Account?> FindAccountAsync(long accountNumber)
    {
        var account = await _bank.FindAccountAsync(accountNumber);
        if (account is null)
            return null;

        if (!IsOwned(account))
            return null;

        return account;
    }

    /// <summary>
    /// Gets accounts owned by the specified owner, but only if that owner
    /// matches the current customer. Returns empty list for other owners.
    /// </summary>
    public async Task<List<Account>> GetAccountsByOwnerAsync(string owner)
    {
        if (!IsCurrentOwner(owner))
            return [];

        return await _bank.GetAccountsByOwnerAsync(owner);
    }

    /// <summary>
    /// Gets only the current customer's accounts (scoped version of GetAllAccountsAsync).
    /// </summary>
    public Task<List<Account>> GetAllAccountsAsync()
        => _bank.GetAccountsByOwnerAsync(_currentOwner);

    /// <summary>
    /// Gets the total balance across the current customer's accounts only.
    /// </summary>
    public async Task<decimal> GetTotalBalanceAsync()
    {
        var accounts = await GetAllAccountsAsync();
        var balances = await Task.WhenAll(accounts.Select(a => a.GetBalanceAsync()));
        return balances.Sum();
    }

    /// <summary>
    /// Deposits into an account if it belongs to the current customer.
    /// </summary>
    public async Task DepositAsync(long accountNumber, decimal amount)
    {
        await RequireOwnedAccountAsync(accountNumber);
        await _bank.DepositAsync(accountNumber, amount);
    }

    /// <summary>
    /// Withdraws from an account if it belongs to the current customer.
    /// </summary>
    public async Task WithdrawAsync(long accountNumber, decimal amount)
    {
        await RequireOwnedAccountAsync(accountNumber);
        await _bank.WithdrawAsync(accountNumber, amount);
    }

    /// <summary>
    /// Transfers money where the source account must belong to the current customer.
    /// The destination can be any valid account (including other customers).
    /// </summary>
    public async Task TransferAsync(long fromAccountNumber, long toAccountNumber, decimal amount)
    {
        await RequireOwnedAccountAsync(fromAccountNumber);

        var toAccount = await _bank.FindAccountAsync(toAccountNumber);
        if (toAccount is null)
            throw new AccountNotFoundException($"Account {toAccountNumber} doesn't exist.");

        await _bank.TransferAsync(fromAccountNumber, toAccountNumber, amount);
    }

    private async Task RequireOwnedAccountAsync(long accountNumber)
    {
        var account = await _bank.FindAccountAsync(accountNumber);
        if (account is null)
            throw new AccountNotFoundException($"Account {accountNumber} doesn't exist.");

        if (!IsOwned(account))
            throw new AccountNotFoundException($"Account {accountNumber} doesn't exist.");
    }

    private bool IsOwned(Account account)
        => account.Owner.Equals(_currentOwner, StringComparison.OrdinalIgnoreCase);

    private bool IsCurrentOwner(string owner)
        => owner.Equals(_currentOwner, StringComparison.OrdinalIgnoreCase);
}
