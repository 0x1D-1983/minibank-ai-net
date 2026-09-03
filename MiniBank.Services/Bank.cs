using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Banking.Repositories;
using MiniBank.Domain.Exceptions;
using MiniBank.Domain.Models;

namespace Banking.Services
{
    public class Bank
    {
        private readonly IAccountRepository _accounts;
        private readonly IAuditLogger _logger;

        public Bank(IAccountRepository accountRepo, IAuditLogger logger)
        {
            _accounts = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task AddAccountAsync(Account account) =>
            _accounts.AddAccountAsync(account);

        public Task<Account?> FindAccountAsync(long accountNumber) =>
            _accounts.FindByIdAsync(accountNumber);

        public async Task<decimal> GetTotalBalanceAsync()
        {
            var accounts = await _accounts.AllAsync();
            var balances = await Task.WhenAll(accounts.Select(a => a.GetBalanceAsync()));
            return balances.Sum();
        }

        public Task<List<Account>> GetAccountsByOwnerAsync(string owner) =>
            _accounts.FindByOwnerAsync(owner);

        public Task<List<Account>> GetAllAccountsAsync() =>
            _accounts.AllAsync();

        /// <summary>
        /// Deposit amount into an account.
        /// </summary>
        public async Task DepositAsync(long accountNumber, decimal amount)
        {
            var account = await FindAccountAsync(accountNumber);
            if (account is null)
                throw new AccountNotFoundException("Account doesn't exist");

            // Account.DepositAsync owns the lock — do not nest WaitAsync here.
            await account.DepositAsync(amount);

            await _accounts.UpdateAccountAsync(account);
            await _logger.LogAsync(accountNumber, AccountAction.Deposit, amount);
        }

        /// <summary>
        /// Withdraw amount from an account.
        /// </summary>
        public async Task WithdrawAsync(long accountNumber, decimal amount)
        {
            var account = await FindAccountAsync(accountNumber);
            if (account is null)
                throw new AccountNotFoundException("Account doesn't exist");

            // Account.WithdrawAsync owns the lock — do not nest WaitAsync here.
            await account.WithdrawAsync(amount);

            await _accounts.UpdateAccountAsync(account);
            await _logger.LogAsync(accountNumber, AccountAction.Withdraw, amount);
        }

        public async Task TransferAsync(long fromAccountNumber, long toAccountNumber, decimal amount)
        {
            if (fromAccountNumber == toAccountNumber)
                throw new ArgumentException("Cannot transfer to the same account", nameof(toAccountNumber));

            var fromAccount = await FindAccountAsync(fromAccountNumber);
            if (fromAccount is null)
                throw new AccountNotFoundException("Source account doesn't exist");

            var toAccount = await FindAccountAsync(toAccountNumber);
            if (toAccount is null)
                throw new AccountNotFoundException("Destination account doesn't exist");

            using (var locks = await Account.LockAllAsync(fromAccount, toAccount))
            {
                locks[fromAccount].Withdraw(amount);
                locks[toAccount].Deposit(amount);
            }

            await _accounts.UpdateAccountAsync(fromAccount);
            await _accounts.UpdateAccountAsync(toAccount);
            await _logger.LogAsync(fromAccountNumber, AccountAction.Transfer, -amount);
            await _logger.LogAsync(toAccountNumber, AccountAction.Transfer, amount);
        }
    }
}
