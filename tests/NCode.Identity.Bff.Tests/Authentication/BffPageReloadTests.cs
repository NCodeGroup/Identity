// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using NCode.Buffers;
using NCode.Identity.Bff.Authentication;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Authentication;

public class BffPageReloadTests : BaseTests
{
    private const string PageReloadCookie = "__Host-Http-bff-reload.default=1";

    private static DateTimeOffset Now { get; } = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static (
        BffPageReload Reload,
        BffFrontend Frontend,
        DefaultHttpContext HttpContext
    ) Create(Action<BffFrontendOptions>? configure = null)
    {
        var cookies = new BffCookies(new FakeTimeProvider(Now));
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog(configure));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/app/page";
        httpContext.Response.Body = new MemoryStream();
        return (new BffPageReload(cookies), frontend, httpContext);
    }

    private static async ValueTask<string> ReadBodyAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, SecureEncoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    #region ShouldReload Tests

    [Theory]
    [InlineData("cross-site")]
    [InlineData("none")]
    [InlineData(null)]
    public void ShouldReload_NavigationWithoutSessionCookie_ReturnsTrue(string? secFetchSite)
    {
        var (reload, frontend, httpContext) = Create();
        if (secFetchSite is not null)
            httpContext.Request.Headers["Sec-Fetch-Site"] = secFetchSite;

        var result = reload.ShouldReload(httpContext, frontend);

        Assert.True(result);
    }

    [Fact]
    public void ShouldReload_HeadRequest_ReturnsTrue()
    {
        var (reload, frontend, httpContext) = Create();
        httpContext.Request.Method = HttpMethods.Head;

        var result = reload.ShouldReload(httpContext, frontend);

        Assert.True(result);
    }

    [Fact]
    public void ShouldReload_NavigateFetchMode_ReturnsTrue()
    {
        var (reload, frontend, httpContext) = Create();
        httpContext.Request.Headers["Sec-Fetch-Mode"] = "navigate";

        var result = reload.ShouldReload(httpContext, frontend);

        Assert.True(result);
    }

    [Fact]
    public void ShouldReload_CorsFetchMode_ReturnsFalse()
    {
        var (reload, frontend, httpContext) = Create();
        httpContext.Request.Headers["Sec-Fetch-Mode"] = "cors";

        var result = reload.ShouldReload(httpContext, frontend);

        Assert.False(result);
    }

    [Fact]
    public void ShouldReload_PostRequest_ReturnsFalse()
    {
        var (reload, frontend, httpContext) = Create();
        httpContext.Request.Method = HttpMethods.Post;

        var result = reload.ShouldReload(httpContext, frontend);

        Assert.False(result);
    }

    [Fact]
    public void ShouldReload_LaxFrontend_ReturnsFalse()
    {
        var (reload, frontend, httpContext) = Create(f => f.Session.SameSite = SameSiteMode.Lax);

        var result = reload.ShouldReload(httpContext, frontend);

        Assert.False(result);
    }

    [Fact]
    public void ShouldReload_SessionCookie_ReturnsFalse()
    {
        var (reload, frontend, httpContext) = Create();
        httpContext.Request.Headers.Cookie =
            $"{BffTestFrontends.SessionCookieName}={new string('a', 43)}";

        var result = reload.ShouldReload(httpContext, frontend);

        Assert.False(result);
    }

    [Fact]
    public void ShouldReload_ReloadMarker_ReturnsFalse()
    {
        var (reload, frontend, httpContext) = Create();
        httpContext.Request.Headers.Cookie = PageReloadCookie;

        var result = reload.ShouldReload(httpContext, frontend);

        Assert.False(result);
    }

    [Fact]
    public void ShouldReload_ProtocolRelativePath_ReturnsFalse()
    {
        var (reload, frontend, httpContext) = Create();
        httpContext.Request.Path = "//evil.example/x";

        var result = reload.ShouldReload(httpContext, frontend);

        Assert.False(result);
    }

    #endregion

    #region WriteAsync Tests

    [Fact]
    public async Task WriteAsync_Always_WritesTheSharedPageSecurityHeaders()
    {
        var (reload, frontend, httpContext) = Create();

        await reload.WriteAsync(httpContext, frontend, CancellationToken.None);

        var headers = httpContext.Response.Headers;
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", httpContext.Response.ContentType);
        Assert.Equal(
            "default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
            headers.ContentSecurityPolicy.ToString()
        );
        Assert.Equal("DENY", headers.XFrameOptions.ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.Equal("same-origin", headers["Referrer-Policy"].ToString());
        Assert.Contains("no-store", headers.CacheControl.ToString());
        Assert.Equal("no-cache", headers.Pragma.ToString());
        Assert.False(headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task WriteAsync_Always_SetsTheReloadMarker()
    {
        var (reload, frontend, httpContext) = Create();

        await reload.WriteAsync(httpContext, frontend, CancellationToken.None);

        var setCookie = httpContext.Response.Headers.SetCookie.ToString();
        Assert.StartsWith($"{PageReloadCookie};", setCookie);
        Assert.Contains("samesite=lax", setCookie.ToLowerInvariant());
        Assert.Contains("max-age=60", setCookie.ToLowerInvariant());
    }

    [Fact]
    public async Task WriteAsync_Get_WritesTheEncodedOwnUrlAsMetaRefreshAndLink()
    {
        var (reload, frontend, httpContext) = Create();
        httpContext.Request.PathBase = "/base";
        httpContext.Request.QueryString = new QueryString("?a=1&b=2");

        await reload.WriteAsync(httpContext, frontend, CancellationToken.None);

        var body = await ReadBodyAsync(httpContext, CancellationToken.None);
        Assert.Equal(
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta http-equiv=\"refresh\" content=\"0;url=/base/app/page?a=1&amp;b=2\"><title>Continue</title></head><body><a href=\"/base/app/page?a=1&amp;b=2\">Continue</a></body></html>",
            body
        );
    }

    [Fact]
    public async Task WriteAsync_Head_WritesTheHeadersAndNoBody()
    {
        var (reload, frontend, httpContext) = Create();
        httpContext.Request.Method = HttpMethods.Head;

        await reload.WriteAsync(httpContext, frontend, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", httpContext.Response.ContentType);
        Assert.StartsWith(PageReloadCookie, httpContext.Response.Headers.SetCookie.ToString());
        Assert.Equal(string.Empty, await ReadBodyAsync(httpContext, CancellationToken.None));
    }

    #endregion
}
