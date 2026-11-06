using System.Text.Json;
using BranchBooking.Api.Middleware;
using BranchBooking.IntegrationTests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BranchBooking.IntegrationTests.Api;

public class MiddlewareTests
{
    private static DefaultHttpContext NewContext(string? routeTemplate = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();
        context.Request.Method = "GET";

        if (routeTemplate is not null)
        {
            context.SetEndpoint(new RouteEndpoint(
                _ => Task.CompletedTask, RoutePatternFactory.Parse(routeTemplate), 0, EndpointMetadataCollection.Empty, "test"));
        }

        return context;
    }

    [Fact]
    public async Task A_safe_correlation_id_is_kept_and_echoed()
    {
        var context = NewContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "abc-123_XYZ";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new ListLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        Assert.Equal("abc-123_XYZ", context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
        Assert.Equal("abc-123_XYZ", CorrelationIdMiddleware.From(context));
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("line\\nbreak")]
    [InlineData("<script>")]
    public async Task An_unsafe_correlation_id_is_replaced(string supplied)
    {
        var context = NewContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = supplied;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new ListLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        var id = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.NotEqual(supplied, id);
        Assert.Matches("^[0-9a-f]{32}$", id);
    }

    [Fact]
    public async Task A_missing_correlation_id_is_generated()
    {
        var context = NewContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new ListLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        Assert.Matches("^[0-9a-f]{32}$", CorrelationIdMiddleware.From(context)!);
    }

    [Fact]
    public async Task Security_headers_are_set()
    {
        var context = NewContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var headers = context.Response.Headers;
        Assert.Equal("nosniff", headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", headers["X-Frame-Options"].ToString());
        Assert.Equal("no-referrer", headers["Referrer-Policy"].ToString());
        Assert.Contains("frame-ancestors 'none'", headers["Content-Security-Policy"].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_request_log_names_the_route_template_and_never_the_url()
    {
        var context = NewContext("api/v1/bookings/{reference}");
        context.Request.Path = "/api/v1/bookings/BK-ABCDE-FGHJK";
        var logger = new ListLogger<RequestLoggingMiddleware>();
        var middleware = new RequestLoggingMiddleware(c => { c.Response.StatusCode = 200; return Task.CompletedTask; }, logger);

        await middleware.InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("api/v1/bookings/{reference}", entry.Message, StringComparison.Ordinal);
        Assert.Contains("200", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("BK-ABCDE-FGHJK", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unmatched_request_is_logged_without_its_path()
    {
        var context = NewContext();
        context.Request.Path = "/secret/BK-ABCDE-FGHJK";
        var logger = new ListLogger<RequestLoggingMiddleware>();
        var middleware = new RequestLoggingMiddleware(_ => Task.CompletedTask, logger);

        await middleware.InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("(no route)", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_request_is_logged_even_when_the_pipeline_throws()
    {
        var context = NewContext("api/v1/bookings");
        var logger = new ListLogger<RequestLoggingMiddleware>();
        var middleware = new RequestLoggingMiddleware(_ => throw new InvalidOperationException("boom"), logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.Single(logger.Entries);
    }

    [Fact]
    public async Task The_exception_handler_writes_a_500_problem_that_hides_the_cause()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = NewContext("api/v1/bookings/{reference}");
        RequestRoute.Capture(context);
        var logger = new ListLogger<ProblemExceptionHandler>();
        var handler = new ProblemExceptionHandler(logger);

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("password=hunter2"), ct);

        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync(ct);
        Assert.DoesNotContain("hunter2", text, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(text);
        Assert.Equal("internal_error", json.RootElement.GetProperty("code").GetString());
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());

        var logged = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logged.Level);
        Assert.Contains("api/v1/bookings/{reference}", logged.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_client_that_disconnects_is_not_an_error()
    {
        var context = NewContext();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        context.RequestAborted = cancelled.Token;
        var logger = new ListLogger<ProblemExceptionHandler>();
        var handler = new ProblemExceptionHandler(logger);

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(), TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(499, context.Response.StatusCode);
        Assert.Empty(logger.Entries);
    }
}
