// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NCode.Identity.Bff.Hardening;
using NCode.Identity.Bff.Options;
using Xunit;

namespace NCode.Identity.Bff.Tests.Hardening;

public class BffSecurityHeadersMiddlewareTests : BaseTests
{
    private const string DefaultContentSecurityPolicy =
        "default-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";

    private BffOptions Options { get; } = new();

    private StartingResponseFeature ResponseFeature { get; } = new();

    private BffSecurityHeadersMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, Microsoft.Extensions.Options.Options.Create(Options));

    private DefaultHttpContext CreateContext()
    {
        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        features.Set<IHttpResponseFeature>(ResponseFeature);
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));
        return new DefaultHttpContext(features);
    }

    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_ResponseStarts_WritesTheDefaultHeaders()
    {
        var httpContext = CreateContext();
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);
        await ResponseFeature.StartAsync();

        var headers = httpContext.Response.Headers;
        Assert.True(nextCalled);
        Assert.Equal(DefaultContentSecurityPolicy, headers.ContentSecurityPolicy.ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.Equal("same-origin", headers["Referrer-Policy"].ToString());
        Assert.Equal("same-origin", headers["Cross-Origin-Opener-Policy"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_ResponseNotStarted_WritesNoHeaderYet()
    {
        var httpContext = CreateContext();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext);

        Assert.Empty(httpContext.Response.Headers);
    }

    [Fact]
    public async Task InvokeAsync_ConfiguredValues_WritesThem()
    {
        Options.SecurityHeaders.ContentSecurityPolicy = "default-src 'self'; script-src 'self'";
        Options.SecurityHeaders.ReferrerPolicy = "no-referrer";
        Options.SecurityHeaders.CrossOriginOpenerPolicy = "same-origin-allow-popups";
        var httpContext = CreateContext();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext);
        await ResponseFeature.StartAsync();

        var headers = httpContext.Response.Headers;
        Assert.Equal(
            "default-src 'self'; script-src 'self'",
            headers.ContentSecurityPolicy.ToString()
        );
        Assert.Equal("no-referrer", headers["Referrer-Policy"].ToString());
        Assert.Equal("same-origin-allow-popups", headers["Cross-Origin-Opener-Policy"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_ApplicationSetsAHeader_KeepsTheApplicationsValue()
    {
        const string PagePolicy = "default-src 'none'; img-src 'self'";
        var httpContext = CreateContext();
        var middleware = CreateMiddleware(context =>
        {
            context.Response.Headers.ContentSecurityPolicy = PagePolicy;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);
        await ResponseFeature.StartAsync();

        var headers = httpContext.Response.Headers;
        Assert.Equal(PagePolicy, headers.ContentSecurityPolicy.ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ResponseClearedBeforeItStarts_StillWritesTheHeaders()
    {
        var httpContext = CreateContext();
        var middleware = CreateMiddleware(context =>
        {
            context.Response.Headers["X-Before-Clear"] = "1";
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);
        await ResponseFeature.StartAsync();

        var headers = httpContext.Response.Headers;
        Assert.False(headers.ContainsKey("X-Before-Clear"));
        Assert.Equal(DefaultContentSecurityPolicy, headers.ContentSecurityPolicy.ToString());
    }

    #endregion
}
