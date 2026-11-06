// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Buffers;
using NCode.Identity.Bff.Authentication;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Jose;
using Xunit;

namespace NCode.Identity.Bff.Tests.Authentication;

public class BffAuthenticationHandlerTests : BaseTests
{
    private const string PageReloadCookie = "__Host-Http-bff-reload.default=1";

    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static BffSessionState CreateState() =>
        new(
            new string('a', 43),
            "store-key",
            1,
            new BffSessionMetadata
            {
                Frontend = BffTestFrontends.Name,
                Issuer = "https://op.example",
                Subject = "sub-1",
                CreatedAt = Now,
                LastAccessedAt = Now,
                AbsoluteExpiresAt = Now.AddHours(8),
                IdleExpiresAt = Now.AddHours(1),
            },
            new BffSessionPayload
            {
                IdToken = "id-token",
                LogoutGuard = "guard",
                Claims =
                [
                    new BffStoredClaim { Type = JoseClaimNames.Payload.Sub, Value = "sub-1" },
                ],
            }
        );

    private async Task<(
        BffAuthenticationHandler Handler,
        DefaultHttpContext HttpContext
    )> CreateHandlerAsync(
        BffSessionState? session,
        object? endpointMetadata = null,
        Action<BffFrontendOptions>? configure = null,
        BffFrontendCatalog? catalog = null
    )
    {
        var timeProvider = new FakeTimeProvider(Now);
        var cookies = new BffCookies(timeProvider);
        var manager = CreateStrictMock<IBffSessionManager>();
        catalog ??= BffTestFrontends.CreateCatalog(configure);
        var resolver = new BffSessionResolver(manager.Object, cookies, [], catalog, timeProvider);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/";
        httpContext.Response.Body = new MemoryStream();
        var feature = new BffRequestFeature
        {
            IsResolved = true,
            Session = session,
            Principal = session is null ? null : BffSessionPrincipal.Create(session, "/bff/logout"),
        };
        if (endpointMetadata is not null)
        {
            var endpoint = new Endpoint(
                null,
                new EndpointMetadataCollection(endpointMetadata),
                "test"
            );
            httpContext.SetEndpoint(endpoint);
            feature.CheckedEndpoint = endpoint;
        }

        httpContext.Features.Set(feature);

        var schemeOptions = CreateStrictMock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        schemeOptions
            .Setup(x => x.Get(It.IsAny<string>()))
            .Returns(new AuthenticationSchemeOptions())
            .Verifiable();

        var handler = new BffAuthenticationHandler(
            schemeOptions.Object,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            resolver,
            catalog,
            new BffConnectionSessions(resolver),
            cookies
        );
        var scheme = new AuthenticationScheme(
            BffDefaults.AuthenticationScheme,
            null,
            typeof(BffAuthenticationHandler)
        );
        await handler.InitializeAsync(scheme, httpContext);
        return (handler, httpContext);
    }

    private static async Task<string> ReadBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, SecureEncoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static void AssertNoSetCookie(HttpContext httpContext) =>
        Assert.False(httpContext.Response.Headers.ContainsKey("Set-Cookie"));

    #region AuthenticateAsync Tests

    [Fact]
    public async Task AuthenticateAsync_WithSession_Succeeds()
    {
        var (handler, _) = await CreateHandlerAsync(CreateState(), new BffApiAttribute());

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(BffDefaults.AuthenticationScheme, result.Ticket?.AuthenticationScheme);
        Assert.Equal("sub-1", result.Principal?.FindFirstValue(JoseClaimNames.Payload.Sub));
    }

    [Fact]
    public async Task AuthenticateAsync_PageEndpoint_Succeeds()
    {
        var (handler, _) = await CreateHandlerAsync(CreateState(), new BffPageAttribute());

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(BffDefaults.AuthenticationScheme, result.Ticket?.AuthenticationScheme);
        Assert.Equal("sub-1", result.Principal?.FindFirstValue(JoseClaimNames.Payload.Sub));
    }

    [Fact]
    public async Task AuthenticateAsync_HubEndpoint_PrincipalCarriesConnectionSession()
    {
        var (handler, _) = await CreateHandlerAsync(CreateState(), new BffHubAttribute());

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("sub-1", result.Principal?.FindFirstValue(JoseClaimNames.Payload.Sub));
        Assert.Equal("/bff/logout", result.Principal?.FindFirstValue(BffClaimTypes.LogoutUrl));
        var connection = result.Principal?.GetBffConnectionSession();
        Assert.NotNull(connection);
        Assert.Equal(BffTestFrontends.Name, connection.Frontend);
    }

    [Fact]
    public async Task AuthenticateAsync_PageEndpoint_PrincipalCarriesNoConnectionSession()
    {
        var (handler, _) = await CreateHandlerAsync(CreateState(), new BffPageAttribute());

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Null(result.Principal?.GetBffConnectionSession());
    }

