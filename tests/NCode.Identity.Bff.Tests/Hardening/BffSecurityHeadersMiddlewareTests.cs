// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NCode.Identity.Bff.Hardening;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Hardening;

public class BffSecurityHeadersMiddlewareTests : BaseTests
{
    private const string DefaultContentSecurityPolicy =
        "default-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";

    private const string AdminContentSecurityPolicy =
        "default-src 'self'; script-src 'self' https://admin.example.com";

    private const string ShopContentSecurityPolicy =
        "default-src 'self'; img-src https://shop.example.com";

    private StartingResponseFeature ResponseFeature { get; } = new();

    private static BffSecurityHeadersMiddleware CreateMiddleware(
        RequestDelegate next,
        Action<BffFrontendOptions>? configure = null
    ) => new(next, BffTestFrontends.CreateCatalog(configure));

    private static BffSecurityHeadersMiddleware CreateMiddleware(
        RequestDelegate next,
        BffOptions options
    ) => new(next, BffTestFrontends.CreateCatalog(options));

    private static BffFrontendOptions CreateHostOptions(
        string host,
        string contentSecurityPolicy
    ) =>
        BffTestFrontends.CreateOptions(x =>
        {
            x.Selection = new BffFrontendSelectionOptions
            {
                Rule = BffFrontendSelectorNames.Host,
                Values = [host],
            };
            x.SecurityHeaders.ContentSecurityPolicy = contentSecurityPolicy;
        });

    private DefaultHttpContext CreateContext(string? host = null)
    {
        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        features.Set<IHttpResponseFeature>(ResponseFeature);
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));
        var httpContext = new DefaultHttpContext(features);
        if (host is not null)
            httpContext.Request.Host = new HostString(host);
        return httpContext;
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
        var httpContext = CreateContext();
        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            x =>
            {
                x.SecurityHeaders.ContentSecurityPolicy = "default-src 'self'; script-src 'self'";
                x.SecurityHeaders.ReferrerPolicy = "no-referrer";
                x.SecurityHeaders.CrossOriginOpenerPolicy = "same-origin-allow-popups";
            }
        );

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

    [Theory]
    [InlineData("admin.example.com", AdminContentSecurityPolicy)]
    [InlineData("shop.example.com", ShopContentSecurityPolicy)]
    public async Task InvokeAsync_TwoFrontends_WritesTheHeadersOfTheSelectedFrontend(
        string host,
        string expectedContentSecurityPolicy
    )
    {
        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            new BffOptions
            {
                Frontends =
                {
                    ["admin"] = CreateHostOptions("admin.example.com", AdminContentSecurityPolicy),
                    ["shop"] = CreateHostOptions("shop.example.com", ShopContentSecurityPolicy),
                },
            }
        );
        var httpContext = CreateContext(host);

        await middleware.InvokeAsync(httpContext);
        await ResponseFeature.StartAsync();

        Assert.Equal(
            expectedContentSecurityPolicy,
            httpContext.Response.Headers.ContentSecurityPolicy.ToString()
        );
    }

    [Fact]
    public async Task InvokeAsync_NoFrontendSelectsTheRequest_WritesTheDefaultHeaders()
    {
        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            new BffOptions
            {
                Frontends =
                {
                    ["admin"] = CreateHostOptions(
                        "admin.example.com",
                        "default-src 'self'; script-src 'self'"
                    ),
                },
            }
        );
        var httpContext = CreateContext("other.example.com");

        await middleware.InvokeAsync(httpContext);
        await ResponseFeature.StartAsync();

        var headers = httpContext.Response.Headers;
        Assert.Equal(DefaultContentSecurityPolicy, headers.ContentSecurityPolicy.ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
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
