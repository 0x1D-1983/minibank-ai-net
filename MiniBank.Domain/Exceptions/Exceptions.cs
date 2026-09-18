using System;

namespace MiniBank.Domain.Exceptions;

public abstract class BankDomainException : Exception
{
    public string ErrorCode { get; }
    public bool IsRetryable { get; }

    protected BankDomainException(string errorCode, string message, bool isRetryable, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
        IsRetryable = isRetryable;
    }
}

public class InvalidAmountException : BankDomainException
{
    public InvalidAmountException(string message)
        : base("INVALID_AMOUNT", message, isRetryable: false) { }
}

public class InsufficientFundsException : BankDomainException
{
    public InsufficientFundsException(string message)
        : base("INSUFFICIENT_FUNDS", message, isRetryable: false) { }
}

public class OverdraftException : InsufficientFundsException
{
    public OverdraftException(string message) : base(message)
    {
        // still ErrorCode = "INSUFFICIENT_FUNDS" via base
    }
}

public class AccountNotFoundException : BankDomainException
{
    public AccountNotFoundException(string message)
        : base("ACCOUNT_NOT_FOUND", message, isRetryable: false) { }
}

public class AuthorizationException : BankDomainException
{
    public AuthorizationException(string message)
        : base("UNAUTHORIZED", message, isRetryable: false) { }
}

public class BankServiceUnavailableException : BankDomainException
{
    public BankServiceUnavailableException(Exception inner)
        : base("BANK_UNAVAILABLE", "Bank service unreachable", true, inner) { }
}