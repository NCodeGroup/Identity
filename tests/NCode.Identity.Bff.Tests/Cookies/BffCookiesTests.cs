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
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Options;
using Xunit;

namespace NCode.Identity.Bff.Tests.Cookies;

public class BffCookiesTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly string Value43 = new('a', 43);

    private static BffCookies CreateCookies() =>
        new(
            Microsoft.Extensions.Options.Options.Create(new BffOptions()),
            new MutableTimeProvider(Now)
        );

    private static string GetSetCookie(DefaultHttpContext httpContext) =>
        Assert.Single(httpContext.Response.Headers.SetCookie.ToArray()) ?? string.Empty;

    private static void AssertCommonAttributes(string setCookie)
    {
        var lower = setCookie.ToLowerInvariant();
        Assert.Contains("secure", lower);
        Assert.Contains("httponly", lower);
        Assert.Contains("path=/", lower);
        Assert.DoesNotContain("domain=", lower);
    }

    #region SetSession Tests

    [Fact]
    public void SetSession_Always_WritesStrictSecureCookie()
    {
        var httpContext = new DefaultHttpContext();

        CreateCookies().SetSession(httpContext.Response, Value43, Now.AddHours(2));

        var setCookie = GetSetCookie(httpContext);
        Assert.StartsWith($"{BffDefaults.SessionCookieName}={Value43}", setCookie);
        Assert.Contains("samesite=strict", setCookie.ToLowerInvariant());
        Assert.Contains("max-age=7200", setCookie.ToLowerInvariant());
        AssertCommonAttributes(setCookie);
    }

    #endregion

    #region DeleteSession Tests

    [Fact]
    public void DeleteSession_Always_ExpiresCookieWithSameAttributes()
    {
        var httpContext = new DefaultHttpContext();

        CreateCookies().DeleteSession(httpContext.Response);

        var setCookie = GetSetCookie(httpContext);
        Assert.StartsWith($"{BffDefaults.SessionCookieName}=;", setCookie);
        Assert.Contains("samesite=strict", setCookie.ToLowerInvariant());
        AssertCommonAttributes(setCookie);
    }

    #endregion

    #region SetLogin Tests

    [Fact]
    public void SetLogin_Always_WritesLaxSecureCookie()
    {
        var httpContext = new DefaultHttpContext();

        CreateCookies().SetLogin(httpContext.Response, Value43);

        var setCookie = GetSetCookie(httpContext);
        Assert.StartsWith($"{BffDefaults.LoginCookieName}={Value43}", setCookie);
        Assert.Contains("samesite=lax", setCookie.ToLowerInvariant());
        Assert.Contains("max-age=600", setCookie.ToLowerInvariant());
        AssertCommonAttributes(setCookie);
    }

    #endregion

    #region DeleteLogin Tests

    [Fact]
    public void DeleteLogin_Always_ExpiresCookieWithSameAttributes()
    {
        var httpContext = new DefaultHttpContext();

        CreateCookies().DeleteLogin(httpContext.Response);

        var setCookie = GetSetCookie(httpContext);
        Assert.StartsWith($"{BffDefaults.LoginCookieName}=;", setCookie);
        Assert.Contains("samesite=lax", setCookie.ToLowerInvariant());
        AssertCommonAttributes(setCookie);
    }

    #endregion

    #region ReadSessionHandle Tests

    [Fact]
    public void ReadSessionHandle_When43Characters_ReturnsValue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{BffDefaults.SessionCookieName}={Value43}";

        var result = CreateCookies().ReadSessionHandle(httpContext.Request);

        Assert.Equal(Value43, result);
    }

    [Fact]
    public void ReadSessionHandle_When42Characters_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie =
            $"{BffDefaults.SessionCookieName}={new string('a', 42)}";

        var result = CreateCookies().ReadSessionHandle(httpContext.Request);

        Assert.Null(result);
    }

    #endregion

    #region ReadLoginSecret Tests

    [Fact]
    public void ReadLoginSecret_When43Characters_ReturnsValue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{BffDefaults.LoginCookieName}={Value43}";

        var result = CreateCookies().ReadLoginSecret(httpContext.Request);

        Assert.Equal(Value43, result);
    }

    [Fact]
    public void ReadLoginSecret_When42Characters_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{BffDefaults.LoginCookieName}={new string('a', 42)}";

        var result = CreateCookies().ReadLoginSecret(httpContext.Request);

        Assert.Null(result);
    }

    [Fact]
    public void ReadLoginSecret_WhenMissing_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();

        var result = CreateCookies().ReadLoginSecret(httpContext.Request);

        Assert.Null(result);
    }

    #endregion
}
