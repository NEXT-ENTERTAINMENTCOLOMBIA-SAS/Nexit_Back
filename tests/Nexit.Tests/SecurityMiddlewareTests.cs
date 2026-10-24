using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Nexit.API.Middleware;

namespace Nexit.Tests;

public class SecurityMiddlewareTests
{
    private static IWebHostEnvironment FakeEnvironment(string name) => Mock.Of<IWebHostEnvironment>(e => e.EnvironmentName == name);

    [Fact]
    public async Task SecurityHeaders_adds_protective_headers_to_every_response()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new SecurityHeadersMiddleware(async current => await current.Response.WriteAsync("ok"), FakeEnvironment("Production"));

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("no-store", context.Response.Headers["Cache-Control"].ToString());
        Assert.Equal("default-src 'none'; frame-ancestors 'none'; base-uri 'none'", context.Response.Headers["Content-Security-Policy"].ToString());
    }

    [Fact]
    public async Task SecurityHeaders_skips_csp_only_for_swagger_in_development()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/swagger/index.html";
        context.Response.Body = new MemoryStream();
        var middleware = new SecurityHeadersMiddleware(async current => await current.Response.WriteAsync("ok"), FakeEnvironment("Development"));

        await middleware.InvokeAsync(context);

        Assert.True(string.IsNullOrEmpty(context.Response.Headers["Content-Security-Policy"].ToString()));
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
    }

    [Fact]
    public async Task GlobalExceptionHandler_hides_unexpected_exception_details()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var logger = Mock.Of<ILogger<GlobalExceptionHandlerMiddleware>>();
        var middleware = new GlobalExceptionHandlerMiddleware(_ => throw new InvalidOperationException("secret internal detail"), logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.DoesNotContain("secret internal detail", body);
        Assert.Contains("Ocurrió un error interno", body);
    }

    [Fact]
    public async Task GlobalExceptionHandler_ignores_requests_cancelled_by_the_client()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cts.Token };
        context.Response.Body = new MemoryStream();
        var logger = Mock.Of<ILogger<GlobalExceptionHandlerMiddleware>>();
        var middleware = new GlobalExceptionHandlerMiddleware(_ => throw new OperationCanceledException(cts.Token), logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task GlobalExceptionHandler_still_returns_500_for_cancellation_not_caused_by_the_client()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var logger = Mock.Of<ILogger<GlobalExceptionHandlerMiddleware>>();
        var middleware = new GlobalExceptionHandlerMiddleware(_ => throw new OperationCanceledException(), logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }
}
