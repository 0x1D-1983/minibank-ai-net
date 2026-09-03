using System.ComponentModel;
using MiniBank.AI.Workflows;

namespace MiniBank.AI.Agents;

/// <summary>
/// Classification-only tools. They do not touch the bank; they tell the
/// workflow whether to take the READ or WRITE path.
/// </summary>
internal sealed class IntentRouting
{
    [Description("The user is asking a lookup question (balance, accounts, history, totals). No money should be moved.")]
    public BankingIntent ClassifyQuery() => BankingIntent.Read(string.Empty);

    [Description("The user wants to deposit money into an account. This is a write.")]
    public BankingIntent ClassifyDeposit(
        [Description("The account that should receive the money.")] long accountNumber,
        [Description("The amount to deposit, in GBP.")] decimal amount)
        => BankingIntent.Deposit(string.Empty, accountNumber, amount);

    [Description("The user wants to withdraw money from an account. This is a write.")]
    public BankingIntent ClassifyWithdraw(
        [Description("The account to take money from.")] long accountNumber,
        [Description("The amount to withdraw, in GBP.")] decimal amount)
        => BankingIntent.Withdraw(string.Empty, accountNumber, amount);

    [Description("The user wants to move money from one account to another. This is a write.")]
    public BankingIntent ClassifyTransfer(
        [Description("The account to send money from.")] long fromAccountNumber,
        [Description("The account to send money to.")] long toAccountNumber,
        [Description("The amount to transfer, in GBP.")] decimal amount)
        => BankingIntent.Transfer(string.Empty, fromAccountNumber, toAccountNumber, amount);
}
