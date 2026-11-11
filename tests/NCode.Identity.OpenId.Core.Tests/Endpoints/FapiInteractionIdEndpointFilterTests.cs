// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints;

public class FapiInteractionIdEndpointFilterTests : BaseTests
{
    private const string HeaderName = FapiInteractionIdEndpointFilter.HeaderName;

    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_WhenTheRequestCarriesAUuid_EchoesIt()
    {
        var requested = Guid.NewGuid().ToString("D");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[HeaderName] = requested;
        var context = new DefaultEndpointFilterInvocationContext(httpContext);
        var expected = new object();

        var result = await FapiInteractionIdEndpointFilter.Instance.InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>(expected)
        );

        Assert.Same(expected, result);
        Assert.Equal(requested, httpContext.Response.Headers[HeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenTheRequestCarriesAMixedCaseUuid_EchoesItAsWritten()
    {
        const string requested = "90fC5b77-C23c-49E3-bB66-396f3EcD36c5";
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[HeaderName] = requested;
        var context = new DefaultEndpointFilterInvocationContext(httpContext);

        await FapiInteractionIdEndpointFilter.Instance.InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>(null)
        );

        Assert.Equal(requested, httpContext.Response.Headers[HeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenTheRequestCarriesAValueThatIsNotAUuid_RepliesWithANewUuid()
    {
        const string requested = "not-a-uuid\r\nx-injected: 1";
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[HeaderName] = requested;
        var context = new DefaultEndpointFilterInvocationContext(httpContext);

        await FapiInteractionIdEndpointFilter.Instance.InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>(null)
        );

        var actual = httpContext.Response.Headers[HeaderName].ToString();
        Assert.NotEqual(requested, actual);
        Assert.True(Guid.TryParseExact(actual, "D", out _));
    }

    [Fact]
    public async Task InvokeAsync_WhenTheRequestCarriesNoHeader_RepliesWithANewUuid()
    {
        var httpContext = new DefaultHttpContext();
        var context = new DefaultEndpointFilterInvocationContext(httpContext);

        await FapiInteractionIdEndpointFilter.Instance.InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>(null)
        );

        var actual = httpContext.Response.Headers[HeaderName].ToString();
        Assert.True(Guid.TryParseExact(actual, "D", out _));
    }

    [Fact]
    public async Task InvokeAsync_Always_SetsTheHeaderBeforeTheEndpointRuns()
    {
        var requested = Guid.NewGuid().ToString("D");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[HeaderName] = requested;
        var context = new DefaultEndpointFilterInvocationContext(httpContext);
        string? headerSeen = null;

        ValueTask<object?> next(EndpointFilterInvocationContext invocationContext)
        {
            headerSeen = invocationContext.HttpContext.Response.Headers[HeaderName].ToString();
            return ValueTask.FromResult<object?>(null);
        }

        await FapiInteractionIdEndpointFilter.Instance.InvokeAsync(context, next);

        Assert.Equal(requested, headerSeen);
    }

    #endregion
}
