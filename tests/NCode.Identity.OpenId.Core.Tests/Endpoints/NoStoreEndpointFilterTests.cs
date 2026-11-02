// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints;

public class NoStoreEndpointFilterTests : BaseTests
{
    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_Always_MarksTheResponseBeforeTheEndpointRuns()
    {
        var httpContext = new DefaultHttpContext();
        var context = new DefaultEndpointFilterInvocationContext(httpContext);
        var expected = new object();
        string? cacheControlSeen = null;

        ValueTask<object?> next(EndpointFilterInvocationContext invocationContext)
        {
            cacheControlSeen =
                invocationContext.HttpContext.Response.Headers.CacheControl.ToString();
            return ValueTask.FromResult<object?>(expected);
        }

        var result = await NoStoreEndpointFilter.Instance.InvokeAsync(context, next);

        Assert.Same(expected, result);
        Assert.Equal("no-store, no-cache, max-age=0", cacheControlSeen);
    }

    #endregion
}
