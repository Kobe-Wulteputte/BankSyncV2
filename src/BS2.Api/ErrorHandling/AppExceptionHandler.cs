using BS2.Application.Abstractions;
using BS2.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BS2.Api.ErrorHandling;

/// <summary>Maps application exceptions to problem+json. Anything else falls through to the default 500.</summary>
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        ProblemDetails? problem = exception switch
        {
            NotFoundException e => new ProblemDetails { Status = 404, Title = "Not found", Detail = e.Message },
            ConflictException e => new ProblemDetails { Status = 409, Title = "Conflict", Detail = e.Message },
            ValidationException e => new ValidationProblemDetails(e.Errors) { Status = 400, Title = "Validation failed" },
            // The message carries request URIs and inner exception text; it goes to the log, not the client.
            BankingProviderException => new ProblemDetails { Status = 502, Title = "Bank provider error", Detail = "The bank provider request failed; see the server log." },
            BadHttpRequestException e => new ProblemDetails { Status = e.StatusCode, Title = "Bad request", Detail = e.Message },
            _ => null
        };

        if (problem is null) return false;

        if (problem.Status >= 500) logger.LogError(exception, "{Title}: {Detail}", problem.Title, exception.Message);

        context.Response.StatusCode = problem.Status!.Value;
        // Runtime type matters: ValidationProblemDetails.Errors is lost when serialized as ProblemDetails.
        await context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json", ct);
        return true;
    }
}
