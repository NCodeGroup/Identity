// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class ProblemResponseEndpointConventionTests
{
    private static DefaultHttpContext CreateHttpContext() =>
        new()
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };

    #region Apply Tests

    [Fact]
    public async Task Apply_WhenTheEndpointAnswersABodilessError_AnswersAProblem()
    {
        var httpContext = CreateHttpContext();
        var builder = new RouteEndpointBuilder(
            _ =>
            {
                _.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            },
            RoutePatternFactory.Parse("/"),
            0
        );

        ProblemResponseEndpointConvention.Apply(builder);
        await builder.RequestDelegate!(httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
    }

    [Fact]
    public void Apply_WhenTheEndpointHasNoRequestDelegate_Throws()
    {
        var builder = new RouteEndpointBuilder(null, RoutePatternFactory.Parse("/"), 0);

        Assert.Throws<InvalidOperationException>(() =>
            ProblemResponseEndpointConvention.Apply(builder)
        );
    }

    #endregion

    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_WhenTheResponseIsABodilessError_AnswersAProblem()
    {
        var httpContext = CreateHttpContext();
        RequestDelegate next = context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        };

        await ProblemResponseEndpointConvention.InvokeAsync(next, httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.StartsWith("application/problem+json", httpContext.Response.ContentType);
        Assert.Equal(StatusCodes.Status404NotFound, await ReadStatusAsync(httpContext));
    }

    [Fact]
    public async Task InvokeAsync_WhenBindingThrowsABadHttpRequestException_AnswersAProblemWithItsStatus()
    {
        var httpContext = CreateHttpContext();
        RequestDelegate next = _ =>
            throw new BadHttpRequestException(
                "Unsupported media type.",
                StatusCodes.Status415UnsupportedMediaType
            );

        await ProblemResponseEndpointConvention.InvokeAsync(next, httpContext);

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, httpContext.Response.StatusCode);
        Assert.StartsWith("application/problem+json", httpContext.Response.ContentType);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, await ReadStatusAsync(httpContext));
    }

    [Fact]
    public async Task InvokeAsync_WhenAnErrorHasAContentType_LeavesTheResponseUntouched()
    {
        var httpContext = CreateHttpContext();
        RequestDelegate next = context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/plain";
            return Task.CompletedTask;
        };

        await ProblemResponseEndpointConvention.InvokeAsync(next, httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.Equal("text/plain", httpContext.Response.ContentType);
        Assert.Equal(0, httpContext.Response.Body.Length);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheResponseIsNoContent_LeavesTheResponseUntouched()
    {
        var httpContext = CreateHttpContext();
        RequestDelegate next = context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        };

        await ProblemResponseEndpointConvention.InvokeAsync(next, httpContext);

        Assert.Equal(StatusCodes.Status204NoContent, httpContext.Response.StatusCode);
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
