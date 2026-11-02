// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.RateLimiting;

public sealed class RateLimitRequestStateTests
{
    #region Get Tests

    [Fact]
    public void Get_WhenNothingIsStored_StoresAnEmptyState()
    {
        var httpContext = new DefaultHttpContext();

        var first = RateLimitRequestState.Get(httpContext);
        var second = RateLimitRequestState.Get(httpContext);

        Assert.False(first.IsRefused);
        Assert.Empty(first.UsageMeters);
        Assert.Same(first, second);
    }

    [Fact]
    public void Get_ForAnotherRequest_ReturnsItsOwnState()
    {
        var firstRequest = new DefaultHttpContext();
        var secondRequest = new DefaultHttpContext();
        var firstState = RateLimitRequestState.Get(firstRequest);
        firstState.IsRefused = true;
        firstState.UsageMeters.Add("requests");

        var actual = RateLimitRequestState.Get(secondRequest);

        Assert.False(actual.IsRefused);
        Assert.Empty(actual.UsageMeters);
        Assert.NotSame(firstState, actual);
    }

    #endregion
}
