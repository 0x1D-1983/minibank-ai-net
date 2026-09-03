using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using Banking.Services;

namespace MiniBank.AI.Tools;

/// <summary>
/// Write operations. These change balances and must only run after approval.
/// </summary>
public sealed class OperationTools
{
    private readonly Bank _bank;

    public OperationTools(Bank bank)
    {
        _bank = bank;
    }

    [Description("Deposit money into an account. This changes the balance.")]
    public async Task<string> DepositAsync(
        [Description("The account that receives the deposit.")] long accountNumber,
        [Description("The amount to deposit, in GBP.")] decimal amount)
    {
        await _bank.DepositAsync(accountNumber, amount);
        return $"Deposited {Format(amount)} into account {accountNumber}.";
    }

    [Description("Withdraw money from an account. This changes the balance.")]
    public async Task<string> WithdrawAsync(
        [Description("The account to withdraw from.")] long accountNumber,
        [Description("The amount to withdraw, in GBP.")] decimal amount)
    {
        await _bank.WithdrawAsync(accountNumber, amount);
        return $"Withdrew {Format(amount)} from account {accountNumber}.";
    }

    [Description("Transfer money from one account to another. This changes both balances.")]
    public async Task<string> TransferAsync(
        [Description("The account to send money from.")] long fromAccountNumber,
        [Description("The account to send money to.")] long toAccountNumber,
        [Description("The amount to transfer, in GBP.")] decimal amount)
    {
        await _bank.TransferAsync(fromAccountNumber, toAccountNumber, amount);
        return $"Transferred {Format(amount)} from account {fromAccountNumber} to account {toAccountNumber}.";
    }

    private static string Format(decimal amount)
        => amount.ToString("C", CultureInfo.GetCultureInfo("en-GB"));
}
