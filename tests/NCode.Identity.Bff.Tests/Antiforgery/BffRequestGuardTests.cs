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
using NCode.Identity.Bff.Antiforgery;
using NCode.Identity.Bff.Options;
using Xunit;

namespace NCode.Identity.Bff.Tests.Antiforgery;

public class BffRequestGuardTests : BaseTests
{
    private const string TrustedOrigin = "https://app.example.com";

    private static BffOptions CreateOptions() => new() { TrustedOrigins = [TrustedOrigin] };

    private static HttpRequest CreateRequest(
        BffOptions options,
        string[] headerValues,
        string? site = null,
        string[]? origins = null
    )
    {
        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;
        if (headerValues.Length > 0)
            request.Headers[options.Antiforgery.HeaderName] = headerValues;
        if (site is not null)
            request.Headers["Sec-Fetch-Site"] = site;
        if (origins is not null)
            request.Headers.Origin = origins;
        return request;
    }

    #region Check Tests

    public static TheoryData<string[]> BadHeaderValues =>
        new() { Array.Empty<string>(), new[] { "0" }, new[] { "1", "1" } };

    public static TheoryData<string[]> UntrustedOrigins =>
        new()
        {
            Array.Empty<string>(),
            new[] { "https://evil.example.com" },
            new[] { TrustedOrigin, TrustedOrigin },
        };

    [Theory]
    [MemberData(nameof(BadHeaderValues))]
    public void Check_HeaderMissingOrWrong_ReturnsHeaderMissing(string[] values)
    {
        var options = CreateOptions();
        var request = CreateRequest(options, values);

        var reason = BffRequestGuard.Check(request, options);

        Assert.Equal(BffRequestGuard.AntiforgeryHeaderMissing, reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("same-origin")]
    [InlineData("none")]
    public void Check_HeaderAndPassingSite_ReturnsNull(string? site)
    {
        var options = CreateOptions();
        var request = CreateRequest(options, [options.Antiforgery.HeaderValue], site);

        var reason = BffRequestGuard.Check(request, options);

        Assert.Null(reason);
    }

    [Theory]
    [InlineData("cross-site")]
    [InlineData("unknown")]
    [InlineData("")]
    public void Check_RefusedSite_ReturnsCrossSite(string site)
    {
        var options = CreateOptions();
        var request = CreateRequest(options, [options.Antiforgery.HeaderValue], site);

        var reason = BffRequestGuard.Check(request, options);

        Assert.Equal(BffRequestGuard.CrossSiteRequest, reason);
    }

    [Theory]
    [MemberData(nameof(UntrustedOrigins))]
    public void Check_SameSiteUntrustedOrigin_ReturnsCrossSite(string[] origins)
    {
        var options = CreateOptions();
        var request = CreateRequest(
            options,
            [options.Antiforgery.HeaderValue],
            "same-site",
            origins
        );

        var reason = BffRequestGuard.Check(request, options);

        Assert.Equal(BffRequestGuard.CrossSiteRequest, reason);
    }

    [Fact]
    public void Check_SameSiteTrustedOrigin_ReturnsNull()
    {
        var options = CreateOptions();
        var request = CreateRequest(
            options,
            [options.Antiforgery.HeaderValue],
            "same-site",
            [TrustedOrigin]
        );

        var reason = BffRequestGuard.Check(request, options);

        Assert.Null(reason);
    }

    #endregion

    #region CheckPage Tests

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public void CheckPage_SafeMethod_ReturnsNull(string method)
    {
        var options = CreateOptions();
        var request = CreateRequest(options, []);
        request.Method = method;

        var reason = BffRequestGuard.CheckPage(request);

        Assert.Null(reason);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void CheckPage_UnsafeMethod_ReturnsUnsafeMethod(string method)
    {
        var options = CreateOptions();
        var request = CreateRequest(options, []);
        request.Method = method;

        var reason = BffRequestGuard.CheckPage(request);

        Assert.Equal(BffRequestGuard.UnsafeMethod, reason);
    }

    #endregion
}
