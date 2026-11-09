// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Cookies;

public class BffCookiesTests : BaseTests
{
    private static DateTimeOffset Now { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const string PageReloadCookieName = "__Host-Http-bff-reload.default";

    private static string Value43 { get; } = new('a', 43);

    private static BffCookies CreateCookies() => new(new FakeTimeProvider(Now));

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
    public void SetSession_WithFrontend_WritesTheFrontendsCookie()
    {
        var httpContext = new DefaultHttpContext();
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        CreateCookies().SetSession(httpContext.Response, frontend, Value43, Now.AddHours(2));

        var setCookie = GetSetCookie(httpContext);
        Assert.StartsWith($"{BffTestFrontends.SessionCookieName}={Value43}", setCookie);
        Assert.Contains("samesite=strict", setCookie.ToLowerInvariant());
        Assert.Contains("max-age=7200", setCookie.ToLowerInvariant());
        AssertCommonAttributes(setCookie);
    }

    [Fact]
    public void SetSession_WithFrontendSameSite_UsesTheFrontendsSameSite()
    {
        var httpContext = new DefaultHttpContext();
        var frontend = BffTestFrontends.Get(
            BffTestFrontends.CreateCatalog(x => x.Session.SameSite = SameSiteMode.Lax)
        );

        CreateCookies().SetSession(httpContext.Response, frontend, Value43, Now.AddHours(2));

        var setCookie = GetSetCookie(httpContext);
        Assert.Contains("samesite=lax", setCookie.ToLowerInvariant());
    }

    #endregion

    #region DeleteSession Tests

    [Fact]
    public void DeleteSession_WithFrontend_ExpiresTheFrontendsCookie()
    {
        var httpContext = new DefaultHttpContext();
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        CreateCookies().DeleteSession(httpContext.Response, frontend);

        var setCookie = GetSetCookie(httpContext);
        Assert.StartsWith($"{BffTestFrontends.SessionCookieName}=;", setCookie);
        Assert.Contains("samesite=strict", setCookie.ToLowerInvariant());
        AssertCommonAttributes(setCookie);
    }

    #endregion

    #region SetLogin Tests

    [Fact]
    public void SetLogin_WithFrontend_WritesTheFrontendsCookie()
    {
        var httpContext = new DefaultHttpContext();
        var frontend = BffTestFrontends.Get(
            BffTestFrontends.CreateCatalog(x => x.Session.LoginLifetime = TimeSpan.FromMinutes(5))
        );

        CreateCookies().SetLogin(httpContext.Response, frontend, Value43);

        var setCookie = GetSetCookie(httpContext);
        Assert.StartsWith($"{BffTestFrontends.LoginCookieName}={Value43}", setCookie);
        Assert.Contains("samesite=lax", setCookie.ToLowerInvariant());
        Assert.Contains("max-age=300", setCookie.ToLowerInvariant());
        AssertCommonAttributes(setCookie);
    }

    #endregion

    #region DeleteLogin Tests

    [Fact]
    public void DeleteLogin_WithFrontend_ExpiresTheFrontendsCookie()
    {
        var httpContext = new DefaultHttpContext();
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        CreateCookies().DeleteLogin(httpContext.Response, frontend);

        var setCookie = GetSetCookie(httpContext);
        Assert.StartsWith($"{BffTestFrontends.LoginCookieName}=;", setCookie);
        Assert.Contains("samesite=lax", setCookie.ToLowerInvariant());
        AssertCommonAttributes(setCookie);
    }

    #endregion

    #region SetPageReload Tests

    [Fact]
    public void SetPageReload_WithFrontend_WritesTheFrontendsLaxMarker()
    {
        var httpContext = new DefaultHttpContext();
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        CreateCookies().SetPageReload(httpContext.Response, frontend);

        var setCookie = GetSetCookie(httpContext);
        Assert.StartsWith($"{PageReloadCookieName}=1;", setCookie);
        Assert.Contains("samesite=lax", setCookie.ToLowerInvariant());
        Assert.Contains("max-age=60", setCookie.ToLowerInvariant());
        AssertCommonAttributes(setCookie);
    }

    [Fact]
    public void SetPageReload_WithStrictFrontend_StaysLax()
    {
        var httpContext = new DefaultHttpContext();
        var frontend = BffTestFrontends.Get(
            BffTestFrontends.CreateCatalog(x => x.Session.SameSite = SameSiteMode.Strict)
        );

        CreateCookies().SetPageReload(httpContext.Response, frontend);

        var setCookie = GetSetCookie(httpContext);
        Assert.Contains("samesite=lax", setCookie.ToLowerInvariant());
    }

    #endregion

    #region DeletePageReload Tests

    [Fact]
    public void DeletePageReload_WithFrontend_ExpiresTheFrontendsCookie()
    {
        var httpContext = new DefaultHttpContext();
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        CreateCookies().DeletePageReload(httpContext.Response, frontend);

        var setCookie = GetSetCookie(httpContext);
        Assert.StartsWith($"{PageReloadCookieName}=;", setCookie);
        Assert.Contains("samesite=lax", setCookie.ToLowerInvariant());
        AssertCommonAttributes(setCookie);
    }

    #endregion

    #region HasPageReload Tests

    [Fact]
    public void HasPageReload_WithFrontendCookie_ReturnsTrue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{PageReloadCookieName}=1";
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        var result = CreateCookies().HasPageReload(httpContext.Request, frontend);

        Assert.True(result);
    }

