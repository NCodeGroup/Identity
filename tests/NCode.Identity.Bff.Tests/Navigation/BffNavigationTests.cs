// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Navigation;
using NCode.Identity.Bff.Options;
using Xunit;

namespace NCode.Identity.Bff.Tests.Navigation;

public class BffNavigationTests : BaseTests
{
    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("app.example.com");
        httpContext.Request.PathBase = "/app";
        return httpContext;
    }

    #region CallbackUri Tests

    [Fact]
    public void CallbackUri_WithPathBase_IncludesPathBaseAndPrefix()
    {
        var httpContext = CreateHttpContext();

        var result = BffNavigation.CallbackUri(httpContext.Request, new BffOptions());

        Assert.Equal("https://app.example.com/app/bff/callback", result);
    }

    #endregion

    #region LocalUrl Tests

    [Fact]
    public void LocalUrl_WithPathBase_PrefixesPath()
    {
        var httpContext = CreateHttpContext();

        var result = BffNavigation.LocalUrl(httpContext.Request, "/home");

        Assert.Equal("/app/home", result);
    }

    #endregion

    #region ErrorUrl Tests

    [Fact]
    public void ErrorUrl_WithReason_EscapesReason()
    {
        var httpContext = CreateHttpContext();
        var options = new BffOptions { ErrorPath = "/oops" };

        var result = BffNavigation.ErrorUrl(httpContext.Request, options, "a b");

        Assert.Equal("/app/oops?error=a%20b", result);
    }

    #endregion

    #region AbsoluteUrl Tests

    [Fact]
    public void AbsoluteUrl_WithPathBase_BuildsFullUrl()
    {
        var httpContext = CreateHttpContext();

        var result = BffNavigation.AbsoluteUrl(httpContext.Request, "/home");

        Assert.Equal("https://app.example.com/app/home", result);
    }

    #endregion
}
