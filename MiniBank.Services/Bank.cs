using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        public Task AddAccountAsync(Account account, CancellationToken cancellationToken = default) =>
            _accounts.AddAccountAsync(account, cancellationToken);

        public Task<Account?> FindAccountAsync(long accountNumber, CancellationToken cancellationToken = default) =>
            _accounts.FindByIdAsync(accountNumber, cancellationToken);

        public async Task<decimal> GetTotalBalanceAsync(CancellationToken cancellationToken = default)
        {
            var accounts = await _accounts.AllAsync(cancellationToken);
            var balances = await Task.WhenAll(accounts.Select(a => a.GetBalanceAsync()));
            return balances.Sum();
        }

        public Task<List<Account>> GetAccountsByOwnerAsync(string owner, CancellationToken cancellationToken = default) =>
            _accounts.FindByOwnerAsync(owner, cancellationToken);

        /// <summary>
        /// Deposit amount into an account.
        /// </summary>
        public async Task DepositAsync(long accountNumber, decimal amount, CancellationToken cancellationToken = default)
        {
            var account = await FindAccountAsync(accountNumber, cancellationToken);
            if (account is null)
                throw new AccountNotFoundException("Account doesn't exist");

            // Account.DepositAsync owns the lock — do not nest WaitAsync here.
            await account.DepositAsync(amount);

            await _accounts.UpdateAccountAsync(account, cancellationToken);
            await _logger.LogAsync(accountNumber, AccountAction.Deposit, amount, cancellationToken);
        }

        /// <summary>
        /// Withdraw amount from an account.
        /// </summary>
        public async Task WithdrawAsync(long accountNumber, decimal amount, CancellationToken cancellationToken = default)
        {
            var account = await FindAccountAsync(accountNumber, cancellationToken);
            if (account is null)
                throw new AccountNotFoundException("Account doesn't exist");

            // Account.WithdrawAsync owns the lock — do not nest WaitAsync here.
            await account.WithdrawAsync(amount);

            await _accounts.UpdateAccountAsync(account, cancellationToken);
            await _logger.LogAsync(accountNumber, AccountAction.Withdraw, amount, cancellationToken);
        }

        public async Task TransferAsync(
            long fromAccountNumber,
            long toAccountNumber,
            decimal amount,
            CancellationToken cancellationToken = default)
        {
            if (fromAccountNumber == toAccountNumber)
                throw new ArgumentException("Cannot transfer to the same account", nameof(toAccountNumber));

            var fromAccount = await FindAccountAsync(fromAccountNumber, cancellationToken);
            if (fromAccount is null)
                throw new AccountNotFoundException("Source account doesn't exist");

            var toAccount = await FindAccountAsync(toAccountNumber, cancellationToken);
            if (toAccount is null)
                throw new AccountNotFoundException("Destination account doesn't exist");

            using (var locks = await Account.LockAllAsync(fromAccount, toAccount))
            {
                locks[fromAccount].Withdraw(amount);
                locks[toAccount].Deposit(amount);
            }

            await _accounts.UpdateAccountAsync(fromAccount, cancellationToken);
            await _accounts.UpdateAccountAsync(toAccount, cancellationToken);
            await _logger.LogAsync(fromAccountNumber, AccountAction.Transfer, -amount, cancellationToken);
            await _logger.LogAsync(toAccountNumber, AccountAction.Transfer, amount, cancellationToken);
        }
    }
}
