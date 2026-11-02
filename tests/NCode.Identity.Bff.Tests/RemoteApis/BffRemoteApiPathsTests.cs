// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NCode.Identity.Bff.RemoteApis;
using Xunit;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public class BffRemoteApiPathsTests : BaseTests
{
    private static DefaultHttpContext CreateContext(
        string path,
        string? remainder,
        string? rawTarget = null
    )
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.RouteValues[BffRemoteApiPaths.RemainderParameter] = remainder;
        if (rawTarget is not null)
            context.Features.Get<IHttpRequestFeature>()!.RawTarget = rawTarget;
        return context;
    }

    #region TryGetRemainder Tests

    [Theory]
    [InlineData("/api/orders/items/1", "items/1", "/items/1")]
    [InlineData("/api/orders", null, "")]
    [InlineData("/api/orders", "", "")]
    [InlineData("/api/orders/", null, "/")]
    [InlineData("/tenant/api/orders/items", "items", "/items")]
    public void TryGetRemainder_MatchedRemainder_ReturnsIt(
        string path,
        string? routeValue,
        string expected
    )
    {
        var context = CreateContext(path, routeValue);

        var result = BffRemoteApiPaths.TryGetRemainder(context, out var remainder);

        Assert.True(result);
        Assert.Equal(expected, remainder.Value ?? string.Empty);
    }

    [Theory]
    [InlineData("/api/orders/items", "other")]
    [InlineData("/api/orders/items", "/api/orders/items/x")]
    [InlineData("/api/ordersitems", "items")]
    public void TryGetRemainder_RemainderNotInPath_ReturnsFalse(string path, string routeValue)
    {
        var context = CreateContext(path, routeValue);

        var result = BffRemoteApiPaths.TryGetRemainder(context, out var remainder);

        Assert.False(result);
        Assert.Equal(PathString.Empty, remainder);
    }

    [Theory]
    [InlineData("/api/orders/../admin", "../admin")]
    [InlineData("/api/orders/./items", "./items")]
    [InlineData("/api/orders/a\\b", "a\\b")]
    [InlineData("/api/orders/a%2Fb", "a%2Fb")]
    [InlineData("/api/orders/a%2fb", "a%2fb")]
    [InlineData("/api/orders/a%5Cb", "a%5Cb")]
    public void TryGetRemainder_UnsafeDecodedPath_ReturnsFalse(string path, string routeValue)
    {
        var context = CreateContext(path, routeValue);

        var result = BffRemoteApiPaths.TryGetRemainder(context, out _);

        Assert.False(result);
    }

    [Theory]
    [InlineData("/api/orders/%2E%2E/admin")]
    [InlineData("/api/orders/%2e/items")]
    [InlineData("/api/orders/a%2Fb")]
    [InlineData("/api/orders/a%5Cb")]
    public void TryGetRemainder_UnsafeRawTarget_ReturnsFalse(string rawTarget)
    {
        var context = CreateContext("/api/orders/items", "items", rawTarget);

        var result = BffRemoteApiPaths.TryGetRemainder(context, out _);

        Assert.False(result);
    }

    [Theory]
    [InlineData("/api/orders/items?q=../x")]
    [InlineData("https://app.example.com/api/orders/items")]
    [InlineData("")]
    public void TryGetRemainder_SafeRawTarget_ReturnsTrue(string rawTarget)
    {
        var context = CreateContext("/api/orders/items", "items", rawTarget);

        var result = BffRemoteApiPaths.TryGetRemainder(context, out var remainder);

        Assert.True(result);
        Assert.Equal("/items", remainder.Value);
    }

    #endregion
}
