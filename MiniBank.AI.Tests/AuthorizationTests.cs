using MiniBank.Domain.Models;
using MiniBank.Domain.Exceptions;
using Banking.Services;
using MiniBank.Auth;
using MiniBank.AI.Tests.Support;
using MiniBank.AI.Tools;
using System.Threading.Tasks;

namespace MiniBank.AI.Tests;

/// <summary>
/// Non-Ollama tests that verify authorization enforcement in Bank and tools.
/// Tests run without LLM calls - they test the authorization layer directly.
/// </summary>
public sealed class AuthorizationTests
{
    private async Task<(Bank bank, RecordingAccountRepository repository)> CreateSeededBankAsync()
    {
        var repository = new RecordingAccountRepository();
        var bank = new Bank(repository, new NoOpAuditLogger());

        await bank.AddAccountAsync(new CurrentAccount("Alice Example", 1234567890, overdraftLimit: 250m));
        await bank.DepositAsync(1234567890, 2_450.00m);

        await bank.AddAccountAsync(new CurrentAccount("John Smith", 10001, overdraftLimit: 500m));
        await bank.DepositAsync(10001, 1_532.42m);

        await bank.AddAccountAsync(new SavingsAccount("John Smith", 10002, interestRate: 0.02m));
        await bank.DepositAsync(10002, 800.00m);

        await bank.AddAccountAsync(new CurrentAccount("Jane Doe", 20001, overdraftLimit: 0m));
        await bank.DepositAsync(20001, 5_000.00m);

        repository.ClearRecordings();
        return (bank, repository);
    }

    [Fact]
    public async Task JohnCanReadOwnAccount10001()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");

        var account = await authorizedBank.FindAccountAsync(10001);

