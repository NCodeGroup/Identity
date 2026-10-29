#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
