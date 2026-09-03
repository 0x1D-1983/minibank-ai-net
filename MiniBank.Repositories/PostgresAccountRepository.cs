namespace Banking.Repositories;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using MiniBank.Domain.Models;
using System.Threading;

public class PostgresAccountRepository : IAccountRepository, IAsyncDisposable
{
    private readonly string _connectionString;
    private NpgsqlDataSource? _dataSource;
    private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

    /// <param name="connectionString">
    /// e.g. "Host=localhost;Port=5432;Database=minibank;Username=user;Password=password"
    /// </param>
    public PostgresAccountRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private async Task<NpgsqlDataSource> GetDataSourceAsync()
    {
        if (_dataSource is not null)
            return _dataSource;

        await _initLock.WaitAsync();
        try
        {
            // Double-checked locking: another caller may have initialized
            // it while we were waiting on the semaphore.
            if (_dataSource is null)
            {
                var builder = new NpgsqlDataSourceBuilder(_connectionString);
                _dataSource = builder.Build(); // pools internally, min/max via connection string
            }
            return _dataSource;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Close the connection pool. Call when shutting down.
    /// </summary>
    public async Task CloseAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
            _dataSource = null;
        }
    }

    public async ValueTask DisposeAsync() => await CloseAsync();

    public async Task AddAccountAsync(Account account)
    {
        string typeVal;
        decimal? interestRate;
        decimal? overdraftLimit;

        switch (account)
        {
            case SavingsAccount savings:
                typeVal = "savings";
                interestRate = savings.InterestRate;
                overdraftLimit = null;
                break;
            case CurrentAccount current:
                typeVal = "current";
                interestRate = null;
                overdraftLimit = current.OverdraftLimit;
                break;
            default:
                throw new ArgumentException($"Unknown account type: {account.GetType()}");
        }

        var balance = await account.GetBalanceAsync();

        var dataSource = await GetDataSourceAsync();
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO accounts (account_number, owner, type, balance, interest_rate, overdraft_limit)
            VALUES ($1, $2, $3, $4, $5, $6)
            """,
            conn);

        cmd.Parameters.AddWithValue(account.AccountNumber);
        cmd.Parameters.AddWithValue(account.Owner);
        cmd.Parameters.AddWithValue(typeVal);
        cmd.Parameters.AddWithValue(balance);
        cmd.Parameters.AddWithValue((object?)interestRate ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)overdraftLimit ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Account?> FindByIdAsync(long accountNumber)
    {
        var dataSource = await GetDataSourceAsync();
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT account_number, owner, type, balance, interest_rate, overdraft_limit
            FROM accounts WHERE account_number = $1
            """,
            conn);
        cmd.Parameters.AddWithValue(accountNumber);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return RowToAccount(reader);
    }

    public async Task<List<Account>> FindByOwnerAsync(string owner)
    {
        var dataSource = await GetDataSourceAsync();
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT account_number, owner, type, balance, interest_rate, overdraft_limit
            FROM accounts WHERE owner = $1
            """,
            conn);
        cmd.Parameters.AddWithValue(owner);

        var results = new List<Account>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(RowToAccount(reader));

        return results;
    }

    public async Task<List<Account>> AllAsync()
    {
        var dataSource = await GetDataSourceAsync();
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT account_number, owner, type, balance, interest_rate, overdraft_limit
            FROM accounts
            """,
            conn);

        var results = new List<Account>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(RowToAccount(reader));

        return results;
    }

    public async Task UpdateAccountAsync(Account account)
    {
        var balance = await account.GetBalanceAsync();

        var dataSource = await GetDataSourceAsync();
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE accounts SET balance = $1, updated_at = NOW()
            WHERE account_number = $2
            """,
            conn);
        cmd.Parameters.AddWithValue(balance);
        cmd.Parameters.AddWithValue(account.AccountNumber);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Map a DB row to a domain Account (SavingsAccount or CurrentAccount).
    /// </summary>
    private static Account RowToAccount(NpgsqlDataReader row)
    {
        var accountNumber = row.GetInt64(row.GetOrdinal("account_number"));
        var owner = row.GetString(row.GetOrdinal("owner"));
        var balance = row.GetDecimal(row.GetOrdinal("balance"));
        var type = row.GetString(row.GetOrdinal("type"));

        Account account = type == "savings"
            ? new SavingsAccount(
                owner,
                accountNumber,
                interestRate: row.GetDecimal(row.GetOrdinal("interest_rate")))
            : new CurrentAccount(
                owner,
                accountNumber,
                overdraftLimit: row.GetDecimal(row.GetOrdinal("overdraft_limit")));

        account.HydrateBalance(balance);
        return account;
    }
}