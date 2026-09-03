using Banking.Domain.Models;
using Banking.Services;
using MiniBank.AI.Tests.Support;
using MiniBank.AI.Tools;

namespace MiniBank.AI.Tests;

public sealed class CustomerToolsTests
{
    [Fact]
    public async Task OwnerTotal_MatchesFirstName_WhenFullNameIsUnique()
    {
        var repository = new RecordingAccountRepository();
        var bank = new Bank(repository, new NoOpAuditLogger());
        await bank.AddAccountAsync(new CurrentAccount("Alice Example", 1234567890, overdraftLimit: 250m));
        await bank.DepositAsync(1234567890, 2_450.00m);
        repository.ClearRecordings();

        var total = await new CustomerTools(bank).GetOwnerTotalBalanceAsync("Alice");

        Assert.Equal(2_450.00m, total);
    }

    [Fact]
    public async Task OwnerTotal_StillMatchesFullName()
    {
        var repository = new RecordingAccountRepository();
        var bank = new Bank(repository, new NoOpAuditLogger());
        await bank.AddAccountAsync(new CurrentAccount("Alice Example", 1234567890, overdraftLimit: 250m));
        await bank.DepositAsync(1234567890, 2_450.00m);
        repository.ClearRecordings();

        var total = await new CustomerTools(bank).GetOwnerTotalBalanceAsync("Alice Example");

        Assert.Equal(2_450.00m, total);
        Assert.Equal(0, repository.AllCallCount);
    }
}
