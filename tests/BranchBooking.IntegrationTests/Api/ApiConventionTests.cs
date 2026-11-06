using System.Reflection;
using BranchBooking.Api.Controllers;
using BranchBooking.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace BranchBooking.IntegrationTests.Api;

public class ApiConventionTests
{
    private static IEnumerable<Type> Controllers() =>
        typeof(BookingsController).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

    [Fact]
    public void Every_controller_route_is_under_api_v1()
    {
        var controllers = Controllers().ToList();

        Assert.NotEmpty(controllers);
        Assert.All(controllers, type =>
        {
            var route = type.GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.StartsWith("api/v1/", route.Template, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData(nameof(BookingsController.Create), RateLimitPolicies.Write)]
    [InlineData(nameof(BookingsController.Get), RateLimitPolicies.Lookup)]
    [InlineData(nameof(BookingsController.Cancel), RateLimitPolicies.Write)]
    [InlineData(nameof(BookingsController.Reschedule), RateLimitPolicies.Write)]
    public void Every_booking_action_is_rate_limited_with_the_right_policy(string action, string policy)
    {
        var method = typeof(BookingsController).GetMethod(action);

        Assert.NotNull(method);
        Assert.Equal(policy, method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Fact]
    public void Booking_responses_are_never_cached()
    {
        var cache = typeof(BookingsController).GetCustomAttribute<ResponseCacheAttribute>();

        Assert.NotNull(cache);
        Assert.True(cache.NoStore);
    }
}
