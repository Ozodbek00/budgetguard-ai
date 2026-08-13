using BudgetGuard.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BudgetGuard.Api.Infrastructure;

/// <summary>
/// Translates application exceptions into RFC 7807 problem responses.
/// <para>
/// Centralised so endpoints stay free of try/catch and every failure looks the
/// same to a client. Only exception types this layer knows how to describe are
/// mapped; anything else falls through to the default 500 rather than being
/// reported with a misleading status code or leaking internals.
/// </para>
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The request could not be processed.",
                Detail = string.Join(" ", validation.AllMessages),
                Extensions = { ["errors"] = validation.Errors }
            },

            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found.",
                Detail = notFound.Message
            },

            _ => null
        };

        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception processing {Path}.", httpContext.Request.Path);
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
