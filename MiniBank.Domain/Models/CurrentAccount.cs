namespace MiniBank.Domain.Models;

using System.Threading.Tasks;
using MiniBank.Domain.Exceptions;

public class CurrentAccount : Account
{
    public decimal OverdraftLimit { get; }

    public CurrentAccount(string owner, long accountNumber, decimal overdraftLimit)
        : base(owner, accountNumber) => OverdraftLimit = overdraftLimit;

    internal override void ApplyWithdraw(decimal amount)
    {
        ValidateWithdrawAmount(amount);
        if (_balance + OverdraftLimit >= amount)
        {
            _balance -= amount;
            History.Add($"{AccountAction.Withdraw}: -{amount:F2}");
        }
        else
        {
            throw new OverdraftException("Overdraft limit exceeded");
        }
    }

    public override async Task<string> ToStringAsync()
    {
        var balance = await GetBalanceAsync();
        return $"CurrentAccount(owner=\"{Owner}\", accountNumber={AccountNumber}, balance={balance}, overdraftLimit={OverdraftLimit})";
    }
}