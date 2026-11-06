using System.Diagnostics;
using BranchBooking.Api.Middleware;
using BranchBooking.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace BranchBooking.Api.Http;

public static class ProblemResults
{
    public static ObjectResult ProblemFor(this ControllerBase controller, Error error)
    {
        var (status, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "The request is not valid."),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not found."),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "The request conflicts with the current state."),
            ErrorType.Policy => (StatusCodes.Status422UnprocessableEntity, "The request breaks a booking rule."),
            _ => (StatusCodes.Status500InternalServerError, "Something went wrong."),
        };

        var problem = controller.ProblemDetailsFactory.CreateProblemDetails(
            controller.HttpContext, statusCode: status, title: title, detail: error.Message);
        problem.Extensions["code"] = error.Code;
        problem.AddCorrelation(controller.HttpContext);

        return new ObjectResult(problem) { StatusCode = status };
    }

    /// <summary>Adds the trace id and, when present, the correlation id so a customer can quote them to support.</summary>
    public static ProblemDetails AddCorrelation(this ProblemDetails problem, HttpContext context)
    {
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        if (CorrelationIdMiddleware.From(context) is { } correlationId)
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        return problem;
    }
}