    [Fact]
    public async Task AuthenticateAsync_UnguardedEndpoint_ReturnsNoResult()
    {
        var (handler, _) = await CreateHandlerAsync(CreateState());

        var result = await handler.AuthenticateAsync();

        Assert.True(result.None);
    }

    [Fact]
    public async Task AuthenticateAsync_NoSession_ReturnsNoResult()
    {
        var (handler, _) = await CreateHandlerAsync(null, new BffApiAttribute());

        var result = await handler.AuthenticateAsync();

        Assert.True(result.None);
    }

    #endregion

    #region ChallengeAsync Tests

    [Fact]
    public async Task ChallengeAsync_ApiEndpoint_WritesUnauthorizedProblem()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffApiAttribute());

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);
        var body = await ReadBodyAsync(httpContext);
        Assert.Contains(BffProblemTypes.SessionRequired, body);
    }

    [Fact]
    public async Task ChallengeAsync_HubEndpoint_WritesUnauthorizedProblem()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffHubAttribute());

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);
        var body = await ReadBodyAsync(httpContext);
        Assert.Contains(BffProblemTypes.SessionRequired, body);
    }

    [Fact]
    public async Task ChallengeAsync_PageEndpoint_AllowedPath_RedirectsToLoginWithReturnUrl()
    {
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffPageAttribute(),
            f => f.ReturnUrls = ["/app"]
        );
        httpContext.Request.Path = "/app/page";
        httpContext.Request.QueryString = new QueryString("?a=1");
        httpContext.Request.Headers.Cookie = PageReloadCookie;

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal(
            "/bff/login?returnUrl=%2Fapp%2Fpage%3Fa%3D1",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Fact]
    public async Task ChallengeAsync_PageEndpoint_FrontendWithPathPrefix_RedirectsToItsLogin()
    {
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffPageAttribute(),
            f =>
            {
                f.PathPrefix = "/admin/bff";
                f.ReturnUrls = ["/admin"];
            }
        );
        httpContext.Request.Path = "/admin/page";
        httpContext.Request.Headers.Cookie = PageReloadCookie;

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal(
            "/admin/bff/login?returnUrl=%2Fadmin%2Fpage",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Fact]
    public async Task ChallengeAsync_PageEndpoint_DisallowedPath_RedirectsToLoginWithRoot()
    {
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffPageAttribute(),
            f => f.ReturnUrls = ["/app"]
        );
        httpContext.Request.Path = "/other";
        httpContext.Request.Headers.Cookie = PageReloadCookie;

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/bff/login?returnUrl=%2F", httpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task ChallengeAsync_PageWithoutAFrontend_Answers401()
    {
        var catalog = BffTestFrontends.CreateCatalog(f =>
            f.Selection = new BffFrontendSelectionOptions
            {
                Rule = BffFrontendSelectorNames.Path,
                Values = ["/admin"],
            }
        );
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffPageAttribute(),
            catalog: catalog
        );
        httpContext.Request.Path = "/other";

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);
        var body = await ReadBodyAsync(httpContext);
        Assert.Contains(BffProblemTypes.SessionRequired, body);
    }

    [Theory]
    [InlineData("cross-site")]
    [InlineData("none")]
    [InlineData(null)]
    public async Task ChallengeAsync_PageNavigationWithoutSessionCookie_WritesReloadPage(
        string? secFetchSite
    )
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffPageAttribute());
        httpContext.Request.Path = "/app/page";
        if (secFetchSite is not null)
            httpContext.Request.Headers["Sec-Fetch-Site"] = secFetchSite;

        await handler.ChallengeAsync(null);

        var headers = httpContext.Response.Headers;
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", httpContext.Response.ContentType);
        Assert.Equal("no-store", headers.CacheControl.ToString());
        Assert.Equal("no-referrer", headers["Referrer-Policy"].ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.Equal(
            "default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
            headers.ContentSecurityPolicy.ToString()
        );
        Assert.False(headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task ChallengeAsync_PageNavigationWithoutSessionCookie_SetsTheReloadMarker()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffPageAttribute());

        await handler.ChallengeAsync(null);

        var setCookie = httpContext.Response.Headers.SetCookie.ToString();
        Assert.StartsWith($"{PageReloadCookie};", setCookie);
        Assert.Contains("samesite=lax", setCookie.ToLowerInvariant());
        Assert.Contains("max-age=60", setCookie.ToLowerInvariant());
    }

    [Fact]
    public async Task ChallengeAsync_PageNavigationWithoutSessionCookie_ReloadsTheRequestsOwnUrl()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffPageAttribute());
        httpContext.Request.PathBase = "/base";
        httpContext.Request.Path = "/app/page";
        httpContext.Request.QueryString = new QueryString("?a=1&b=2");

        await handler.ChallengeAsync(null);

        var body = await ReadBodyAsync(httpContext);
        Assert.Equal(
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta http-equiv=\"refresh\" content=\"0;url=/base/app/page?a=1&amp;b=2\"><title>Continue</title></head><body><a href=\"/base/app/page?a=1&amp;b=2\">Continue</a></body></html>",
            body
        );
    }

    [Fact]
    public async Task ChallengeAsync_PageNavigationWithNavigateFetchMode_WritesReloadPage()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffPageAttribute());
        httpContext.Request.Headers["Sec-Fetch-Mode"] = "navigate";

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task ChallengeAsync_PageHeadRequestWithoutSessionCookie_WritesReloadHeadersAndNoBody()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffPageAttribute());
        httpContext.Request.Method = HttpMethods.Head;

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", httpContext.Response.ContentType);
        Assert.StartsWith(PageReloadCookie, httpContext.Response.Headers.SetCookie.ToString());
        Assert.Equal(string.Empty, await ReadBodyAsync(httpContext));
    }

    [Fact]
    public async Task ChallengeAsync_PageNavigationWithProtocolRelativePath_RedirectsToLoginWithoutReload()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffPageAttribute());
        httpContext.Request.Path = "//evil.example/x";

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/bff/login?returnUrl=%2F", httpContext.Response.Headers.Location.ToString());
        Assert.False(httpContext.Response.Headers.ContainsKey("Set-Cookie"));
        var body = await ReadBodyAsync(httpContext);
        Assert.DoesNotContain("evil.example", body);
    }

    [Fact]
    public async Task ChallengeAsync_PageNavigationWithSessionCookie_RedirectsToLogin()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffPageAttribute());
        httpContext.Request.Headers.Cookie =
            $"{BffTestFrontends.SessionCookieName}={new string('a', 43)}";

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/bff/login?returnUrl=%2F", httpContext.Response.Headers.Location.ToString());
        AssertNoSetCookie(httpContext);
    }

    [Fact]
    public async Task ChallengeAsync_PageNavigationWithReloadMarker_RedirectsToLoginAndDeletesTheMarker()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffPageAttribute());
        httpContext.Request.Headers.Cookie = PageReloadCookie;

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/bff/login?returnUrl=%2F", httpContext.Response.Headers.Location.ToString());
        Assert.StartsWith(
            "__Host-Http-bff-reload.default=;",
            httpContext.Response.Headers.SetCookie.ToString()
        );
    }

    [Fact]
    public async Task ChallengeAsync_PageRequestWithCorsFetchMode_RedirectsToLogin()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffPageAttribute());
        httpContext.Request.Headers["Sec-Fetch-Mode"] = "cors";

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/bff/login?returnUrl=%2F", httpContext.Response.Headers.Location.ToString());
        AssertNoSetCookie(httpContext);
    }

    [Fact]
    public async Task ChallengeAsync_PageNavigationToLaxFrontend_RedirectsToLogin()
    {
        var (handler, httpContext) = await CreateHandlerAsync(
            null,
            new BffPageAttribute(),
            f => f.Session.SameSite = SameSiteMode.Lax
        );

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/bff/login?returnUrl=%2F", httpContext.Response.Headers.Location.ToString());
        AssertNoSetCookie(httpContext);
    }

    [Fact]
    public async Task ChallengeAsync_PagePostRequest_RedirectsToLogin()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffPageAttribute());
        httpContext.Request.Method = HttpMethods.Post;

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/bff/login?returnUrl=%2F", httpContext.Response.Headers.Location.ToString());
        AssertNoSetCookie(httpContext);
    }

    [Fact]
    public async Task ChallengeAsync_UnguardedEndpoint_Throws()
    {
        var (handler, _) = await CreateHandlerAsync(null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ChallengeAsync(null)
        );

        Assert.Contains("does not guard", exception.Message);
    }

    #endregion

    #region SignOutAsync Tests

    [Fact]
    public async Task SignOutAsync_UnguardedEndpoint_Throws()
    {
        var (handler, _) = await CreateHandlerAsync(CreateState());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SignOutAsync(null)
        );

        Assert.Contains("AsBffApi() or [BffApi]", exception.Message);
    }

    #endregion

    #region ForbidAsync Tests

    [Fact]
    public async Task ForbidAsync_ApiEndpoint_WritesForbiddenProblem()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffApiAttribute());

        await handler.ForbidAsync(null);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        var body = await ReadBodyAsync(httpContext);
        Assert.Contains(BffProblemTypes.Forbidden, body);
    }

    [Fact]
    public async Task ForbidAsync_HubEndpoint_WritesForbiddenProblem()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffHubAttribute());

        await handler.ForbidAsync(null);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        var body = await ReadBodyAsync(httpContext);
        Assert.Contains(BffProblemTypes.Forbidden, body);
    }

    #endregion
}
