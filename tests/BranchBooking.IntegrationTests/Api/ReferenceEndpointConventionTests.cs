using System.Reflection;
using BranchBooking.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace BranchBooking.IntegrationTests.Api;

public class ReferenceEndpointConventionTests
{
    [Fact]
    public void Every_action_whose_route_carries_a_reference_is_rate_limited()
    {
        var controllers = typeof(BookingsController).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

        var checkedActions = 0;
        foreach (var controller in controllers)
        {
            var controllerTemplate = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var http = action.GetCustomAttributes().OfType<HttpMethodAttribute>().FirstOrDefault();
                if (http is null)
                {
                    continue;
                }

                var template = controllerTemplate + "/" + (http.Template ?? string.Empty);
                if (!template.Contains("{reference}", StringComparison.Ordinal))
                {
                    continue;
                }

                checkedActions++;
                Assert.True(
                    action.GetCustomAttribute<EnableRateLimitingAttribute>() is not null,
                    $"{controller.Name}.{action.Name} takes a reference but has no [EnableRateLimiting].");
            }
        }

        Assert.Equal(4, checkedActions); // Get, cancel, reschedule and messages.
    }
}
