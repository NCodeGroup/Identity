// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class ManagementProblemResponseMiddlewareTests
{
    private static DefaultHttpContext CreateHttpContext(string path) =>
        new()
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            Request = { Path = path },
            Response = { Body = new MemoryStream() },
        };

    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_WhenAManagementRequestEndsWithABodilessError_AnswersAProblem()
    {
        var httpContext = CreateHttpContext("/api/x");
        var middleware = new ManagementProblemResponseMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, httpContext.Response.StatusCode);
        Assert.StartsWith("application/problem+json", httpContext.Response.ContentType);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, await ReadStatusAsync(httpContext));
    }

    [Fact]
    public async Task InvokeAsync_WhenTheRouterAnswersMethodNotAllowed_AnswersAProblemAndKeepsTheAllowHeader()
    {
        var httpContext = CreateHttpContext("/api/x");
        var middleware = new ManagementProblemResponseMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            context.Response.Headers.Allow = "GET";
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status405MethodNotAllowed, httpContext.Response.StatusCode);
        Assert.StartsWith("application/problem+json", httpContext.Response.ContentType);
        Assert.Equal("GET", httpContext.Response.Headers.Allow.ToString());
        Assert.Equal(StatusCodes.Status405MethodNotAllowed, await ReadStatusAsync(httpContext));
    }

    [Fact]
    public async Task InvokeAsync_WhenAManagementErrorHasAContentType_LeavesTheResponseUntouched()
    {
        var httpContext = CreateHttpContext("/api/x");
        var middleware = new ManagementProblemResponseMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/plain";
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.Equal("text/plain", httpContext.Response.ContentType);
        Assert.Equal(0, httpContext.Response.Body.Length);
    }

    [Fact]
    public async Task InvokeAsync_WhenAManagementResponseIsNoContent_LeavesTheResponseUntouched()
    {
        var httpContext = CreateHttpContext("/api/x");
        var middleware = new ManagementProblemResponseMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status204NoContent, httpContext.Response.StatusCode);
        Assert.Null(httpContext.Response.ContentType);
        Assert.Equal(0, httpContext.Response.Body.Length);
    }

    [Theory]
    [InlineData("/apix")]
    [InlineData("/other")]
    public async Task InvokeAsync_WhenTheRequestIsNotUnderTheManagementPrefix_LeavesTheResponseUntouched(
        string path
    )
    {
        var httpContext = CreateHttpContext(path);
        var middleware = new ManagementProblemResponseMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.Null(httpContext.Response.ContentType);
        Assert.Equal(0, httpContext.Response.Body.Length);
    }

    #endregion

    private static async Task<int> ReadStatusAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        var root = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);
        return root.GetProperty("status").GetInt32();
    }
}
