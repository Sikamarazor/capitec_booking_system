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

        return new ObjectResult(problem) { StatusCode = status };
    }
}
