using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MiniBank.Domain.Exceptions;
using MiniBank.Domain.Models;

namespace Banking.Services;

public sealed class CustomerBank
{
    private readonly Bank _bank;

    internal CustomerBank(Bank bank, string customer)
    {
        _bank = bank ?? throw new ArgumentNullException(nameof(bank));
        if (string.IsNullOrWhiteSpace(customer))
            throw new ArgumentException("Customer is required.", nameof(customer));

        Customer = customer.Trim();
    }

    public string Customer { get; }

    public async Task<Account?> FindAccountAsync(long accountNumber)
    {
        var account = await _bank.FindAccountAsync(accountNumber);
        return IsCurrentCustomer(account) ? account : null;
    }

    public async Task<decimal> GetTotalBalanceAsync()
    {
        var accounts = await GetAllAccountsAsync();
        var balances = await Task.WhenAll(accounts.Select(account => account.GetBalanceAsync()));
        return balances.Sum();
    }

    public Task<List<Account>> GetAccountsByOwnerAsync(string owner)
        => IsCurrentCustomer(owner)
            ? _bank.GetAccountsByOwnerAsync(Customer)
            : Task.FromResult(new List<Account>());

    public Task<List<Account>> GetAllAccountsAsync()
        => _bank.GetAccountsByOwnerAsync(Customer);

    public async Task DepositAsync(long accountNumber, decimal amount)
    {
        await RequireCurrentCustomerAccountAsync(accountNumber, "Account doesn't exist");
        await _bank.DepositAsync(accountNumber, amount);
    }

    public async Task WithdrawAsync(long accountNumber, decimal amount)
    {
        await RequireCurrentCustomerAccountAsync(accountNumber, "Account doesn't exist");
        await _bank.WithdrawAsync(accountNumber, amount);
    }

    public async Task TransferAsync(long fromAccountNumber, long toAccountNumber, decimal amount)
    {
        await RequireCurrentCustomerAccountAsync(fromAccountNumber, "Source account doesn't exist");
        await _bank.TransferAsync(fromAccountNumber, toAccountNumber, amount);
    }

    private async Task RequireCurrentCustomerAccountAsync(long accountNumber, string message)
    {
        var account = await FindAccountAsync(accountNumber);
        if (account is null)
            throw new AccountNotFoundException(message);
    }

    private bool IsCurrentCustomer(Account? account)
        => account is not null && IsCurrentCustomer(account.Owner);

    private bool IsCurrentCustomer(string owner)
        => Customer.Equals(owner, StringComparison.OrdinalIgnoreCase);
}