        Assert.NotNull(account);
        Assert.Equal(10001L, account.AccountNumber);
        Assert.Equal("John Smith", account.Owner);
    }

    [Fact]
    public async Task JohnCanReadOwnAccount10002()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");

        var account = await authorizedBank.FindAccountAsync(10002);

        Assert.NotNull(account);
        Assert.Equal(10002L, account.AccountNumber);
    }

    [Fact]
    public async Task JohnCannotReadJanesAccount20001()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");

        var account = await authorizedBank.FindAccountAsync(20001);

        Assert.Null(account);
    }

    [Fact]
    public async Task JohnCannotReadAlicesAccount()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");

        var account = await authorizedBank.FindAccountAsync(1234567890);

        Assert.Null(account);
    }

    [Fact]
    public async Task JohnCannotDepositToJanesAccount()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");

        var ex = await Assert.ThrowsAsync<AccountNotFoundException>(
            () => authorizedBank.DepositAsync(20001, 100m));

        Assert.Contains("20001", ex.Message);
    }

    [Fact]
    public async Task JohnCannotWithdrawFromJanesAccount()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");

        var ex = await Assert.ThrowsAsync<AccountNotFoundException>(
            () => authorizedBank.WithdrawAsync(20001, 100m));

        Assert.Contains("20001", ex.Message);
    }

    [Fact]
    public async Task JohnCanTransferFromOwnAccountToJanesAccount()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");

        await authorizedBank.TransferAsync(10001, 20001, 50m);

        var fromAccount = await bank.FindAccountAsync(10001);
        var toAccount = await bank.FindAccountAsync(20001);
        Assert.Equal(1482.42m, await fromAccount!.GetBalanceAsync());
        Assert.Equal(5050.00m, await toAccount!.GetBalanceAsync());
    }

    [Fact]
    public async Task JohnCannotTransferFromJanesAccount()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");

        var ex = await Assert.ThrowsAsync<AccountNotFoundException>(
            () => authorizedBank.TransferAsync(20001, 10001, 50m));

        Assert.Contains("20001", ex.Message);

        var fromAccount = await bank.FindAccountAsync(20001);
        var toAccount = await bank.FindAccountAsync(10001);
        Assert.Equal(5000.00m, await fromAccount!.GetBalanceAsync());
        Assert.Equal(1532.42m, await toAccount!.GetBalanceAsync());
    }

    [Fact]
    public async Task GetAllAccountsAsync_ReturnsOnlyJohnsAccounts()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");

        var accounts = await authorizedBank.GetAllAccountsAsync();

        Assert.Equal(2, accounts.Count);
        Assert.All(accounts, a => Assert.Equal("John Smith", a.Owner));
        Assert.Contains(accounts, a => a.AccountNumber == 10001);
        Assert.Contains(accounts, a => a.AccountNumber == 10002);
    }

    [Fact]
    public async Task GetTotalBalanceAsync_ReturnsOnlyJohnsTotal()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");

        var total = await authorizedBank.GetTotalBalanceAsync();

        Assert.Equal(2332.42m, total);
    }

    [Fact]
    public async Task GetAccountsByOwnerAsync_ReturnsEmptyForOtherOwner()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");

        var accounts = await authorizedBank.GetAccountsByOwnerAsync("Jane Doe");

        Assert.Empty(accounts);
    }

    [Fact]
    public async Task GetAccountsByOwnerAsync_ReturnsAccountsForCurrentOwner()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");

        var accounts = await authorizedBank.GetAccountsByOwnerAsync("John Smith");

        Assert.Equal(2, accounts.Count);
    }

    [Fact]
    public async Task AccountTools_GetBalance_ReturnsNotFoundForOtherCustomersAccount()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");
        var tools = new AccountTools(authorizedBank);

        var ex = await Assert.ThrowsAsync<AccountNotFoundException>(
            () => tools.GetBalanceAsync(20001));

        Assert.Contains("20001", ex.Message);
    }

    [Fact]
    public async Task AccountTools_FindAccountsByOwner_ReturnsCurrentCustomerAccounts()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");
        var tools = new AccountTools(authorizedBank);

        var accounts = await tools.FindAccountsByOwnerAsync();

        Assert.Equal(2, accounts.Count);
        Assert.All(accounts, account => Assert.Equal("John Smith", account.Owner));
    }

    [Fact]
    public async Task AccountTools_GetTotalValue_ScopedToCurrentCustomer()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");
        var tools = new AccountTools(authorizedBank);

        var total = await tools.GetTotalValueAsync();

        Assert.Equal(2332.42m, total);
    }

    [Fact]
    public async Task AccountTools_GetHighestBalanceAccount_ScopedToCurrentCustomer()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");
        var tools = new AccountTools(authorizedBank);

        var highest = await tools.GetHighestBalanceAccountAsync();

        Assert.NotNull(highest);
        Assert.Equal(10001L, highest.AccountNumber);
        Assert.Equal(1532.42m, highest.Balance);
    }

    [Fact]
    public async Task CustomerTools_GetOwnerTotalBalance_UsesCurrentCustomer()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");
        var tools = new CustomerTools(authorizedBank);

        var total = await tools.GetOwnerTotalBalanceAsync();

        Assert.Equal(2332.42m, total);
    }

    [Fact]
    public async Task CustomerTools_CountDepositsByOwner_UsesCurrentCustomer()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");
        var tools = new CustomerTools(authorizedBank);

        var count = await tools.CountDepositsByOwnerAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task TransactionTools_GetDeposits_ThrowsForOtherCustomersAccount()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");
        var tools = new TransactionTools(authorizedBank);

        var ex = await Assert.ThrowsAsync<AccountNotFoundException>(
            () => tools.GetDepositsAsync(20001));

        Assert.Contains("20001", ex.Message);
    }

    [Fact]
    public async Task TransactionTools_GetAccountHistory_ThrowsForOtherCustomersAccount()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");
        var tools = new TransactionTools(authorizedBank);

        var ex = await Assert.ThrowsAsync<AccountNotFoundException>(
            () => tools.GetAccountHistoryAsync(20001));

        Assert.Contains("20001", ex.Message);
    }

    [Fact]
    public async Task OperationTools_Deposit_ThrowsForOtherCustomersAccount()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");
        var tools = new OperationTools(authorizedBank);

        var ex = await Assert.ThrowsAsync<AccountNotFoundException>(
            () => tools.DepositAsync(20001, 100m));

        Assert.Contains("20001", ex.Message);
    }

    [Fact]
    public async Task OperationTools_Withdraw_ThrowsForOtherCustomersAccount()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");
        var tools = new OperationTools(authorizedBank);

        var ex = await Assert.ThrowsAsync<AccountNotFoundException>(
            () => tools.WithdrawAsync(20001, 100m));

        Assert.Contains("20001", ex.Message);
    }

    [Fact]
    public async Task OperationTools_Transfer_ThrowsWhenSourceIsOtherCustomersAccount()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");
        var tools = new OperationTools(authorizedBank);

        var ex = await Assert.ThrowsAsync<AccountNotFoundException>(
            () => tools.TransferAsync(20001, 10001, 100m));

        Assert.Contains("20001", ex.Message);
    }

    [Fact]
    public async Task OperationTools_Transfer_AllowedWhenDestinationIsOtherCustomer()
    {
        var (bank, _) = await CreateSeededBankAsync();
        var authorizedBank = new AuthorizedBank(bank, "John Smith");
        var tools = new OperationTools(authorizedBank);

        var result = await tools.TransferAsync(10001, 20001, 50m);

        Assert.Contains("10001", result);
        Assert.Contains("20001", result);
        Assert.Contains("50", result);
    }
}
