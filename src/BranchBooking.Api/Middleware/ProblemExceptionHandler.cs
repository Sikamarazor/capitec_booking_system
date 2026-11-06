using BranchBooking.Api.Http;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BranchBooking.Api.Middleware;

/// <summary>Turns any unexpected exception into a 500 problem response that reveals nothing about the cause.</summary>
public sealed class ProblemExceptionHandler(ILogger<ProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;   // The client went away; nothing to report.
            return true;
        }

        logger.LogError(exception, "Unhandled exception for {Method} {Route}", context.Request.Method, RequestRoute.Of(context));

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Something went wrong.",
            Detail = "An unexpected error occurred. If it keeps happening, quote the correlation id to support.",
        };
        problem.Extensions["code"] = "internal_error";
        problem.AddCorrelation(context);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }
}
