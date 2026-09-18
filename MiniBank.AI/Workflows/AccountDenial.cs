using System;
using MiniBank.Domain.Exceptions;

namespace MiniBank.AI.Workflows;

public static class AccountDenial
{
    public static bool TryGet(Exception exception, out Exception denial)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is AccountNotFoundException or AuthorizationException)
            {
                denial = current;
                return true;
            }

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                {
                    if (TryGet(inner, out denial))
                        return true;
                }

                break;
            }
        }

        denial = exception;
        return false;
    }
}
