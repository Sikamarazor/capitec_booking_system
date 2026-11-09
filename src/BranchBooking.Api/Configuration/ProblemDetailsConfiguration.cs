using BranchBooking.Api.Http;
using BranchBooking.Api.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace BranchBooking.Api.Configuration;

public static class ProblemDetailsConfiguration
{
    /// <summary>One error shape for everything: business errors, bad input, unknown routes and unexpected failures.</summary>
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context => context.ProblemDetails.AddCorrelation(context.HttpContext));

        services.AddExceptionHandler<ProblemExceptionHandler>();

        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = context =>
            {
                var problem = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "The request is not valid.",
                    Detail = "One or more fields are missing or malformed.",
                };
                problem.Extensions["code"] = "validation_failed";
                problem.AddCorrelation(context.HttpContext);

                return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
            });

        return services;
    }
}
