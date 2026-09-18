using System.Linq;
using System.Threading.Tasks;
using Banking.Services;
using MiniBank.AI.Tests.Support;
using MiniBank.Domain.Exceptions;
using MiniBank.Domain.Models;

namespace MiniBank.AI.Tests;

public sealed class CustomerBankAuthorizationTests
{
    [Fact]
    public async Task JohnCanReadOnlyJohnAccounts()
    {
        var bank = await CreateSeededBankAsync();
        var john = bank.ForCustomer("John Smith");

        Assert.NotNull(await john.FindAccountAsync(10001));
        Assert.NotNull(await john.FindAccountAsync(10002));
        Assert.Null(await john.FindAccountAsync(20001));
        Assert.Null(await john.FindAccountAsync(1234567890));

        var visibleAccounts = await john.GetAllAccountsAsync();
        Assert.Equal(
            new[] { 10001L, 10002L },
            visibleAccounts.Select(account => account.AccountNumber).Order().ToArray());
        Assert.Equal(2_332.42m, await john.GetTotalBalanceAsync());
    }

    [Fact]
    public async Task OwnerLookupsDoNotResolveOtherCustomers()
    {
        var bank = await CreateSeededBankAsync();
        var john = bank.ForCustomer("John Smith");

        Assert.Empty(await john.GetAccountsByOwnerAsync("Jane Doe"));
        Assert.Empty(await john.GetAccountsByOwnerAsync("Alice Example"));

        var johnAccounts = await john.GetAccountsByOwnerAsync("John Smith");
        Assert.Equal(
            new[] { 10001L, 10002L },
            johnAccounts.Select(account => account.AccountNumber).Order().ToArray());
    }

    [Fact]
    public async Task JohnCanTransferToJaneButCannotDebitJane()
    {
        var bank = await CreateSeededBankAsync();
        var john = bank.ForCustomer("John Smith");

        await john.TransferAsync(10001, 20001, 50m);

        Assert.Equal(1_482.42m, await (await bank.FindAccountAsync(10001))!.GetBalanceAsync());
        Assert.Equal(5_050.00m, await (await bank.FindAccountAsync(20001))!.GetBalanceAsync());

        var ex = await Assert.ThrowsAsync<AccountNotFoundException>(
            () => john.TransferAsync(20001, 10001, 10m));
        Assert.Equal("Source account doesn't exist", ex.Message);

        Assert.Equal(1_482.42m, await (await bank.FindAccountAsync(10001))!.GetBalanceAsync());
        Assert.Equal(5_050.00m, await (await bank.FindAccountAsync(20001))!.GetBalanceAsync());
    }

    [Fact]
    public async Task JohnCannotDepositOrWithdrawOnJaneAccount()
    {
        var bank = await CreateSeededBankAsync();
        var john = bank.ForCustomer("John Smith");

        await Assert.ThrowsAsync<AccountNotFoundException>(() => john.DepositAsync(20001, 10m));
        await Assert.ThrowsAsync<AccountNotFoundException>(() => john.WithdrawAsync(20001, 10m));

        Assert.Equal(5_000.00m, await (await bank.FindAccountAsync(20001))!.GetBalanceAsync());
    }

    private static async Task<Bank> CreateSeededBankAsync()
    {
        var bank = new Bank(new RecordingAccountRepository(), new NoOpAuditLogger());

        await bank.AddAccountAsync(new CurrentAccount("Alice Example", 1234567890, overdraftLimit: 250m));
        await bank.DepositAsync(1234567890, 2_450.00m);

        await bank.AddAccountAsync(new CurrentAccount("John Smith", 10001, overdraftLimit: 500m));
        await bank.DepositAsync(10001, 1_532.42m);

        await bank.AddAccountAsync(new SavingsAccount("John Smith", 10002, interestRate: 0.02m));
        await bank.DepositAsync(10002, 800.00m);

        await bank.AddAccountAsync(new CurrentAccount("Jane Doe", 20001, overdraftLimit: 0m));
        await bank.DepositAsync(20001, 5_000.00m);

        return bank;
    }
}
