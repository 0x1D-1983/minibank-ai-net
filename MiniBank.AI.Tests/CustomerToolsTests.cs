using MiniBank.Domain.Models;
using Banking.Services;
using MiniBank.AI.Tests.Support;
using MiniBank.AI.Tools;
using System.Threading.Tasks;

namespace MiniBank.AI.Tests;

public sealed class CustomerToolsTests
{
    [Fact]
    public async Task OwnerTotal_UsesAuthorizedCustomer()
    {
        var repository = new RecordingAccountRepository();
        var bank = new Bank(repository, new NoOpAuditLogger());
        await bank.AddAccountAsync(new CurrentAccount("Alice Example", 1234567890, overdraftLimit: 250m));
        await bank.DepositAsync(1234567890, 2_450.00m);
        repository.ClearRecordings();

        var authorizedBank = new AuthorizedBank(bank, "Alice Example");
        var total = await new CustomerTools(authorizedBank).GetOwnerTotalBalanceAsync();

        Assert.Equal(2_450.00m, total);
        AgentAssert.LookedUpOwner(repository, "Alice Example");
    }
}
