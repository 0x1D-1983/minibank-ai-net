using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MiniBank.Domain.Exceptions;

namespace MiniBank.Api;

internal sealed class ApplicationExceptionHandler(ILogger<ApplicationExceptionHandler> logger) : IExceptionHandler
{
    private const string GenericErrorDetail = "An error occurred while processing your request.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
            return false;

        var (status, title, detail) = exception switch
        {
            AccountNotFoundException or AuthorizationException
                => (StatusCodes.Status404NotFound, "Not found", exception.Message),
            ArgumentException or InvalidAmountException or InsufficientFundsException or OverdraftException
                => (StatusCodes.Status400BadRequest, "Invalid request", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Workflow error", GenericErrorDetail)
        };

        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "{Title}: {Detail}", title, exception.Message);
        else
            logger.LogWarning(exception, "{Title}: {Detail}", title, exception.Message);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        }, cancellationToken);

        return true;
    }
}
