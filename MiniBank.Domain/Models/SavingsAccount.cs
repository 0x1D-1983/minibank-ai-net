namespace MiniBank.Domain.Models;

using System.Threading.Tasks;
using MiniBank.Domain.Exceptions;

public class SavingsAccount : Account
{
    public decimal InterestRate { get; }

    public SavingsAccount(string owner, long accountNumber, decimal interestRate)
        : base(owner, accountNumber) => InterestRate = interestRate;

    internal override void ApplyWithdraw(decimal amount)
    {
        ValidateWithdrawAmount(amount);
        if (_balance >= amount)
        {
            _balance -= amount;
            History.Add($"{AccountAction.Withdraw}: -{amount:F2}");
        }
        else
        {
            throw new InsufficientFundsException("Insufficient balance");
        }
    }

    public async Task ApplyInterestAsync()
    {
        // SavingsAccount has protected access to _balance/History, so it can
        // mutate directly here — the handle is only needed as a lock token.
        using var handle = await AcquireLockAsync();
        var interest = _balance * InterestRate;
        _balance += interest;
        History.Add($"{AccountAction.Interest}: +{interest:F2}");
    }

    public override async Task<string> ToStringAsync()
    {
        var balance = await GetBalanceAsync();
        return $"SavingsAccount(owner=\"{Owner}\", accountNumber={AccountNumber}, balance={balance}, interestRate={InterestRate})";
    }
}