    [Fact]
    public void HasPageReload_WhenMissing_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        var result = CreateCookies().HasPageReload(httpContext.Request, frontend);

        Assert.False(result);
    }

    [Fact]
    public void HasPageReload_WithAnotherFrontendsCookie_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{BffDefaults.PageReloadCookieName}.other=1";
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        var result = CreateCookies().HasPageReload(httpContext.Request, frontend);

        Assert.False(result);
    }

    #endregion

    #region ReadSessionHandle Tests

    [Fact]
    public void ReadSessionHandle_WithFrontendCookie_ReturnsValue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{BffTestFrontends.SessionCookieName}={Value43}";
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        var result = CreateCookies().ReadSessionHandle(httpContext.Request, frontend);

        Assert.Equal(Value43, result);
    }

    [Fact]
    public void ReadSessionHandle_WithFrontendCookieOf42Characters_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie =
            $"{BffTestFrontends.SessionCookieName}={new string('a', 42)}";
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        var result = CreateCookies().ReadSessionHandle(httpContext.Request, frontend);

        Assert.Null(result);
    }

    [Fact]
    public void ReadSessionHandle_WithAnotherFrontendsCookie_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{BffDefaults.SessionCookieName}.other={Value43}";
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        var result = CreateCookies().ReadSessionHandle(httpContext.Request, frontend);

        Assert.Null(result);
    }

    #endregion

    #region ReadLoginSecret Tests

    [Fact]
    public void ReadLoginSecret_WithFrontendCookie_ReturnsValue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{BffTestFrontends.LoginCookieName}={Value43}";
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        var result = CreateCookies().ReadLoginSecret(httpContext.Request, frontend);

        Assert.Equal(Value43, result);
    }

    [Fact]
    public void ReadLoginSecret_WithFrontendCookieOf42Characters_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie =
            $"{BffTestFrontends.LoginCookieName}={new string('a', 42)}";
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        var result = CreateCookies().ReadLoginSecret(httpContext.Request, frontend);

        Assert.Null(result);
    }

    [Fact]
    public void ReadLoginSecret_WhenMissing_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        var result = CreateCookies().ReadLoginSecret(httpContext.Request, frontend);

        Assert.Null(result);
    }

    [Fact]
    public void ReadLoginSecret_WithAnotherFrontendsCookie_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{BffDefaults.LoginCookieName}.other={Value43}";
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

        var result = CreateCookies().ReadLoginSecret(httpContext.Request, frontend);

        Assert.Null(result);
    }

    #endregion
}
