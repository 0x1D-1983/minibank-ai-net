using System;

namespace MiniBank.Domain.Exceptions;

public class InvalidAmountException : Exception
{
    public InvalidAmountException(string message) : base(message) { }
}

public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(string message) : base(message) { }
}

public class OverdraftException : Exception
{
    public OverdraftException(string message) : base(message) { }
}

public class AccountNotFoundException : Exception
{
    public AccountNotFoundException(string message) : base(message) { }
}